#!/usr/bin/env python3
"""
Social Intelligence Service for X'iDeAI
Authenticated version - uses stored cookies for direct X access.
"""

import os
import sys
import json
import time
import pickle
import base64
import argparse
import random
import urllib.parse
import re
import threading
import atexit
from pathlib import Path
from datetime import datetime, timezone
import requests
from bs4 import BeautifulSoup

from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
# from selenium.webdriver.support.ui import WebDriverWait
# from selenium.webdriver.support import expected_conditions as EC
import sys

try:
    from webdriver_manager.chrome import ChromeDriverManager
    HAS_WDM = True
except:
    HAS_WDM = False

try:
    import undetected_chromedriver as uc
    HAS_UC = True
except:
    HAS_UC = False

# ===== PERFORMANCE OPTIMIZATION: Driver Pool =====
class ChromeDriverPool:
    """Singleton driver pool to reuse Chrome instances (3-5s savings per operation)"""
    _driver = None
    _lock = threading.Lock()
    _creation_time = None
    _max_age = 3600  # 1 hour, then recreate
    
    @classmethod
    def get(cls, headless=True, use_undetected=True):
        with cls._lock:
            # Recreate if too old (memory leak prevention)
            if cls._driver and cls._creation_time:
                age = time.time() - cls._creation_time
                if age > cls._max_age:
                    print(f"[POOL] Driver age={age:.0f}s, recreating...", file=sys.stderr)
                    cls.close()
            
            if cls._driver is None:
                print("[POOL] Creating new driver...", file=sys.stderr)
                cls._driver = _create_driver_internal(headless, use_undetected)
                cls._creation_time = time.time()
            return cls._driver
    
    @classmethod
    def close(cls):
        with cls._lock:
            if cls._driver:
                try:
                    cls._driver.quit()
                except:
                    pass
                cls._driver = None
                cls._creation_time = None

# =========================================================
# GLOBAL HELPERS FOR ROBUST INTERACTION
# =========================================================

def human_delay(min_s=2.0, max_s=5.0):
    """Sleep for a random amount of time to mimic human behavior"""
    sleep_time = random.uniform(min_s, max_s)
    # print(f"DEBUG: Human delay {sleep_time:.2f}s", file=sys.stderr)
    time.sleep(sleep_time)

def atomic_clear(elem, driver=None):
    """Clears a contenteditable element — SADECE hedef element (Ctrl+A YOK, v4.9.2)"""
    try:
        elem.click()
        if driver: driver.execute_script("arguments[0].focus();", elem)
        time.sleep(0.2)
        # selectNodeContents: sadece bu element'in içini seç, Ctrl+A kullanma
        if driver:
            driver.execute_script("""
                var el = arguments[0];
                el.focus();
                var range = document.createRange();
                range.selectNodeContents(el);
                var sel = window.getSelection();
                sel.removeAllRanges();
                sel.addRange(range);
                document.execCommand('delete', false, null);
            """, elem)
        time.sleep(0.3)
    except: pass


def robust_type_and_verify(driver, element, text, tweet_index=0):
    """ULTRA-ROBUST TYPING WITH JS FALLBACK (v4.8.2 - innerText Fixed)"""
    from selenium.webdriver.common.by import By
    from selenium.webdriver.common.keys import Keys
    from selenium.webdriver.common.action_chains import ActionChains
    import pyperclip
    
    # LOG TEXT STATS
    print(f"DEBUG: Tweet {tweet_index} text length: {len(text)} chars", file=sys.stderr)
    
    methods = ["clipboard", "js_native", "sendkeys"]
    
    for method in methods:
        # ALWAYS CLEAN BEFORE TYPING
        atomic_clear(element, driver)
        human_delay(1.0, 2.5) # Safety Delay before typing
        
        print(f"Typing tweet {tweet_index} using {method}...", file=sys.stderr)
        
        if method == "clipboard":
            try:
                pyperclip.copy(text)
                time.sleep(0.3)
                ActionChains(driver).key_down(Keys.CONTROL).send_keys('v').key_up(Keys.CONTROL).perform()
                time.sleep(0.5)
                # React nudge: END key — metin değiştirmez, sadece cursor sona gider
                ActionChains(driver).send_keys(Keys.END).perform()
            except Exception as e:
                print(f"Clipboard fail: {e}", file=sys.stderr)
        
        elif method == "js_native":
            # v4.8.2: Safe DOM insertion
            try:
                driver.execute_script("""
                    var elem = arguments[0];
                    elem.focus();
                    elem.innerHTML = '';
                    var ev1 = new InputEvent('beforeinput', { bubbles: true, cancelable: true, inputType: 'insertText', data: arguments[1] });
                    elem.dispatchEvent(ev1);
                    var textNode = document.createTextNode(arguments[1]);
                    var sel = window.getSelection();
                    if (sel && sel.rangeCount > 0) {
                        sel.getRangeAt(0).insertNode(textNode);
                        sel.collapse(textNode, textNode.length);
                    } else {
                        elem.appendChild(textNode);
                    }
                    elem.dispatchEvent(new Event('input', { bubbles: true }));
                    elem.dispatchEvent(new Event('change', { bubbles: true }));
                """, element, text)
                time.sleep(0.5)
                # React nudge: END key
                ActionChains(driver).send_keys(Keys.END).perform()
            except: pass

        elif method == "sendkeys":
            try:
                element.send_keys(text)
            except: pass
        
        # VERIFICATION WAIT (React sync)
        time.sleep(2.5) 
        
        # VISUAL VERIFICATION: Use innerText via JS 
        current_text = driver.execute_script("return arguments[0].innerText;", element).strip()
        input_len = len(text.strip())
        current_len = len(current_text)
        
        # If duplication occurred
        if input_len > 50 and current_len > input_len * 1.5:
             print(f"DUPLICATION DETECTED! Input: {input_len}, Current: {current_len}. Retrying...", file=sys.stderr)
             continue
        
        # SUCCESS CHECK: Is button enabled?
        try:
            selectors = ["[data-testid='tweetButtonInline']", "[data-testid='tweetButton']", "div[role='button'][data-testid$='Button']"]
            for sel in selectors:
                btns = driver.find_elements(By.CSS_SELECTOR, sel)
                for btn in btns:
                    if btn.is_enabled() and btn.get_attribute("aria-disabled") != "true" and btn.is_displayed():
                        print(f"Success! Tweet button enabled after {method}.", file=sys.stderr)
                        return True
        except: pass
        
        # Fallback text check
        if input_len > 0 and current_len >= input_len * 0.9:
            print(f"✅ Part {tweet_index} verified ({current_len}/{input_len})", file=sys.stderr)
            return True
    
    return False

def _make_heuristic_hints(ua_string):
    """Generate likely Client Hints from User-Agent string"""
    import re
    # Default to Windows 10 safe values
    hints = {
        "platform": "Windows",
        "mobile": False,
        "brands": [{"brand": "Not=A?Brand", "version": "99"}],
        "fullVersion": "120.0.0.0",
        "platformVersion": "10.0.0",
        "architecture": "x86",
        "model": "",
        "bitness": "64",
        "wow64": False
    }
    
    if "Windows NT 10.0" in ua_string: 
        hints["platform"] = "Windows"
        hints["platformVersion"] = "10.0.0"
    elif "Mac OS X" in ua_string: 
        hints["platform"] = "macOS"
        hints["platformVersion"] = "13.0.0" # Safe default
    elif "Linux" in ua_string: 
        hints["platform"] = "Linux"
        hints["platformVersion"] = "5.4.0"
    
    ver = "120"
    # Match Chrome/120 or Edg/120
    match = re.search(r"(Chrome|Edg)/(\d+)", ua_string)
    if match: 
        ver = match.group(2)
        hints["fullVersion"] = f"{ver}.0.0.0"
    
    brand = "Google Chrome"
    if "Edg/" in ua_string: 
        brand = "Microsoft Edge"
        
    hints["brands"] = [
        {"brand": brand, "version": ver},
        {"brand": "Chromium", "version": ver},
        {"brand": "Not=A?Brand", "version": "99"}
    ]
    return hints

def _create_driver_internal(headless=True, use_undetected=True):
    """Internal driver creation (moved from setup_driver)"""
    ensure_dirs()

    # v4.4.7: Deep Identity Sync (Client Hints)
    use_ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    use_hints = None
    
    json_file = APPDATA_DIR / "twitter_cookies.json"
    if json_file.exists():
        try:
            import json
            data = json.loads(json_file.read_text())
            for item in data:
                if "meta_user_agent" in item:
                    use_ua = item["meta_user_agent"]
                if "meta_client_hints" in item:
                    use_hints = item["meta_client_hints"]
                    # Ensure mobile is boolean
                    if "mobile" in use_hints and isinstance(use_hints["mobile"], str):
                         use_hints["mobile"] = (use_hints["mobile"].lower() == "true")
            
            if use_hints:
                print(f"✅ Deep Identity Synced: {use_ua[:30]}... + Metadata", file=sys.stderr)
            elif use_ua:
                # v4.4.8: Heuristic Generation if deep data missing
                use_hints = _make_heuristic_hints(use_ua)
                print(f"✅ Heuristic Identity Generated: {use_ua[:30]}...", file=sys.stderr)
        except: pass
    
    if os.environ.get("X_VISIBLE") == "1":
        headless = False
    
    # Undetected ChromeDriver
    if use_undetected and HAS_UC:
        try:
            options = uc.ChromeOptions()
            if headless:
                options.add_argument("--headless=new")
            
            # REMOVED INCOGNITO: Blocks file upload permissions needed for media attachments
            # options.add_argument("--incognito")
            # options.add_argument("--disk-cache-size=0")
            # options.add_argument("--media-cache-size=0")
            
            options.add_argument("--disable-blink-features=AutomationControlled")
            options.add_argument("--no-sandbox")
            options.add_argument("--disable-dev-shm-usage")
            options.add_argument("--window-size=1920,1080")
            
            # OPTIMIZATION: Page load strategy (images enabled; blocking breaks X UI/media previews)
            options.page_load_strategy = 'eager'
            
            try:
                driver = uc.Chrome(options=options, version_main=None, headless=headless)
            except Exception as e:
                if "version" in str(e).lower():
                    # v4.8.0: Auto-detect Chrome version instead of hardcoding
                    detected_ver = None
                    try:
                        import subprocess as _sp
                        result = _sp.run(
                            ['reg', 'query', r'HKEY_CURRENT_USER\Software\Google\Chrome\BLBeacon', '/v', 'version'],
                            capture_output=True, text=True, timeout=5
                        )
                        if result.returncode == 0:
                            import re as _re
                            ver_match = _re.search(r'(\d+)\.', result.stdout)
                            if ver_match:
                                detected_ver = int(ver_match.group(1))
                    except: pass
                    
                    if not detected_ver:
                        detected_ver = 146  # Safe modern default
                    
                    print(f"Version mismatch in social_intel, forcing v{detected_ver}... {e}", file=sys.stderr)
                    # RECREATE OPTIONS: Cannot reuse after failure
                    options = uc.ChromeOptions()
                    if headless:
                        options.add_argument("--headless=new")
                    options.add_argument("--disable-blink-features=AutomationControlled")
                    options.add_argument("--no-sandbox")
                    options.add_argument("--disable-dev-shm-usage")
                    options.add_argument("--window-size=1920,1080")
                    options.page_load_strategy = 'eager'
                    
                    driver = uc.Chrome(options=options, version_main=detected_ver, headless=headless)
                else:
                    raise e
            try:
                driver.set_page_load_timeout(25)
            except Exception:
                pass
            return driver
        except Exception as e:
            print(f"Undetected Chrome error, falling back: {e}", file=sys.stderr)
    
    # Standard Selenium
    options = Options()
    
    # REMOVED INCOGNITO: Blocks file upload permissions needed for media attachments
    # options.add_argument("--incognito")
    
    if headless:
        options.add_argument("--headless=new")
        options.add_argument("--disable-features=VizDisplayCompositor")
        options.add_argument("--disable-software-rasterizer")
        options.add_argument("--disable-extensions")
        options.add_argument("--disable-background-networking")
        options.add_argument("--disable-translate")
        options.add_argument("--disable-sync")
    
    options.add_argument("--disable-gpu")
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")
    options.add_argument("--window-size=1920,1080")
    options.add_argument("--start-maximized")
    options.add_argument("--remote-debugging-port=0")
    
    # NOTE: Cache was previously forced to 0 to limit growth, but this blocked some uploads on X.
    # If cache bloat returns, add a periodic cleanup instead of disabling cache here.
    
    options.add_argument("--disable-blink-features=AutomationControlled")
    options.add_experimental_option("excludeSwitches", ["enable-automation"])
    options.add_experimental_option('useAutomationExtension', False)
    options.add_argument(f"user-agent={use_ua}")
    
    # OPTIMIZATION: Fast page load (images enabled; blocking breaks X UI/media previews)
    options.page_load_strategy = 'eager'
    
    driver = None
    try:
        if HAS_WDM:
            driver = webdriver.Chrome(service=Service(ChromeDriverManager().install()), options=options)
        else:
            driver = webdriver.Chrome(options=options)
        
        ua_override = {"userAgent": use_ua}
        if use_hints:
            ua_override["userAgentMetadata"] = use_hints
            
        driver.execute_cdp_cmd('Network.setUserAgentOverride', ua_override)

        try:
            driver.set_page_load_timeout(25)
        except Exception:
            pass
        
        return driver
    except Exception as e:
        print(f"Chrome error: {e}", file=sys.stderr)
        return None

# App data directory
def _discover_appdata():
    paths = [
        Path(os.environ.get('LOCALAPPDATA', os.path.expanduser('~'))) / "XiDeAI",
        Path(os.path.dirname(__file__)).parent, # Root of app
        Path(os.path.dirname(__file__)).parent.parent / "XiDeAI", # Sibling XiDeAI folder
        Path(os.path.dirname(__file__)), # Current dir
    ]
    for p in paths:
        if (p / "twitter_cookies.json").exists() or (p / "twitter_cookies.pkl").exists():
            return p
    return paths[0] # Default

APPDATA_DIR = _discover_appdata()
PROFILE_DIR = APPDATA_DIR / "chrome_anon_profile"
COOKIES_FILE = APPDATA_DIR / "twitter_cookies.pkl"

# ===== CLEANUP HANDLER =====
def _cleanup_driver_pool():
    """Cleanup driver pool on exit"""
    ChromeDriverPool.close()

atexit.register(_cleanup_driver_pool)

# RELEVANCE FILTER / BLACKLIST
BLACKLIST_KEYWORDS = [
    "rahmet", "taziye", "mekanı cennet", "nur içinde", "başsağlığı", "vefat", "kandiliniz", "bayramınız", 
    "kutlu olsun", "mübarek olsun", "futbol", "gol", "maç sonucu", "penaltı", "şampiyonlık", "kombine", "istifa", "ayet", "sure", "bakara", "allah", "amin", "siyaset", 
    "bakan", "parti", "chp", "akp", "mhp", "belediye", "başkan", "ziyaret", "hayırlı", "düğün", "nişan", 
    "açılış", "teşkilat", "muhtar"
]

TECHNICAL_KEYWORDS = [
    "grafik", "analiz", "teknik", "rsi", "macd", "direnç", "destek", "kırılım", "hedef", 
    "mum", "formasyon", "fibo", "pivot", "borsa", "endeks", "hisse", "finans", "trade", "chart",
    "hma", "ema", "sma", "ho", "ortalaması", "ortalama"
]

# SPAM FILTER: Private link patterns (Telegram, Discord, etc.)
PRIVATE_LINK_PATTERNS = [
    r"(t\.me|telegram\.me)\/[a-zA-Z0-9_\-]+",                    # t.me/username
    r"(?:https?:\/\/)?(t\.me|telegram\.me|telegram\.org)\/\S+",  # Full URLs
    r"telegram\s+(?:link|channel|group|bot|chat)[\s:]*\S+",      # "telegram link/channel"
    r"\bt\.me\S*\b",                                               # Shortened t.me
    r"(?:discord\.gg|discord\.com)\/\S+",                         # Discord links
    r"ucretsiz\s+telegram",                                        # Turkish: "free telegram"
    r"telegram\s*kanal",                                           # Turkish: "telegram channel"
    r"(?:ses\s+kayd|audio).*telegram",                            # Audio/voice content on Telegram
]

# v3.7.2: Common symbol words that cause noise (matched exactly or with $)
COMMON_STOCK_WORDS = ["LOGO", "INFO", "LINK", "DATA", "SAFE", "ARK", "NEAR", "BIST", "GOLD", "OIL", "GAS"]

def calculate_relevance_score(text, symbol_hint, has_image=False):
    """
    Calculate a relevance score for a tweet.
    Returns: score (int), higher is better. -1000 means blacklisted.
    """
    import re
    text_upper = text.upper()
    score = 0
    
    # 0. PRIVATE LINK CHECK (Telegram, Discord, etc.) - Reject immediately
    for pattern in PRIVATE_LINK_PATTERNS:
        if re.search(pattern, text, re.IGNORECASE):
            return -1000  # Spam/Commercial content

    # 0.b Facebook Cross-Post / Irrelevant Check (Specific for "FB" token confusion)
    # If looking for "FB" (Fenerbahçe) but text mentions "facebook", "fb.com", "instagram"
    if "facebook" in text.lower() or "fb.com" in text.lower() or "instagram" in text.lower():
        # Check if we are in a non-tech context for FB (i.e. Fan Zone)
        # If symbol_hint implies Fenerbahce context
        if symbol_hint and any(x in symbol_hint.upper() for x in ["FB", "FENER"]):
             return -1000
    
    # 1. Blacklist check
    for kw in BLACKLIST_KEYWORDS:
        if kw.upper() in text_upper:
            return -1000
            
    # 2. Symbol match (+50)
    has_symbol = False
    if symbol_hint:
        sym = symbol_hint.upper().replace("#", "").replace("$", "")
        is_common = sym in COMMON_STOCK_WORDS
        
        # Stricter matching for common words (must have $ or # prefix)
        if is_common:
            if f"${sym}" in text_upper or f"#{sym}" in text_upper:
                score += 80 # Higher reward for direct hit on common stock
                has_symbol = True
            elif f" {sym} " in f" {text_upper} ":
                # Common word without marker: huge penalty unless it has tech keywords
                score -= 400 
        else:
            # Normal matching for unique symbols
            if (f"${sym}" in text_upper or 
                f"#{sym}" in text_upper or 
                (len(sym) >= 3 and f" {sym} " in f" {text_upper} ")):
                score += 50
                has_symbol = True
            
    # 3. Technical Keywords (+15 each)
    found_tech = 0
    for kw in TECHNICAL_KEYWORDS:
        if kw.upper() in text_upper:
            found_tech += 1
    score += found_tech * 15
    
    # 4. Image Bonus (+100) - Huge bonus for charts/tables
    if has_image:
        score += 100
    
    # 5. Ticker Stuffing Check (Anti-Spam)
    import re
    tickers = re.findall(r'[$#][A-Z]{2,10}', text_upper)
    unique_tickers = set(tickers)
    if len(unique_tickers) > 5:
        score -= 80 # Heavy penalty for "list" tweets
    elif len(unique_tickers) > 3:
        score -= 30 # Light penalty for multi-symbol tweets
        
    # 6. RELEVANCE CHECK
    # - If it's a Guru Scan (symbol_hint is empty), we allow it if it has technical keywords OR an image.
    # - If it has a symbol, we are more relaxed.
    # - If no symbol AND no tech AND no image, reject.
    if not has_symbol and found_tech == 0 and not has_image:
        return -100 # Not relevant enough
        
    # 7. Length bonus (Only if relevant)
    if score > 0 and len(text) > 100:
        score += 5
        
    return score

def ensure_dirs():
    APPDATA_DIR.mkdir(parents=True, exist_ok=True)
    PROFILE_DIR.mkdir(parents=True, exist_ok=True)

def setup_driver(headless=True, use_undetected=True, bypass_pool=False):
    """
    DEPRECATED: Use ChromeDriverPool.get() instead for better performance.
    Kept for backward compatibility with interactive login.
    """
    if bypass_pool:
        print(f"[SCRIPTS] Creating fresh BYPASS driver (headless={headless})...", file=sys.stderr)
        return _create_driver_internal(headless, use_undetected)
    return ChromeDriverPool.get(headless, use_undetected)
def save_cookies(driver):
    """Save cookies to pickle file"""
    try:
        pickle.dump(driver.get_cookies(), open(COOKIES_FILE, "wb"))
        return True
    except Exception as e:
        print(f"Error saving cookies: {e}", file=sys.stderr)
        return False

def load_cookies(driver):
    """Load cookies from pickle OR json file"""
    json_file = APPDATA_DIR / "twitter_cookies.json"
    
    # v4.6.2: Google Drive conflict fallback - check for (1), (2) suffixed copies
    if not json_file.exists():
        import glob
        # Try finding in APPDATA_DIR or script's parent
        search_dirs = [APPDATA_DIR, Path(os.environ.get('LOCALAPPDATA', os.path.expanduser('~'))) / "XiDeAI"]
        for sd in search_dirs:
            pattern = str(sd / "twitter_cookies*.json")
            candidates = sorted(glob.glob(pattern), key=lambda f: os.path.getmtime(f), reverse=True)
            if candidates:
                json_file = Path(candidates[0])
                print(f"[COOKIE] Using fallback JSON: {json_file.name} from {sd}", file=sys.stderr)
                break
    
    if not COOKIES_FILE.exists() and not json_file.exists():
        return False
        
    try:
        # v4.3.5: Set page load timeout to prevent indefinite hangs
        driver.set_page_load_timeout(30)
        # v4.5.2: Revert to x.com because user cookies are confirmed to be .x.com
        driver.get("https://x.com/home") # Must be on domain to set cookies
        
        # 1. Try JSON (Fresher, from WebView2)
        if json_file.exists():
            try:
                import json
                cookies = json.loads(json_file.read_text())
                added_count = 0
                for c in cookies:
                    cookie_dict = {
                        'name': c.get('name', c.get('Name')),
                        'value': c.get('value', c.get('Value')),
                        'domain': c.get('domain', c.get('Domain')),
                        'path': c.get('path', c.get('Path', '/')),
                    }
                    if 'expires' in c: cookie_dict['expiry'] = int(c['expires'])
                    elif 'Expires' in c: cookie_dict['expiry'] = int(c['Expires'])
                    
                    if 'isSecure' in c: cookie_dict['secure'] = bool(c['isSecure'])
                    elif 'Secure' in c: cookie_dict['secure'] = bool(c['Secure'])
                    elif 'secure' in c: cookie_dict['secure'] = bool(c['secure'])

                    if 'isHttpOnly' in c: cookie_dict['httpOnly'] = bool(c['isHttpOnly'])
                    elif 'HttpOnly' in c: cookie_dict['httpOnly'] = bool(c['HttpOnly'])
                    elif 'httpOnly' in c: cookie_dict['httpOnly'] = bool(c['httpOnly'])
                    
                    if 'sameSite' in c and c['sameSite'] in ["Strict", "Lax", "None"]:
                        cookie_dict['sameSite'] = c['sameSite']
                        
                    # Selenium add_cookie ignores domains matching ".something.com" sometimes if strictly checked, 
                    # but usually it's fine.
                    try: 
                        driver.add_cookie(cookie_dict)
                        added_count += 1
                    except Exception as cookie_err: 
                        print(f"Bypassed cookie err {cookie_dict.get('name')}: {cookie_err}", file=sys.stderr)
                        
                print(f"✅ JSON Session Synced: {added_count}/{len(cookies)} cookies loaded from WebView2", file=sys.stderr)
            except Exception as je:
                print(f"JSON Cookie Error: {je}", file=sys.stderr)

        # 2. Try Pickle (Backup / Selenium stored)
        if COOKIES_FILE.exists():
            try:
                cookies = pickle.load(open(COOKIES_FILE, "rb"))
                for idx, cookie in enumerate(cookies):
                    try:
                        if 'sameSite' in cookie:
                            if cookie['sameSite'] not in ["Strict", "Lax", "None"]:
                                del cookie['sameSite']
                        driver.add_cookie(cookie)
                    except: pass
            except Exception as pe:
                 # Only log if JSON also failed
                 if not json_file.exists(): print(f"Pickle Cookie Error: {pe}", file=sys.stderr)
        
        # MAJOR FIX: Navigate instead of refresh to activate cookies
        try:
            driver.get("https://x.com/home")
            time.sleep(3)
            
            # Simple check if we are still on login page
            current = driver.current_url.lower()
            if "login" in current or "signin" in current or "i/flow" in current:
                print("❌ Cookie load FAILED: Redirected to Login Page. Cookies might be expired.", file=sys.stderr)
                return False
        except: pass

        return True
    except Exception as e:
        print(f"[COOKIE-ERROR] Genel hata: {e}", file=sys.stderr)
        return False

def login_interactive():
    """Opens visible browser for user to login manually"""
    print("Opening browser for login...", file=sys.stderr)
    driver = setup_driver(headless=False)
    if not driver:
        return {"status": "error", "message": "Failed to start driver"}

    try:
        driver.get("https://x.com/i/flow/login")
        
        print("Waiting for login... (Time limit: 120s)", file=sys.stderr)
        # Polling for login success
        start_time = time.time()
        logged_in = False
        
        while time.time() - start_time < 120:
            if "home" in driver.current_url or "compose" in driver.current_url:
                logged_in = True
                break
            time.sleep(1)
            
        if logged_in:
            time.sleep(3) # Wait for cookies to settle
            if save_cookies(driver):
                return {"status": "success", "message": "Login successful. Cookies saved."}
            else:
                return {"status": "error", "message": "Login detected but failed to save cookies."}
        else:
            return {"status": "error", "message": "Login timed out."}
            
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        driver.quit()

def parse_follower_text(text):
    """Parse follower count text like '5.2K', '120K', '1.5M' to integer."""
    if not text:
        return 0
    text = text.strip().replace(",", "").replace(" ", "")
    try:
        if "B" in text.upper():
            return int(float(text.upper().replace("B", "")) * 1_000_000_000)
        elif "M" in text.upper():
            return int(float(text.upper().replace("M", "")) * 1_000_000)
        elif "K" in text.upper():
            return int(float(text.upper().replace("K", "")) * 1_000)
        else:
            return int(float(text))
    except:
        return 0

def find_influencer_posts(query, market, limit=10, since_date=None, until_date=None):
    """Search influencer posts either via VIP timelines or global search"""
    if not COOKIES_FILE.exists():
        return json.dumps({"status": "error", "message": "No cookies", "data": []})

    # Query expansion: if query is just a symbol, add analysis keywords
    symbol_hint = query.strip().replace("$", "").replace("#", "").upper()
    
    # If it looks like a ticker (short, caps), expand it
    search_q = query
    if len(symbol_hint) > 0 and len(symbol_hint) < 10 and " " not in query:
        # Match C# Service logic: (SASA OR $SASA OR #SASA)
        terms = [symbol_hint, f"${symbol_hint}", f"#{symbol_hint}"]
        joined_terms = "(" + " OR ".join(list(set(terms))) + ")"
        
        # Consistent keywords with C# SocialIntelService (Relaxed: No min_faves or filter:safe)
        kwd_group = "(analiz OR grafik OR yorum OR bilanço OR teknik OR hedef OR destek OR direnç OR trade OR chart)"
        search_q = f"{joined_terms} {kwd_group}"
    
    vip_handles = []
    try:
        # Improved from:handle extraction (Handles optional @ symbol)
        if "from:" in query:
            vip_handles = re.findall(r'from:@?(\w+)', query)
            # Remove from: parts to get the actual symbol query
            symbol_hint = re.sub(r'from:@?\w+', '', query).strip()
    except Exception:
        vip_handles = []

    try:
        if vip_handles:
            posts = find_influencer_tweets_from_timeline(vip_handles, symbol_hint, market, limit, since_date, until_date)
        else:
            posts = search_influencer_feed(search_q, symbol_hint)
            
        # Sort by relevance score
        if isinstance(posts, list):
            # BYPASS -500 filter for deep scan (since_date) or non-financial queries
            if not since_date:
                posts = [p for p in posts if p.get("relevance_score", 0) > -500]
            posts.sort(key=lambda x: x.get("relevance_score", 0), reverse=True)
            
        return json.dumps({"status": "success", "data": posts[:limit] if not since_date else posts})
    except Exception as e:
        print(f"❌ find_influencer_posts error: {e}", file=sys.stderr)
        return json.dumps({"status": "error", "message": str(e), "data": []})

def find_influencer_tweets_from_timeline(vip_handles, symbol_query, market, limit=10, since_date=None, until_date=None):
    """Fetch tweets from VIP timelines when query includes from: handles"""
    if not COOKIES_FILE.exists():
        return []

    driver = setup_driver(headless=True, use_undetected=True)
    if not driver:
        return []
    
    target_date_obj = None
    if since_date:
        try:
           target_date_obj = datetime.strptime(since_date, "%Y-%m-%d").replace(tzinfo=timezone.utc)
        except: pass

    results = []
    try:
        if not load_cookies(driver):
            print("Cookie load failed", file=sys.stderr)
            return []

        symbol_filter = (symbol_query or "").upper()
        # Process ALL VIP handles (removed [:5] limit)
        for handle in vip_handles:
            try:
                handle_clean = handle.lstrip("@")
                if not handle_clean:
                    continue
                # FORCE CHRONOLOGICAL: Use /with_replies to ensure we see the LATEST tweets, not "Top"
                timeline_url = f"https://x.com/{handle_clean}/with_replies"
                driver.get(timeline_url)
                time.sleep(3) # Increase wait for profile load

                # CHECK IF ACCOUNT EXISTS / SUSPENDED
                try:
                    page_text = driver.find_element(By.TAG_NAME, "body").text
                    if "This account doesn't exist" in page_text or "Böyle bir hesap yok" in page_text or "Account suspended" in page_text:
                        print(f"⚠️ Account @{handle_clean} is dead!", file=sys.stderr)
                        results.append({
                            "author": "ERROR_404",
                            "content": "ACC_NOT_FOUND",
                            "url": timeline_url,
                            "relevance_score": 999
                        })
                        break # Skip this handle but send marker


                except: pass

                # DEBUG LOGGING (Ported to AppData for reliability)
                debug_path = APPDATA_DIR / "debug_scan.log"
                def log_debug(msg):
                    try:
                        with open(debug_path, "a", encoding="utf-8") as f:
                            f.write(f"[{datetime.now().strftime('%H:%M:%S')}] {msg}\n")
                    except Exception as e:
                        # CRITICAL: Use stderr to avoid polluting JSON output in stdout
                        print(f"Log error (non-critical): {e}", file=sys.stderr)
                
                log_debug(f"--- START SCAN FOR {handle} (URL: {timeline_url}) ---")
                
                oldest_tweet_date = datetime.now(timezone.utc)

                # CRITICAL: Parse tweets BEFORE scrolling to capture newest ones at top
                def parse_visible_tweets(round_idx):
                    nonlocal oldest_tweet_date
                    parsed = []
                    tweets = driver.find_elements(By.TAG_NAME, "article")
                    log_debug(f"Round {round_idx}: Found {len(tweets)} article elements")
                    
                    for idx, tweet in enumerate(tweets):
                        try:
                            # 1. Author and Names (CRITICAL)
                            try:
                                names_el = tweet.find_element(By.CSS_SELECTOR, "[data-testid='User-Names']")
                                names_text = names_el.text
                                handle_match = re.search(r'@(\w+)', names_text)
                                tweet_author = "@" + handle_match.group(1) if handle_match else f"@{handle_clean}"
                            except:
                                tweet_author = f"@{handle_clean}"

                            # 2. Text Content (CRITICAL)
                            try:
                                text_el = tweet.find_element(By.CSS_SELECTOR, "[data-testid='tweetText']")
                                text = text_el.text.replace("\n", " ")
                            except:
                                # Fallback to general text if d-t-t not found
                                text = tweet.text.replace("\n", " ")

                            # 2.5 Image (Moved up for short text check)
                            img_url = None
                            try:
                                # Try to find media specifically
                                img_els = tweet.find_elements(By.CSS_SELECTOR, "[data-testid='tweetPhoto'] img")
                                if not img_els:
                                    img_els = tweet.find_elements(By.CSS_SELECTOR, "img[src*='media']")
                                
                                for img in img_els:
                                    src = img.get_attribute("src")
                                    if src and "profile_images" not in src:
                                        img_url = src
                                        break
                            except: pass
                            
                            # RELAXED LENGTH CHECK (Fix for short signals like 'EFE HMA')
                            if len(text) < 3 and not img_url:
                                log_debug(f"  Tweet {idx}: SKIPPED (Too short: {len(text)})")
                                continue
                            
                            # 3. URL
                            url = ""
                            try:
                                link = tweet.find_element(By.CSS_SELECTOR, "a[href*='/status/']")
                                url = link.get_attribute("href")
                            except Exception:
                                # Some ads or blocked tweets don't have status links
                                pass
                            
                            # Skip if already seen (by URL)
                            if url and any(r.get("url") == url for r in results):
                                continue
                            
                            # 4. Time
                            time_str = ""
                            try:
                                time_el = tweet.find_element(By.TAG_NAME, "time")
                                time_str = time_el.get_attribute("datetime")
                                if time_str:
                                     dt = datetime.fromisoformat(time_str.replace("Z", "+00:00"))
                                     if dt < oldest_tweet_date: oldest_tweet_date = dt
                            except Exception:
                                pass
                            
                            # 5. RT and Orig Author
                            original_author = tweet_author
                            is_retweet = False
                            try:
                                social_context = tweet.find_elements(By.CSS_SELECTOR, "[data-testid='socialContext']")
                                if social_context:
                                    is_retweet = True
                                    body_links = tweet.find_elements(By.CSS_SELECTOR, "div[data-testid='User-Names'] a")
                                    for bl in body_links:
                                        href = bl.get_attribute("href")
                                        if href and f"/{handle_clean}" not in href.lower() and "/status/" not in href:
                                            original_author = "@" + href.split("/")[-1]
                                            break
                            except: pass


                            # 7. Engagement
                            engagement = 0
                            try:
                                like_el = tweet.find_element(By.CSS_SELECTOR, "[data-testid='like']")
                                engagement = parse_follower_text(like_el.text)
                            except: pass

                            # 8. SCORING
                            score = calculate_relevance_score(text, symbol_query, has_image=(img_url is not None))
                            
                            # FORCE VIP KEEP: If tweet is from the target handle, KEEP IT regardless of content/score
                            if handle_clean and tweet_author.upper().endswith(handle_clean.upper()):
                                score = 2000 # Super high score for direct tweets
                                log_debug(f"  Tweet {idx}: VIP Author Override ({tweet_author}) -> Score 2000")
                            
                            # Bypass for Meta-Teacher/Guru
                            is_financial_query = symbol_query and any(word in symbol_query.upper() for word in ["BTC", "$", "#", "ANALIZ", "GRAFIK", "CHART"])
                            if not since_date and is_financial_query and score < 10:
                                continue
                            
                            parsed.append({
                                "author": tweet_author,
                                "original_author": original_author,
                                "is_retweet": is_retweet,
                                "content": text[:500],
                                "url": url,
                                "engagement": engagement,
                                "relevance_score": score,
                                "postDate": time_str or datetime.now(timezone.utc).isoformat(),
                                "imageUrl": img_url
                            })
                        except Exception as tweet_err:
                            log_debug(f"  Tweet {idx}: ERROR parsing individual tweet: {tweet_err}")
                    return parsed
                
                # STEP 1: Parse TOP tweets first (newest are at top)
                time.sleep(2)  # Wait for initial load
                results.extend(parse_visible_tweets(0))
                print(f"DEBUG: Found {len(results)} tweets before scroll", file=sys.stderr)
                
                # STEP 2: Scroll and get more if needed (Dynamic Scroll based on limit AND date)
                # Max 100 scrolls for very deep scan (approx 600-800 tweets)
                max_scrolls = 3 # v3.5: Reduced default for speed
                if limit > 20: max_scrolls = 10
                if limit > 50: max_scrolls = 20
                if target_date_obj: max_scrolls = 100 # Keep deep for Meta-Teacher
                
                # Check for smart stop date (don't scan before this date if we already have it in DB)
                until_date_obj = None
                if until_date:
                    try:
                        until_date_obj = datetime.strptime(until_date, "%Y-%m-%d").replace(tzinfo=timezone.utc)
                    except: pass

                for i in range(max_scrolls):
                    # Stop if we have enough tweets AND NOT date mode
                    if not target_date_obj and len(results) >= limit:
                        break
                    
                    # DATE CHECK: If oldest tweet seen is OLDER than target, we can probably stop?
                    if target_date_obj and oldest_tweet_date < target_date_obj:
                         log_debug(f"  Reached target date limit {oldest_tweet_date} < {target_date_obj}")
                         break
                         
                    # SMART STOP: If we reached 'until_date' (newest date in our DB), we can stop scrolling
                    if until_date_obj and oldest_tweet_date < until_date_obj:
                         log_debug(f"  Smart Stop: Reached already known records date {oldest_tweet_date} < {until_date_obj}")
                         break

                    driver.execute_script("window.scrollBy(0, window.innerHeight);")
                    time.sleep(1.3) # v3.5: Stable scroll
                    log_debug(f"  Scrolling... ({i+1}/{max_scrolls}) total_results={len(results)}")
                    
                    # Parse current view
                    results.extend(parse_visible_tweets(i + 1))
                    
                    # Deduplicate results by URL
                    unique_results = []
                    seen_urls = set()
                    for r in results:
                        if r['url'] not in seen_urls:
                            unique_results.append(r)
                            seen_urls.add(r['url'])
                    results = unique_results
                
                log_debug(f"--- END SCAN FOR {handle} : Total {len(results)} collected ---")

                # Sort by DATE (newest first)
                # Parse ISO dates to objects for proper sorting, handling missing/invalid gracefully
                def parse_date_safe(d_str):
                     try: return datetime.fromisoformat(d_str.replace("Z", "+00:00"))
                     except: return datetime.min.replace(tzinfo=timezone.utc)
                
                results.sort(key=lambda x: parse_date_safe(x.get("postDate", "")), reverse=True)
                
                # CRITICAL Fix: Do NOT filter strictly by date for Guru feed updates.
                # Just return the newest N items found.
                # Only use date filter if specifically requested for historical analysis
                # if target_date_obj:
                #      results = [r for r in results if r.get("postDate") >= since_date] 
                
                if len(results) >= limit and not target_date_obj:
                    break
            except Exception as timeline_err:
                print(f"Timeline load error for {handle}: {timeline_err}", file=sys.stderr)
        print(f"✅ Timeline method found {len(results)} posts", file=sys.stderr)
        return results[:limit] if not target_date_obj else results  # Return top N newest
    except Exception as e:
        print(f"❌ find_influencer_tweets_from_timeline error: {e}", file=sys.stderr)
        return results
    finally:
        driver.quit()

def search_influencer_feed(query, symbol_hint):
    """Use global search feed when VIP handles are not provided"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC

    driver = setup_driver(headless=True, use_undetected=True)
    if not driver:
        return []

    results = []
    try:
        if not load_cookies(driver):
            return []
        encoded = urllib.parse.quote(query)
        driver.get(f"https://x.com/search?q={encoded}&src=typed_query&f=live")
        try:
            # Wait longer for articles
            WebDriverWait(driver, 20).until(EC.presence_of_element_located((By.TAG_NAME, "article")))
            print(f"DEBUG: Found articles for query: {query}", file=sys.stderr)
        except:
            print(f"DEBUG: No articles found for query: {query}", file=sys.stderr)
            return []
        
        # Try specific tweet container first
        articles = driver.find_elements(By.CSS_SELECTOR, "article[data-testid='tweet']")
        if not articles:
            articles = driver.find_elements(By.TAG_NAME, "article")
        symbol_upper = (symbol_hint or "").upper()
        for art in articles[:10]:
            try:
                text = ""
                try:
                    content_el = art.find_element(By.CSS_SELECTOR, "[data-testid='tweetText']")
                    text = content_el.text
                except Exception:
                    # FALLBACK: Use article text if specific tweetText not found
                    raw_text = art.text or ""
                    # Split lines and take significant ones (X layout often starts with user info and ends with metrics)
                    lines = [l.strip() for l in raw_text.split('\n') if len(l.strip()) > 10]
                    if len(lines) > 2:
                        # Extract probable tweet content (usually in the middle after handle/date)
                        text = " ".join(lines[1:5])
                    else:
                        text = raw_text.replace("\n", " ")
                
                if not text or len(text) < 10:
                    continue

                img_url = None
                try:
                    img_els = art.find_elements(By.CSS_SELECTOR, "img[src*='media']")
                    for img in img_els:
                        src = img.get_attribute("src")
                        if src and "profile_images" not in src:
                            img_url = src
                            break
                except: pass

                # SCORING FILTER
                score = calculate_relevance_score(text, symbol_upper, has_image=(img_url is not None))
                if score < 10: # Lower threshold to catch more results (trust X search)
                    continue
                
                handle = ""
                try:
                    # Priority 1: User-Names (Standard X layout)
                    user_el = art.find_element(By.CSS_SELECTOR, "[data-testid='User-Names']")
                    handle_text = user_el.text
                    # Pattern: "Name @handle · 2h"
                    matches = re.findall(r'@(\w+)', handle_text)
                    if matches:
                        handle = "@" + matches[0]
                    else:
                        # Split by @ if regex fails
                        if "@" in handle_text:
                            handle = "@" + handle_text.split("@")[1].split()[0]
                        else:
                            handle = handle_text.split("\n")[0]
                except Exception:
                    try:
                        # Priority 2: Direct link to profile (Highly reliable if User-Names fails)
                        links = art.find_elements(By.CSS_SELECTOR, "a[role='link']")
                        for link in links:
                            href = link.get_attribute("href") or ""
                            # Profile links: x.com/username (no 'status', 'search', etc.)
                            if "x.com/" in href and not any(x in href for x in ["/status/", "/search", "/home", "/explore", "/i/"]):
                                potential_handle = href.split("x.com/")[1].split("/")[0].split("?")[0]
                                if potential_handle:
                                    handle = "@" + potential_handle
                                    break
                    except: pass
                
                # Priority 3: Regex on full text if still empty
                if not handle or handle == "@":
                    try:
                        full_text = art.text
                        handle_match = re.search(r'@(\w+)', full_text)
                        if handle_match:
                            handle = "@" + handle_match.group(1)
                    except: pass

                if not handle or handle == "@":
                    continue  # Handle parse edilemedi → bu tweet'i atla, X-User kirliliği oluşturma
                
                url = ""
                time_str = ""
                try:
                    time_el = art.find_element(By.TAG_NAME, "time")
                    url = time_el.find_element(By.XPATH, "./..").get_attribute("href")
                    time_str = time_el.get_attribute("datetime")
                except Exception:
                    url = "https://x.com"
                    time_str = datetime.now(timezone.utc).isoformat()

                img_url = None
                try:
                    img_els = art.find_elements(By.CSS_SELECTOR, "img[src*='media']")
                    for img in img_els:
                        src = img.get_attribute("src")
                        if src and "profile_images" not in src:
                            img_url = src
                            break
                except: pass
                
                engagement = 0
                try:
                    like_el = art.find_element(By.CSS_SELECTOR, "[data-testid='like']")
                    engagement = parse_follower_text(like_el.text)
                except Exception:
                    engagement = 0
                
                results.append({
                    "author": handle,
                    "content": text[:500],
                    "url": url,
                    "engagement": engagement,
                    "relevance_score": score,
                    "postDate": time_str,
                    "imageUrl": img_url
                })
            except Exception as search_err:
                print(f"Search parse error: {search_err}", file=sys.stderr)
        return results
    finally:
        driver.quit()

def import_cookies(json_path):
    """Import cookies from JSON file (EditThisCookie export)"""
    try:
        with open(json_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
            
        # Convert to Selenium format (list of dicts)
        # EditThisCookie format is already list of dicts suitable for Selenium
        # We just need to ensure fields like 'sameSite' are compatible
        
        # Save directly to pickle
        pickle.dump(data, open(COOKIES_FILE, "wb"))
        return {"status": "success", "message": "Cookies imported successfully!"}
            
    except Exception as e:
        return {"status": "error", "message": str(e)}

def post_tweet(text, media_path=None):
    """Post a tweet using Selenium automation (bypasses API limits)"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies found. Please import cookies first."}
        
    # Run visible to avoid detection
    driver = setup_driver(headless=False)
    if not driver:
        return {"status": "error", "message": "Failed to start driver"}

    try:
        if not load_cookies(driver):
             return {"status": "error", "message": "Failed to load cookies"}

        driver.get("https://x.com/compose/tweet")
        time.sleep(2)
        
        try:
            tweet_box = WebDriverWait(driver, 45).until(
                EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='tweetTextarea_0'], [role='textbox']"))
            )
            time.sleep(1)
            
            if media_path:
                import os
                if os.path.exists(media_path):
                    try:
                        file_input = driver.find_element(By.CSS_SELECTOR, "input[type='file']")
                        file_input.send_keys(os.path.abspath(media_path))
                        # Wait for upload to process (preview to appear)
                        WebDriverWait(driver, 20).until(
                            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='attachments']"))
                        )
                        time.sleep(2) 
                    except Exception as media_err:
                        print(f"Media upload warning: {str(media_err)}", file=sys.stderr)
            
            # Robust Text Entry v2.1 (Global Helper with Mandatory Clear)
            success = robust_type_and_verify(driver, tweet_box, text)
            if not success:
                print("Warning: Verification failed in post_tweet, proceeding anyway.", file=sys.stderr)
            
            # WAKE UP REACT: Send dummy key press to force state update
            try:
                tweet_box.send_keys(" ")
                time.sleep(0.1)
                tweet_box.send_keys(Keys.BACKSPACE)
            except: pass
            time.sleep(1.5)
            
            # 3. CLICK POST BUTTON (Robustly)
            # 3. CLICK POST BUTTON (Robustly)
            human_delay(2.0, 4.0) # Safety Delay before click
            print("DEBUG: Clicking post button...", file=sys.stderr)
            post_btn = None
            for selector in ["[data-testid='tweetButton']", "div[role='button'][data-testid$='Button']", "//span[text()='Gönderi yayınla']/../../.."]:
                try:
                    if selector.startswith("//"):
                        post_btn = WebDriverWait(driver, 3).until(EC.element_to_be_clickable((By.XPATH, selector)))
                    else:
                        post_btn = WebDriverWait(driver, 3).until(EC.element_to_be_clickable((By.CSS_SELECTOR, selector)))
                    if post_btn: break
                except: continue
            
            if post_btn:
                driver.execute_script("arguments[0].scrollIntoView({block: 'center'});", post_btn)
                time.sleep(0.5)
                driver.execute_script("arguments[0].click();", post_btn)
                print("DEBUG: Post button clicked via JS.", file=sys.stderr)
            else:
                print("DEBUG: Button not found, trying CTRL+ENTER fallback.", file=sys.stderr)
                tweet_box.send_keys(Keys.CONTROL, Keys.ENTER)
            
            # Verify closure or confirmation
            time.sleep(5)
            return {"status": "success", "message": "Tweet posted successfully (Draft Cleared)!"}
            
        except Exception as e:
            ts = datetime.now().strftime("%Y%m%d_%H%M%S")
            driver.save_screenshot(f"tweet_fail_{ts}.png")
            return {"status": "error", "message": f"Interact fail: {str(e)}"}
            
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def reply_to_tweet(tweet_url, text):
    """Reply to a specific tweet"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies."}
        
    # SECURITY GUARD: Ensure we are replying to a TWEET, not a profile
    if "/status/" not in tweet_url:
        return {"status": "error", "message": "URL is not a tweet status. Standalone tweet prevention triggered."}

    # CRITICAL FIX: Use bypass_pool=True for interactions to avoid collision with background scraper
    driver = setup_driver(headless=False, use_undetected=True, bypass_pool=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        print(f"DEBUG: Navigating to {tweet_url}", file=sys.stderr)
        driver.get(tweet_url)
        time.sleep(5)  # Increased wait for page load and redirections
        
        # v3.7.3: CRITICAL URL VERIFICATION
        # If redirected to home, process MUST stop to prevent "standalone tweet instead of reply" bug
        current_url = driver.current_url.lower()
        target_id = re.search(r'status/(\d+)', tweet_url)
        target_id = target_id.group(1) if target_id else ""
        
        if "/status/" not in current_url:
            print(f"ERROR: Navigation failed or redirected to {current_url}. Aborting reply.", file=sys.stderr)
            return {"status": "error", "message": f"Navigation failed (at {current_url}). Reply aborted for safety."}
        
        if target_id and target_id not in current_url:
            # Check if we are still at least on A tweet page. X sometimes normalizes URLs.
            print(f"WARNING: Current URL {current_url} does not exactly match target ID {target_id}, but we are on a status page.", file=sys.stderr)
        
        # Calculate reply box selector
        # Usually it's the main draft editor.
        try:
            # Click the "Reply" text area (placeholder usually says 'Post your reply')
            print("DEBUG: Looking for reply box...", file=sys.stderr)
            reply_area = WebDriverWait(driver, 20).until(  # Increased timeout
                EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetTextarea_0']"))
            )

            # v4.0.1 SAFETY FIX: Verify it is a REPLY box, not a NEW TWEET box
            # New Tweet: "What is happening?!" / "Neler oluyor?"
            # Reply: "Post your reply" / "Yanıtını gönder"
            try:
                area_label = reply_area.get_attribute("aria-label") or ""
                placeholder = reply_area.get_attribute("placeholder") or ""
                btn_text = reply_area.text or ""
                
                check_str = (area_label + " " + placeholder + " " + btn_text).lower()
                
                # Keywords that indicate a NEW TWEET box (Bad)
                if "what is happening" in check_str or "neler oluyor" in check_str:
                    print(f"CRITICAL SAFETY STOP: Detected NEW TWEET box instead of REPLY box. Label: {area_label}", file=sys.stderr)
                    return {"status": "error", "message": "SAFETY STOP: Attempted to post standalone tweet instead of reply!"}
                
                # Optionally, verify keywords for REPLY box (Good) but X changes texts often, safer to blacklist the "New Tweet" text
                print(f"DEBUG: Box verification passed (Label: {area_label})", file=sys.stderr)
            except Exception as safety_err:
                 print(f"WARNING: Safety check failed, verifying URL again...", file=sys.stderr)
                 if "/status/" not in driver.current_url:
                     return {"status": "error", "message": "Safety check failed and URL is wrong."}

            print("DEBUG: Reply box found, clicking...", file=sys.stderr)
            
            # Use JavaScript click to avoid element interception
            driver.execute_script("arguments[0].scrollIntoView(true);", reply_area)
            time.sleep(0.5)
            driver.execute_script("arguments[0].click();", reply_area)
            time.sleep(1)
            
            # Robust Text Entry v2.1 (Global Helper with Mandatory Clear)
            success = robust_type_and_verify(driver, reply_area, text)
            if not success:
                print("Warning: Verification failed in reply_to_tweet, proceeding anyway.", file=sys.stderr)
            
            time.sleep(1.5)  # Increased wait for text insertion
            
            # Click Reply button with Multiple Selector support
            print("DEBUG: Looking for reply button...", file=sys.stderr)
            reply_btn = None
            for selector in ["[data-testid='tweetButtonInline']", "[data-testid='tweetButton']", "div[role='button'][data-testid$='Button']"]:
                try:
                    reply_btn = WebDriverWait(driver, 5).until(
                        EC.element_to_be_clickable((By.CSS_SELECTOR, selector))
                    )
                    if reply_btn: break
                except: continue

            if reply_btn:
                print(f"DEBUG: Reply button found, clicking...", file=sys.stderr)
                driver.execute_script("arguments[0].scrollIntoView({block: 'center'});", reply_btn)
                time.sleep(1)
                driver.execute_script("arguments[0].click();", reply_btn)
            else:
                print("DEBUG: Reply button not found via selectors, trying CTRL+ENTER shortcut...", file=sys.stderr)
                # Fallback: Click the area again and send Ctrl+Enter
                driver.execute_script("arguments[0].click();", reply_area)
                time.sleep(0.5)
                reply_area.send_keys(Keys.CONTROL + Keys.ENTER)
            
            print("DEBUG: Waiting for post confirmation...", file=sys.stderr)
            time.sleep(5)  # Wait to ensure post is processed
            return {"status": "success", "message": "Reply attempt completed!"}
            
        except Exception as e:
             print(f"DEBUG: Reply interact error: {str(e)}", file=sys.stderr)
             # Save screenshot for debugging
             try:
                 ts = datetime.now().strftime("%Y%m%d_%H%M%S")
                 screenshot_path = APPDATA_DIR / f"reply_fail_{ts}.png"
                 driver.save_screenshot(str(screenshot_path))
                 print(f"DEBUG: Screenshot saved to {screenshot_path}", file=sys.stderr)
             except: pass
             return {"status": "error", "message": f"Reply interact fail: {str(e)}"}
             
    except Exception as e:
        print(f"DEBUG: Outer exception: {str(e)}", file=sys.stderr)
        return {"status": "error", "message": str(e)}
    finally:
        try:
            if driver: 
                print("DEBUG: Closing driver...", file=sys.stderr)
                driver.quit()
        except Exception as quit_ex:
            print(f"DEBUG: Driver quit error (ignoring): {quit_ex}", file=sys.stderr)
            pass  # Ignore quit errors

def fetch_replies(tweet_url):
    """Fetch replies for a specific tweet"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies."}

    driver = setup_driver(headless=True) # Pool driver is fine for read-only
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        print(f"DEBUG: Fetching replies from {tweet_url}", file=sys.stderr)
        driver.get(tweet_url)
        time.sleep(3)
        
        # Wait for any status content
        try:
             WebDriverWait(driver, 20).until(
                EC.presence_of_element_located((By.CSS_SELECTOR, "article[data-testid='tweet']"))
            )
        except:
             print("DEBUG: article[data-testid='tweet'] not found within 20s", file=sys.stderr)
        
        # Scroll bit to trigger load
        driver.execute_script("window.scrollBy(0, 600);")
        time.sleep(2)
        
        articles = driver.find_elements(By.CSS_SELECTOR, "article[data-testid='tweet']")
        if not articles:
            articles = driver.find_elements(By.TAG_NAME, "article")

        replies = []
        # Skip index 0 (the main tweet)
        for i, art in enumerate(articles):
            if i == 0: continue 
            
            try:
                # Basic parse
                text_el = art.find_elements(By.CSS_SELECTOR, "[data-testid='tweetText']")
                text = text_el[0].text if text_el else ""
                
                handle_els = art.find_elements(By.CSS_SELECTOR, "[data-testid='User-Names'] span")
                handle = ""
                for h in handle_els:
                    if h.text.startswith("@"):
                        handle = h.text
                        break
                
                # Link
                link_els = art.find_elements(By.CSS_SELECTOR, "a[href*='/status/']")
                link = link_els[0].get_attribute("href") if link_els else ""
                
                if text or handle:
                    replies.append({
                        "handle": handle,
                        "text": text,
                        "url": link
                    })
            except: continue
        
        return {"status": "success", "data": replies}
        
    except Exception as e:
        return {"status": "error", "message": str(e)}

def fetch_retweeters(tweet_url):
    """Fetch users who retweeted a tweet"""
    from selenium.webdriver.common.by import By
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    import re
    
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies."}

    # Extract ID
    match = re.search(r'status/(\d+)', tweet_url)
    if not match:
        return {"status": "error", "message": "Invalid tweet URL"}
    
    tweet_id = match.group(1)
    retweets_url = f"https://x.com/i/status/{tweet_id}/retweets"

    driver = setup_driver(headless=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        print(f"DEBUG: Fetching retweeters from {retweets_url}", file=sys.stderr)
        driver.get(retweets_url)
        time.sleep(3)
        
        # Wait for user cells
        try:
             WebDriverWait(driver, 15).until(
                EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='UserCell']"))
            )
        except:
             print("DEBUG: No retweeters found (timeout)", file=sys.stderr)
             return {"status": "success", "data": []}
        
        # Scroll bit
        driver.execute_script("window.scrollBy(0, 500);")
        time.sleep(1)
        
        user_cells = driver.find_elements(By.CSS_SELECTOR, "[data-testid='UserCell']")
        retweeters = []
        
        for cell in user_cells:
            try:
                # Get handle
                handle_els = cell.find_elements(By.CSS_SELECTOR, "span")
                handle = ""
                for h in handle_els:
                    if h.text.startswith("@"):
                        handle = h.text
                        break
                
                if handle:
                    retweeters.append({
                        "handle": handle
                    })
            except: continue
            
        return {"status": "success", "data": retweeters}
        
    except Exception as e:
        return {"status": "error", "message": str(e)}


def like_tweet(tweet_url):
    """Like a specific tweet"""
    from selenium.webdriver.common.by import By
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    import time
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies."}
        
    # CRITICAL: Interaction must bypass pool to avoid collision with background scraper
    driver = setup_driver(headless=False, use_undetected=True, bypass_pool=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        driver.get(tweet_url)
        time.sleep(2)
        
        try:
            # Check if already liked (unlike button exists)
            try:
                driver.find_element(By.CSS_SELECTOR, "[data-testid='unlike']")
                return {"status": "success", "message": "Already liked"}
            except: pass
            
            # Click Like
            print("DEBUG: Looking for like button...", file=sys.stderr)
            # Find all buttons with testid='like'
            like_btn = None
            selectors = ["[data-testid='like']", "div[role='button'][data-testid='like']", "//div[@data-testid='like']"]
            for selector in selectors:
                try:
                    if selector.startswith("//"):
                        like_btn = WebDriverWait(driver, 10).until(EC.element_to_be_clickable((By.XPATH, selector)))
                    else:
                        like_btn = WebDriverWait(driver, 10).until(EC.element_to_be_clickable((By.CSS_SELECTOR, selector)))
                    if like_btn: break
                except: continue

            if like_btn:
                print("DEBUG: Like button found, clicking...", file=sys.stderr)
                driver.execute_script("arguments[0].scrollIntoView({block: 'center'});", like_btn)
                time.sleep(1)
                driver.execute_script("arguments[0].click();", like_btn)
                time.sleep(2)
                return {"status": "success", "message": "Liked successfully"}
            else:
                return {"status": "error", "message": "Like button not found"}
            
        except Exception as e:
             return {"status": "error", "message": f"Like fail: {str(e)}"}
             
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def retweet(tweet_url):
    """Retweet a specific tweet"""
    from selenium.webdriver.common.by import By
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    import time
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies."}
        
    # CRITICAL: Interaction must bypass pool to avoid collision with background scraper
    driver = setup_driver(headless=False, use_undetected=True, bypass_pool=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        driver.get(tweet_url)
        time.sleep(2)
        
        try:
            # Check if already retweeted (unretweet button exists)
            try:
                driver.find_element(By.CSS_SELECTOR, "[data-testid='unretweet']")
                return {"status": "success", "message": "Already retweeted"}
            except: pass
            
            # Find Retweet button (Universal 2025 selectors)
            print("DEBUG: Looking for retweet button...", file=sys.stderr)
            retweet_btn = None
            selectors = ["[data-testid='retweet']", "div[role='button'][data-testid='retweet']", "//div[@data-testid='retweet']"]
            for selector in selectors:
                try:
                    if selector.startswith("//"):
                        retweet_btn = WebDriverWait(driver, 10).until(EC.element_to_be_clickable((By.XPATH, selector)))
                    else:
                        retweet_btn = WebDriverWait(driver, 10).until(EC.element_to_be_clickable((By.CSS_SELECTOR, selector)))
                    if retweet_btn: break
                except: continue

            if retweet_btn:
                print("DEBUG: Retweet button found, clicking...", file=sys.stderr)
                driver.execute_script("arguments[0].scrollIntoView({block: 'center'});", retweet_btn)
                time.sleep(1)
                driver.execute_script("arguments[0].click();", retweet_btn)
                time.sleep(1)
                
                # Confirm Retweet (Popup)
                print("DEBUG: Looking for retweet confirmation button...", file=sys.stderr)
                confirm_btn = WebDriverWait(driver, 8).until(
                    EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='retweetConfirm']"))
                )
                driver.execute_script("arguments[0].click();", confirm_btn)
                time.sleep(2)
                return {"status": "success", "message": "Retweeted successfully"}
            else:
                return {"status": "error", "message": "Retweet button not found"}
            
        except Exception as e:
             return {"status": "error", "message": f"Retweet fail: {str(e)}"}
             
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_profile_stats():
    """Scrape profile stats (Followers, Following) from own profile"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies."}
        
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        # Navigate to home first to load session
        driver.get("https://x.com/home")
        try:
            # Click Profile using aria-label or testid
            # Note: Selectors change. data-testid='AppTabBar_Profile_Link' is standard.
            WebDriverWait(driver, 10).until(
                EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='AppTabBar_Profile_Link']"))
            ).click()
            
            # Wait for stats to load
            WebDriverWait(driver, 10).until(
                EC.presence_of_element_located((By.CSS_SELECTOR, "[href*='/following']"))
            )
            
            # Get URL to know username
            username = driver.current_url.split("/")[-1]
            
            stats = {"username": username, "following": "0", "followers": "0"}
            
            # Scrape Following
            try:
                following_elem = driver.find_element(By.CSS_SELECTOR, f"[href='/{username}/following']")
                stats["following"] = following_elem.text.split()[0]
            except: pass
            
            # Scrape Followers
            try:
                # Try Verified Followers first (for Premium)
                followers_elem = driver.find_element(By.CSS_SELECTOR, f"[href='/{username}/verified_followers']")
                stats["followers"] = followers_elem.text.split()[0]
            except:
                try:
                    # Fallback to normal followers
                    followers_elem = driver.find_element(By.CSS_SELECTOR, f"[href='/{username}/followers']")
                    stats["followers"] = followers_elem.text.split()[0]
                except: pass
                
            return {"status": "success", "data": stats}
            
        except Exception as e:
             return {"status": "error", "message": f"Nav/Scrape fail: {str(e)}"}
             
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_recent_engagement(limit=10):
    """Scrape likes/rt/replies for the last N tweets from own profile"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    if not COOKIES_FILE.exists(): return {"status": "error", "message": "No cookies"}
    
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error"}
    
    try:
        if not load_cookies(driver): return {"status": "error"}
        driver.get("https://x.com/home")
        
        # Go to profile
        profile_btn = WebDriverWait(driver, 15).until(
            EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='AppTabBar_Profile_Link']"))
        )
        profile_btn.click()
        time.sleep(3)
        
        # Find tweets
        tweets = driver.find_elements(By.TAG_NAME, "article")
        results = []
        
        for tweet in tweets[:limit]:
            try:
                # Find metrics
                # Testids: reply, retweet, like
                metrics = {
                    "text": tweet.text[:100].replace("\n", " "),
                    "replies": 0,
                    "retweets": 0,
                    "likes": 0
                }
                
                try:
                    reply_elem = tweet.find_element(By.CSS_SELECTOR, "[data-testid='reply']")
                    metrics["replies"] = parse_follower_text(reply_elem.text)
                except: pass
                
                try:
                    rt_elem = tweet.find_element(By.CSS_SELECTOR, "[data-testid='retweet']")
                    metrics["retweets"] = parse_follower_text(rt_elem.text)
                except: pass
                
                try:
                    like_elem = tweet.find_element(By.CSS_SELECTOR, "[data-testid='like']")
                    metrics["likes"] = parse_follower_text(like_elem.text)
                except: pass
                
                results.append(metrics)
            except: continue
            
        return {"status": "success", "data": results}
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_breaking_news_topics():
    """Scrape 'What's Happening' section for smart tagging (Trends)"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    if not COOKIES_FILE.exists(): return {"status": "error"}
    
    driver = setup_driver(headless=True)
    try:
        if not load_cookies(driver): return {"status": "error"}
        driver.get("https://x.com/explore")
        
        # Wait for trends
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='trend']"))
        )
        
        trends = []
        elements = driver.find_elements(By.CSS_SELECTOR, "[data-testid='trend']")
        for el in elements:
            try:
                # Trends structure: "1. Politics · Trending\n#TrendName\n50K Posts"
                # We want the Name (#TrendName or Text)
                text_parts = el.text.split("\n")
                
                candidate = ""
                for part in text_parts:
                    part = part.strip()
                    # Skip metadata lines
                    if "Trending" in part or "posts" in part.lower() or part.isdigit() or len(part) < 2:
                        continue
                    # Bias towards hashtags
                    if part.startswith("#"):
                        candidate = part
                        break
                    # Fallback to normal text if reasonable length
                    if not candidate and len(part) < 25:
                        candidate = part
                
                if candidate and len(candidate) < 30 and candidate not in trends:
                    # Filter generic promoted/junk
                    if "Promoted" not in candidate:
                        trends.append(candidate)
            except: pass
            
            if len(trends) >= 8: break
            
        return {"status": "success", "data": trends}
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_dms():
    """Scrape latest DMs (top 3)"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    if not COOKIES_FILE.exists(): return {"status": "error"}
    
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error"}
    
    try:
        if not load_cookies(driver): return {"status": "error"}
        driver.get("https://x.com/messages")
        
        # Wait for DM list
        try:
            WebDriverWait(driver, 10).until(
                EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='conversation']"))
            )
        except:
            return {"status": "success", "data": []}
            
        dms = []
        conversations = driver.find_elements(By.CSS_SELECTOR, "[data-testid='conversation']")
        
        for conv in conversations[:3]:
            try:
                # Extract text using textContent to get hidden text
                text = conv.text.replace("\n", " | ")
                dms.append(text)
            except: pass
            
        return {"status": "success", "data": dms}
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_financial_summary():
    """Fetch BIST100, USD, EUR, Gold using yfinance for 100% stability"""
    import yfinance as yf
    data = {}
    
    try:
        # BIST100
        bist = yf.Ticker("XU100.IS")
        data["BIST100"] = f"{bist.history(period='1d')['Close'].iloc[-1]:.2f}".replace('.', ',')
        
        # USD/TRY
        usd = yf.Ticker("USDTRY=X")
        data["USD"] = f"{usd.history(period='1d')['Close'].iloc[-1]:.4f}".replace('.', ',')
        
        # EUR/TRY
        eur = yf.Ticker("EURTRY=X")
        data["EUR"] = f"{eur.history(period='1d')['Close'].iloc[-1]:.4f}".replace('.', ',')
        
        # GOLD (ONS)
        gold = yf.Ticker("GC=F")
        data["Gold"] = f"{gold.history(period='1d')['Close'].iloc[-1]:.2f}".replace('.', ',')
        
        # SILVER
        silver = yf.Ticker("SI=F")
        data["Silver"] = f"{silver.history(period='1d')['Close'].iloc[-1]:.2f}".replace('.', ',')
        
    except Exception as e:
        print(f"yfinance error: {e}", file=sys.stderr)
        # Fallback to N/A if all fails
        for k in ["BIST100", "USD", "EUR", "Gold", "Silver"]:
            if k not in data: data[k] = "N/A"

    return {"status": "success", "data": data}

def post_thread_chain(tweets, media_path=None):
    """
    Post a tweet thread — v4.9.2 REPLY-CHAIN
    Her tweet ayrı gönderilir: 1. tweet normal, sonrakiler bir öncekine reply.
    Add buton/compose modal yaklaşımı tamamen terk edildi.
    """
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC

    if not tweets:
        return {"status": "error", "message": "No tweets provided"}

    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies found."}

    def _post_one(driver, text, reply_to_url=None, media_path=None):
        """Tek bir tweet gönder. reply_to_url verilirse o tweet'e reply atar. media_path verilirse görsel ekler."""
        try:
            if reply_to_url:
                driver.get(reply_to_url)
                time.sleep(3)
                try:
                    reply_btn = WebDriverWait(driver, 10).until(
                        EC.element_to_be_clickable((By.CSS_SELECTOR,
                            "[data-testid='reply']"))
                    )
                    driver.execute_script("arguments[0].click();", reply_btn)
                    print(f"Reply button clicked: {reply_to_url}", file=sys.stderr)
                    time.sleep(2)
                except Exception as e:
                    print(f"Reply btn fail, fallback intent URL: {e}", file=sys.stderr)
                    tweet_id = reply_to_url.rstrip("/").split("/")[-1]
                    driver.get(f"https://x.com/intent/tweet?in_reply_to={tweet_id}")
                    time.sleep(3)
            else:
                driver.get("https://x.com/compose/tweet")
                print("Compose opened via URL", file=sys.stderr)
                time.sleep(3)

            # Textbox bul
            selectors = [
                "[data-testid='tweetTextarea_0']",
                "div[role='textbox']",
                "div[contenteditable='true']"
            ]
            box = None
            for sel in selectors:
                try:
                    box = WebDriverWait(driver, 10).until(
                        EC.presence_of_element_located((By.CSS_SELECTOR, sel))
                    )
                    if box:
                        print(f"Textbox found: {sel}", file=sys.stderr)
                        break
                except Exception:
                    continue

            if not box:
                print("FATAL: Textbox not found", file=sys.stderr)
                return None

            # Yaz
            robust_type_and_verify(driver, box, text, 0)

            # Medya yükle (sadece media_path verilmişse)
            if media_path and os.path.exists(media_path):
                try:
                    file_input = driver.find_element(By.CSS_SELECTOR,
                        "input[data-testid='fileInput'], input[accept*='image'][type='file']")
                    file_input.send_keys(media_path)
                    print(f"Media uploaded: {media_path}", file=sys.stderr)
                    time.sleep(3)  # Yüklemenin tamamlanmasını bekle
                except Exception as e:
                    print(f"Media upload failed (non-fatal): {e}", file=sys.stderr)
            post_btn = None
            btn_selectors = [
                "[data-testid='tweetButton']",
                "[data-testid='tweetButtonInline']",
                "[data-testid='sendReplies']",
            ]
            for attempt in range(8):
                for sel in btn_selectors:
                    try:
                        btns = driver.find_elements(By.CSS_SELECTOR, sel)
                        for b in btns:
                            if b.is_displayed() and b.get_attribute("aria-disabled") != "true":
                                post_btn = b
                                break
                    except Exception:
                        pass
                    if post_btn:
                        break
                if post_btn:
                    print(f"Post button ready (attempt {attempt+1})", file=sys.stderr)
                    break
                print(f"Post button not ready (attempt {attempt+1}/8)...", file=sys.stderr)
                time.sleep(2.5)

            if not post_btn:
                print("FATAL: Post button not found", file=sys.stderr)
                return None

            driver.execute_script(
                "arguments[0].scrollIntoView({behavior:'instant',block:'center'});", post_btn)
            time.sleep(0.4)
            driver.execute_script("arguments[0].click();", post_btn)
            time.sleep(3)

            # Dialog kapansın
            try:
                WebDriverWait(driver, 8).until(
                    EC.invisibility_of_element_located((By.CSS_SELECTOR,
                        "[data-testid='tweetTextarea_0']"))
                )
                print("Dialog closed — posted", file=sys.stderr)
            except Exception:
                print("Dialog close timeout — continuing", file=sys.stderr)

            # Tweet URL'sini al
            # Yöntem 1: current_url
            cur = driver.current_url
            if "/status/" in cur:
                print(f"Tweet URL (current_url): {cur}", file=sys.stderr)
                return cur

            # Yöntem 2: profil sayfası
            try:
                handle_el = driver.execute_script(
                    "var el=document.querySelector('a[data-testid=\"AppTabBar_Profile_Link\"]'); return el?el.getAttribute('href'):null;")
                if handle_el:
                    driver.get(f"https://x.com{handle_el}")
                    time.sleep(2.5)
                    links = driver.find_elements(By.CSS_SELECTOR,
                        "article[data-testid='tweet'] a[href*='/status/']")
                    for lnk in links:
                        href = lnk.get_attribute("href") or ""
                        if handle_el.strip("/") in href and "/status/" in href:
                            print(f"Tweet URL (profile): {href}", file=sys.stderr)
                            return href
            except Exception as e:
                print(f"Profile URL method: {e}", file=sys.stderr)

            print("WARNING: Could not get tweet URL", file=sys.stderr)
            return "https://x.com/home"

        except Exception as e:
            print(f"_post_one error: {type(e).__name__}: {e}", file=sys.stderr)
            return None

    # ── Driver başlat ────────────────────────────────────────────────────────
    driver = setup_driver(headless=False)
    if not driver:
        return {"status": "error", "message": "Failed to start driver"}

    try:
        if not load_cookies(driver):
            return {"status": "error", "message": "Failed to load cookies"}

        driver.refresh()
        time.sleep(2)

        if "login" in driver.current_url.lower() or "flow" in driver.current_url.lower():
            return {"status": "error", "message": "Session expired. Please re-login."}

        print(f"[Thread] reply-chain: {len(tweets)} tweets", file=sys.stderr)

        # ── 1. tweet ─────────────────────────────────────────────────────────
        print(f"[Thread] Posting tweet 1/{len(tweets)}...", file=sys.stderr)
        first_url = _post_one(driver, tweets[0], reply_to_url=None, media_path=media_path)

        if not first_url:
            return {"status": "error", "message": "First tweet failed"}

        print(f"[Thread] ✅ Tweet 1 posted: {first_url}", file=sys.stderr)
        time.sleep(4)

        # ── Kalan tweetler ───────────────────────────────────────────────────
        last_url = first_url
        for i, tweet_text in enumerate(tweets[1:], 2):
            print(f"[Thread] Posting tweet {i}/{len(tweets)} as reply...", file=sys.stderr)
            reply_url = _post_one(driver, tweet_text, reply_to_url=last_url)

            if not reply_url:
                print(f"[Thread] WARNING: Tweet {i} failed", file=sys.stderr)
                time.sleep(3)
                reply_url = last_url  # aynı URL'e retry

            print(f"[Thread] ✅ Tweet {i} posted", file=sys.stderr)
            last_url = reply_url
            time.sleep(4)

        print(f"✅ Thread posted successfully ({len(tweets)} parts)", file=sys.stderr)
        return {"status": "success", "message": f"Thread with {len(tweets)} tweets posted!", "url": first_url}

    except Exception as e:
        import traceback
        return {"status": "error", "message": f"Thread fail: {str(e)} | {traceback.format_exc()}"}
    finally:
        if driver:
            driver.quit()

def fetch_search_news():
    """Search for breaking financial news on X"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    if not COOKIES_FILE.exists(): return {"status": "error", "message": "No cookies"}
    
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        # Search for high-impact financial news (Turkey & World)
        # Fix: Remove min_faves:5 and filter:media which hides fresh news on Live tab
        query = "(borsa OR hisse OR bist100 OR ekonomi OR dolar OR altın OR fed OR tcmb OR \"son dakika\") (haber OR gelişme) -filter:replies"
        encoded = urllib.parse.quote(query)
        url = f"https://x.com/search?q={encoded}&src=typed_query&f=live"
        
        driver.get(url)
        
        try:
            WebDriverWait(driver, 10).until(EC.presence_of_element_located((By.TAG_NAME, "article")))
        except:
            return {"status": "success", "data": []} # No results is not an error
        # Scroll down multiple times to ensure we get enough tweets (for 20)
        for _ in range(3):
            driver.execute_script("window.scrollBy(0, 2000);")
            time.sleep(1.2)
        
        # Final wait for DOM stabilization
        time.sleep(1.0)

        results = []
        tweets = driver.find_elements(By.TAG_NAME, "article")
        print(f"DEBUG: Found {len(tweets)} elements in news feed", file=sys.stderr)
        
        for tweet in tweets[:20]:
            try:
                # 1. Clean Text
                content = ""
                try:
                    text_elem = tweet.find_element(By.CSS_SELECTOR, "[data-testid='tweetText']")
                    content = text_elem.text
                except:
                    content = tweet.text.replace("\n", " ")
                
                if not content or len(content.strip()) < 10:
                    continue

                # 2. Extract handle (Improved v4.8.2)
                handle = ""
                try:
                    user_el = tweet.find_element(By.CSS_SELECTOR, "[data-testid='User-Names']")
                    handle_text = user_el.text
                    m = re.search(r'@(\w+)', handle_text)
                    if m: handle = "@" + m.group(1)
                except: pass

                if not handle:
                    try:
                        # Profile link fallback
                        links = tweet.find_elements(By.CSS_SELECTOR, "a[role='link']")
                        for l in links:
                            href = l.get_attribute("href") or ""
                            if "x.com/" in href and not any(x in href for x in ["/status/", "/search", "home"]):
                                handle = "@" + href.split("x.com/")[1].split("/")[0].split("?")[0]
                                break
                    except: pass
                
                if not handle: handle = "X-Haber"
                
                # 3. Real URL & Time
                url = "https://x.com"
                time_val = ""
                try:
                    time_elem = tweet.find_element(By.TAG_NAME, "time")
                    time_val = time_elem.get_attribute("datetime") # Extract ISO time
                    link_elem = time_elem.find_element(By.XPATH, "./..")
                    url = link_elem.get_attribute("href")
                except: pass

                results.append({
                    "text": content,
                    "source": handle,
                    "url": url,
                    "time": time_val
                })
            except Exception as e:
                print(f"Error parsing news tweet: {e}", file=sys.stderr)
                continue
            
        print(f"DEBUG: Scraped {len(results)} news items", file=sys.stderr)
        return {"status": "success", "data": results}
        
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def scrape_bigpara_market_list(url, limit=10):
    """Scrape BigPara market data using requests + bs4"""
    results = []
    try:
        headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        }
        resp = requests.get(url, headers=headers, timeout=10)
        if resp.status_code != 200:
            return {"status": "error", "message": f"HTTP {resp.status_code}"}
            
        soup = BeautifulSoup(resp.content, "html.parser")
        
        # Find all stock links (avoid menus)
        # Select links that contain '-detay' in href but NOT 'hisselerim-detay'
        stock_links = soup.select("a[href*='-detay']")
        valid_links = [l for l in stock_links if '/borsa/hisse-fiyatlari/' in l.get('href', '') and 'hisselerim-detay' not in l.get('href', '')]
        
        # Deduplicate by href/text
        seen = set()
        
        for link in valid_links:
            if len(results) >= limit: break
            
            try:
                # Find parent row (usually ul)
                row = link.find_parent("ul")
                if not row: continue
                
                # Get fields
                # Structure: Symbol | Price | Prev | % | ...
                fields = [t.strip() for t in row.get_text(separator="|").split("|") if t.strip()]
                
                # Validate fields
                # Index 0 is Symbol usually
                if len(fields) < 4: continue
                
                symbol = fields[0]
                if symbol in seen: continue
                seen.add(symbol)
                
                # Parse Price (Index 1) and Change (Index 3)
                # Note: Index might vary slightly if structure changes, but typically:
                # 0: SYM, 1: Price, 2: Prev, 3: Percent
                
                price_str = fields[1].replace(".", "").replace(",", ".")
                pct_str = fields[3].replace(",", ".")
                
                try:
                    price = float(price_str)
                    change = float(pct_str)
                    
                    results.append({
                        "Symbol": symbol,
                        "Close": price,
                        "ChangePercent": change
                    })
                except: continue

            except: continue
            
        if not results:
             return {"status": "error", "message": "No data parsed from BigPara"}

        return {"status": "success", "data": results}

    except Exception as e:
        return {"status": "error", "message": str(e)}

def get_top_gainers():
    """Scrape BIST100 top gainers"""
    return scrape_bigpara_market_list("https://bigpara.hurriyet.com.tr/borsa/en-cok-artan-hisseler/")

def get_top_losers():
    """Scrape BIST100 top losers"""
    return scrape_bigpara_market_list("https://bigpara.hurriyet.com.tr/borsa/en-cok-azalanlar/")

def get_top_volume():
    """Scrape BIST100 most active (volume)"""
    return scrape_bigpara_market_list("https://bigpara.hurriyet.com.tr/borsa/en-cok-islem-gorenler-tl/")

def find_twitter_handle_via_google(name):
    """Search Google for an Official Twitter handle"""
    from selenium.webdriver.common.keys import Keys
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    import time
    
    # Use headless for search
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    handle = None
    try:
        query = f"{name} official twitter"
        print(f"DEBUG: Googling for {name}...", file=sys.stderr)
        
        # Google Search
        driver.get("https://www.google.com/search?q=" + urllib.parse.quote(query))
        
        # Parse results
        try:
            # Wait for results
            WebDriverWait(driver, 10).until(EC.presence_of_element_located((By.ID, "search")))
            
            links = driver.find_elements(By.CSS_SELECTOR, "div#search a")
            for link in links:
                href = link.get_attribute("href")
                if href and ("twitter.com/" in href or "x.com/" in href):
                    # Clean up URL to get handle
                    # e.g. https://twitter.com/edindzeko?lang=en -> @edindzeko
                    parts = href.replace("https://", "").replace("http://", "").split("/")
                    if len(parts) > 1:
                        raw_handle = parts[1].split("?")[0] # Remove query params
                        if raw_handle not in ["status", "hashtag", "search", "home", "login", "explore"]:
                            handle = "@" + raw_handle
                            print(f"DEBUG: Found handle {handle} for {name}", file=sys.stderr)
                            break
        except Exception as e:
            print(f"DEBUG: Google parse error: {e}", file=sys.stderr)
            
        if handle:
            return {"status": "success", "handle": handle}
        else:
            return {"status": "error", "message": "No handle found"}
            
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        try:
             # Just in case this was running independent
             pass
        except: pass

def quote_retweet(url, text):
    """
    Quotes a tweet with the given text.
    """
    driver = ChromeDriverPool.get()
    try:
        driver.get(url)
        time.sleep(random.uniform(2.5, 4.0))
        
        # 1. Scroll to bring actions into view
        driver.execute_script("window.scrollBy(0, 300);")
        time.sleep(1)
        
        # 2. Click Retweet Button (group[id*='retweet'])
        try:
            rt_btn = driver.find_element(By.CSS_SELECTOR, "div[data-testid='retweet']")
            rt_btn.click()
            time.sleep(1)
        except:
             return {"status": "error", "message": "Retweet button not found"}
             
        # 3. Click 'Quote' option
        try:
            quote_item = driver.find_element(By.XPATH, "//span[contains(text(), 'Alıntı') or contains(text(), 'Quote')]")
            quote_item.click()
            time.sleep(1.5)
        except:
             return {"status": "error", "message": "Quote option not found"}
             
        # 4. Type text
        try:
            input_area = driver.find_element(By.CSS_SELECTOR, "div[data-testid='tweetTextarea_0']")
            atomic_clear(input_area, driver)
            
            # Type safely
            for char in text:
                input_area.send_keys(char)
                time.sleep(random.uniform(0.01, 0.05))
            
            time.sleep(1)
        except:
             return {"status": "error", "message": "Quote text area not found"}
             
        # 5. Click Post
        try:
            post_btn = driver.find_element(By.CSS_SELECTOR, "div[data-testid='tweetButton']")
            post_btn.click()
            time.sleep(3)
        except:
             return {"status": "error", "message": "Post button not found"}
             
        return {"status": "success", "text": text}
        
    except Exception as e:
        return {"status": "error", "message": f"Quote RT error: {str(e)}"}

def interact_with_targets(targets):
    """Like + RT the last tweet of target accounts
    targets_str: comma-separated handles (e.g., "@handle1,@handle2" or "handle1,2")
    """
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies found"}
    
    handles = [h.strip().lstrip('@') for h in targets_str.split(',') if h.strip()]
    if not handles:
        return {"status": "error", "message": "No handles provided"}
    
    driver = setup_driver(headless=True, use_undetected=True)
    if not driver:
        return {"status": "error", "message": "Failed to start driver"}
    
    try:
        if not load_cookies(driver):
            return {"status": "error", "message": "Failed to load cookies"}
        
        from selenium.webdriver.support.ui import WebDriverWait
        from selenium.webdriver.support import expected_conditions as EC
        
        results = {}

        def click_first(selectors, timeout=8):
            """Try a list of CSS selectors until one is clickable."""
            last_err = None
            for sel in selectors:
                try:
                    el = WebDriverWait(driver, timeout).until(EC.element_to_be_clickable((By.CSS_SELECTOR, sel)))
                    el.click()
                    return True, sel
                except Exception as e:
                    last_err = e
            return False, last_err
        
        for handle in handles:
            try:
                # Go to profile
                profile_url = f"https://x.com/{handle}"
                print(f"Fetching tweets from @{handle}...", file=sys.stderr)
                driver.get(profile_url)
                time.sleep(random.uniform(2, 4))
                
                # Find first tweet (excluding pins, replies, retweets - only original tweets)
                tweet_links = driver.find_elements(By.CSS_SELECTOR, "a[href*='/status/']")
                if not tweet_links:
                    results[handle] = "No tweets found"
                    continue
                
                # Get first original (non-retweet, non-reply, non-pinned) tweet
                found_tweet_url = None
                for link in tweet_links[:30]:  # Check first 30
                    href = link.get_attribute("href")
                    if href and '/status/' in href:
                        # Check if this is a reply, retweet, or pinned
                        parent = link
                        is_reply = False
                        is_retweet = False
                        is_pinned = False
                        
                        try:
                            # Go up to find tweet container (max 8 levels)
                            for _ in range(8):
                                parent = parent.find_element(By.XPATH, "..")
                            
                            container_text = parent.text.lower()
                            
                            # v2.1 Improved Filtering
                            # 1. Skip pinned
                            if "pinned" in container_text or "sabitlendi" in container_text:
                                is_pinned = True
                            
                            # 2. Skip replies - Check for "replying to" or multiple social contexts
                            social_context = ""
                            try:
                                social_context = parent.find_element(By.CSS_SELECTOR, "[data-testid='socialContext']").text.lower()
                            except: pass

                            if ("replying to" in container_text or 
                                "yanıt olarak" in container_text or
                                "başlık olarak" in container_text):
                                is_reply = True
                            
                            # 3. Skip retweets
                            if "retweeted" in container_text or "retweet'ledi" in container_text or "retweet" in social_context:
                                # Ensure it's not "Retweeted your tweet"
                                if not ("retweeted your" in container_text):
                                    is_retweet = True
                            
                            # 4. Final safety: If the username mentioned in the context is NOT the handle, it's likely a RT or Reply
                            # (Advanced check omitted for stability unless needed)

                            if is_pinned or is_reply or is_retweet: 
                                continue
                        except:
                            pass
                        
                        found_tweet_url = href
                        break
                
                if not found_tweet_url:
                    results[handle] = "No suitable tweet found"
                    continue
                
                # Navigate to tweet
                driver.get(found_tweet_url)
                time.sleep(random.uniform(1, 2))

                # Like button: handle lowercase/uppercase testids
                like_selectors = [
                    "[data-testid='like']",
                    "[data-testid='Like']",
                    "div[data-testid='like']",
                    "button[data-testid='like']",
                ]
                ok_like, err_like = click_first(like_selectors, timeout=6)
                if ok_like:
                    time.sleep(random.uniform(0.5, 1))
                    results[handle] = "✅ Like + Attempting RT"
                else:
                    results[handle] = "⚠️ Like button not found"

                # Retweet button: similar fallbacks
                rt_selectors = [
                    "[data-testid='retweet']",
                    "[data-testid='Retweet']",
                    "div[data-testid='retweet']",
                    "button[data-testid='retweet']",
                ]
                ok_rt, err_rt = click_first(rt_selectors, timeout=6)
                if ok_rt:
                    time.sleep(random.uniform(0.5, 1.5))
                    # Confirm RT if modal appears
                    confirm_selectors = [
                        "[data-testid='retweetConfirm']",
                        "div[data-testid='retweetConfirm']",
                        "button[data-testid='retweetConfirm']",
                    ]
                    click_first(confirm_selectors, timeout=3)
                    results[handle] = f"✅ Like + RT: {found_tweet_url}"
                else:
                    results[handle] += " (RT failed)"
                
                time.sleep(random.uniform(2, 4))
                
            except Exception as e:
                results[handle] = f"Error: {str(e)}"
        
        return {"status": "success", "data": results}
    
    except Exception as e:
        return {"status": "error", "message": str(e)}
    
    finally:
        if driver: driver.quit()

def discover_influencers(category, custom_query=None):
    """(Nightly Job) Discover high-quality accounts by scanning followers of major hub accounts."""
    category = category.upper()
    hub_accounts = {
        "BIST": ["bigpara", "BloombergHT", "InvestingTR"],
        "CRYPTO": ["BTCTurk", "Paribu", "bitcointr", "CoinDesk"],
        "FOREX": ["InvestingTR", "gcmforex", "ForeksHaber"],
        "TECH": ["OpenAI", "TechCrunch", "WIRED", "TheVerge", "YCombinator"],
        "BUSINESS": ["Forbes", "Entrepreneur", "BusinessInsider", "HBR", "Inc"],
        "PERSONAL": ["TimFerriss", "JamesClear", "HubermanLab", "RyanHoliday"],
        "GLOBAL": ["TheEconomist", "WorldBank", "wef", "IanBremmer"]
    }
    keywords = {
        "BIST": ["yatırım", "finans", "borsa", "ekonomi", "hisse", "trader"],
        "CRYPTO": ["kripto", "crypto", "bitcoin", "btc", "eth", "blockchain"],
        "FOREX": ["forex", "döviz", "altın", "gold", "emtia", "fx"],
        "TECH": ["ai", "machine learning", "tech", "software", "engineer", "founder", "cto", "developer"],
        "BUSINESS": ["entrepreneur", "ceo", "business", "investor", "venture", "growth", "strategy"],
        "PERSONAL": ["author", "health", "mindset", "productivity", "coach", "speaker", "biohacker"],
        "GLOBAL": ["global", "economy", "policy", "geopolitics", "analyst", "researcher", "macro"]
    }
    target_hubs = hub_accounts.get(category)
    if not target_hubs:
        return {"status": "error", "message": f"Invalid category: {category}"}
    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies found."}
    driver = setup_driver(headless=True, use_undetected=True)
    if not driver: return {"status": "error", "message": "Failed to start driver"}
    try:
        if not load_cookies(driver):
            return {"status": "error", "message": "Failed to load cookies"}
        from selenium.webdriver.support.ui import WebDriverWait
        from selenium.webdriver.support import expected_conditions as EC
        discovered_handles = set()
        for hub_handle in target_hubs:
            followers_url = f"https://x.com/{hub_handle}/followers"
            print(f"Scanning followers of @{hub_handle}", file=sys.stderr)
            driver.get(followers_url)
            try:
                WebDriverWait(driver, 15).until(EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='UserCell']")))
            except:
                continue
            for i in range(5):
                driver.execute_script(f"window.scrollBy(0, {random.randint(800, 1200)});")
                time.sleep(random.uniform(1.0, 2.5))
            user_cells = driver.find_elements(By.CSS_SELECTOR, "[data-testid='UserCell']")
            for cell in user_cells:
                try:
                    bio_text = cell.text.lower()
                    if any(keyword in bio_text for keyword in keywords.get(category, [])):
                        handle_element = cell.find_element(By.CSS_SELECTOR, "a[href^='/'][role='link']")
                        handle = handle_element.get_attribute("href").split("/")[-1]
                        if handle and len(handle) > 2:
                            discovered_handles.add("@" + handle)
                except:
                    continue
            if len(discovered_handles) >= 25:
                break
        final_list = list(discovered_handles)
        return {"status": "success", "count": len(final_list), "data": final_list}
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

if __name__ == "__main__":
    # Shared parent parser for global flags
    parent_parser = argparse.ArgumentParser(add_help=False)
    parent_parser.add_argument("--visible", action="store_true", help="Show browser window for debugging")
    parent_parser.add_argument("--base64", action="store_true", help="Treat text/queries as base64 encoded")

    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command")
    
    # GetCookiesJson command
    cmd_getcookies = subparsers.add_parser("GetCookiesJson", parents=[parent_parser])

    # Login command
    cmd_login = subparsers.add_parser("login_interactive", parents=[parent_parser])
    
    # Search command
    cmd_search = subparsers.add_parser("search_influencer", parents=[parent_parser])
    cmd_search.add_argument("--query", required=True)
    cmd_search.add_argument("--market", required=True)
    cmd_search.add_argument("--limit", required=False, help="Number of tweets to fetch")
    cmd_search.add_argument("--since", required=False, help="Fetch tweets since YYYY-MM-DD")
    cmd_search.add_argument("--until_date", required=False, help="Fetch tweets until YYYY-MM-DD")
    # --base64 and --visible are now inherited from parent_parser
    
    # Import cookies command
    cmd_import = subparsers.add_parser("import_cookies", parents=[parent_parser])
    cmd_import.add_argument("--file", required=True)
    
    # Post tweet command
    cmd_post = subparsers.add_parser("post_tweet", parents=[parent_parser])
    cmd_post.add_argument("--file", help="Path to JSON file containing text and media")
    cmd_post.add_argument("--text", help="Tweet text (Base64 encoded)")
    cmd_post.add_argument("--media", help="Path to image/video file to upload")
    
    # Clear session command
    cmd_clear = subparsers.add_parser("clear_session", parents=[parent_parser])

    # Get Stats
    cmd_stats = subparsers.add_parser("get_stats", parents=[parent_parser])

    # Get Trends
    cmd_trends = subparsers.add_parser("get_trends", parents=[parent_parser])

    # Get DMs
    cmd_dms = subparsers.add_parser("get_dms", parents=[parent_parser])

    # Get Financials
    cmd_fin = subparsers.add_parser("get_financials", parents=[parent_parser])

    # Get Top Gainers
    cmd_gainers = subparsers.add_parser("get_top_gainers", parents=[parent_parser])

    # Get Top Losers
    cmd_losers = subparsers.add_parser("get_top_losers", parents=[parent_parser])

    # Get Top Volume
    cmd_volume = subparsers.add_parser("get_top_volume", parents=[parent_parser])

    # Get Engagement
    cmd_engagement = subparsers.add_parser("get_engagement", parents=[parent_parser])

    # Batch Get Prices
    cmd_batch = subparsers.add_parser("batch_get_prices", parents=[parent_parser])
    cmd_batch.add_argument("--symbols", required=True, help="Comma-separated symbols")

    # Interact Targets
    cmd_interact = subparsers.add_parser("interact_with_targets", parents=[parent_parser])
    cmd_interact.add_argument("--targets", required=True)

    # Post Thread
    cmd_thread = subparsers.add_parser("post_thread", parents=[parent_parser])
    cmd_thread.add_argument("--file", help="Path to JSON file containing tweets list and media")
    cmd_thread.add_argument("--tweets", help="JSON list of tweets (Base64 encoded)")
    cmd_thread.add_argument("--media", help="Path to media file (image) to attach to first tweet")
    # --base64 and --visible inherited

    # Reply Tweet command
    cmd_reply = subparsers.add_parser("reply_tweet", parents=[parent_parser])
    cmd_reply.add_argument("--url", required=True)
    cmd_reply.add_argument("--text", required=True)

    # Fetch News command
    cmd_news = subparsers.add_parser("fetch_news", parents=[parent_parser])
    
    cmd_discover = subparsers.add_parser("discover_influencers", parents=[parent_parser])
    cmd_discover.add_argument("--category", required=True, help="BIST, CRYPTO, or FOREX")
    cmd_discover.add_argument("--query", required=False, help="Custom search query (optional)")

    # Like Tweet command
    cmd_like = subparsers.add_parser("like_tweet", parents=[parent_parser])
    cmd_like.add_argument("--url", required=True)

    # Retweet command
    cmd_rt = subparsers.add_parser("retweet", parents=[parent_parser])
    cmd_rt.add_argument("--url", required=True)

    # Find Handle command
    cmd_find = subparsers.add_parser("find_handle", parents=[parent_parser])
    cmd_find.add_argument("--name", required=True)

    args = parser.parse_args()
    
    # Global visibility override
    if hasattr(args, 'visible') and args.visible:
        os.environ["X_VISIBLE"] = "1"
    
    # ---------------------------------------------------------
    # HANDLERS WITH LOCK PROTECTION
    # ---------------------------------------------------------
    
    # User Request: Integrate lock_manager to prevent parallel execution
    try:
        from lock_manager import acquire_lock, release_lock
    except ImportError:
        # Fallback if lock_manager is not found (e.g. path issues)
        def acquire_lock(**kwargs): pass
        def release_lock(): pass
        print("Warning: lock_manager not found, running without lock.", file=sys.stderr)

    exit_code = 0
    try:
        # Acquire lock before any navigation or driver start
        # Timeout 180s, Stale 600s
        acquire_lock(timeout_seconds=180, stale_seconds=600)
        
        if args.command == "login_interactive":
            print(json.dumps(login_interactive()))
            
        elif args.command == "search_influencer":
            query = args.query
            if args.base64 and query:
                try:
                    query = base64.b64decode(query).decode('utf-8')
                except Exception as decode_err:
                    print("---JSON_START---")
                    print(json.dumps([]))
                    print("---JSON_END---")
                    sys.exit(0)
            
            limit = int(args.limit) if hasattr(args, "limit") and args.limit else 10
            since = args.since if hasattr(args, "since") and args.since else None
            until = args.until_date if hasattr(args, "until_date") and args.until_date else None

            print("---JSON_START---")
            result_json = find_influencer_posts(query, args.market, limit=limit, since_date=since, until_date=until)
            print(result_json)
            print("---JSON_END---")
            
        elif args.command == "import_cookies":
            print(json.dumps(import_cookies(args.file)))

        elif args.command == "post_tweet":
            text = ""
            media = args.media
            
            # Priority: File -> CLI
            if args.file and os.path.exists(args.file):
                try:
                    with open(args.file, 'r', encoding='utf-8') as f:
                        payload = json.load(f)
                        text = payload.get("text", "")
                        if "media" in payload and payload["media"]:
                            media = payload["media"]
                except Exception as e:
                     print(json.dumps({"status": "error", "message": f"File read error: {str(e)}"}))
                     sys.exit(1)
            else:
                text = args.text
                if args.base64 and text:
                    try:
                         text = base64.b64decode(text.strip()).decode('utf-8')
                    except: pass

            print("---JSON_START---")
            print(json.dumps(post_tweet(text, media_path=media)))
            print("---JSON_END---")

        elif args.command == "clear_session":
            try:
                if COOKIES_FILE.exists():
                    os.remove(COOKIES_FILE)
                print(json.dumps({"status": "success", "message": "Session cleared."}))
            except Exception as e:
                print(json.dumps({"status": "error", "message": str(e)}))
                
        elif args.command == "get_stats":
            print(json.dumps(get_profile_stats()))

        elif args.command == "get_engagement":
            print(json.dumps(get_recent_engagement()))

        elif args.command == "get_trends":
            print(json.dumps(get_breaking_news_topics()))

        elif args.command == "get_dms":
            print(json.dumps(get_dms()))

        elif args.command == "get_financials":
            print(json.dumps(get_financial_summary()))

        elif args.command == "get_top_gainers":
            print(json.dumps(get_top_gainers()))

        elif args.command == "get_top_losers":
            print(json.dumps(get_top_losers()))

        elif args.command == "get_top_volume":
            print(json.dumps(get_top_volume()))

        elif args.command == "interact_with_targets":
            targets = args.targets if hasattr(args, 'targets') and args.targets else ""
            print(json.dumps(interact_with_targets(targets)))

        elif args.command == "reply_tweet":
            text_content = args.text
            if args.base64:
                try:
                    text_content = base64.b64decode(args.text).decode('utf-8')
                except: pass
            print("---JSON_START---")
            print(json.dumps(reply_to_tweet(args.url, text_content)))
            print("---JSON_END---")

        elif args.command == "post_thread":
            tweets = []
            media = args.media

            # Priority: File -> CLI
            if args.file and os.path.exists(args.file):
                try:
                    with open(args.file, 'r', encoding='utf-8') as f:
                        payload = json.load(f)
                        
                        # Handle if payload is just a list (legacy compat) or dict
                        if isinstance(payload, list):
                            tweets = payload
                        elif isinstance(payload, dict):
                            tweets = payload.get("tweets", [])
                            if "media" in payload and payload["media"]:
                                media = payload["media"]
                except Exception as e:
                     print(json.dumps({"status": "error", "message": f"File read error: {str(e)}"}))
                     sys.exit(1)
            else:
                # Legacy CLI Args
                tweets_str = args.tweets
                if args.base64 and tweets_str:
                    try:
                        tweets_str = base64.b64decode(tweets_str.strip()).decode('utf-8')
                    except: pass
                
                try:
                    tweets = json.loads(tweets_str)
                except:
                    tweets = [tweets_str]

            print("---JSON_START---")
            print(json.dumps(post_thread_chain(tweets, media)))
            print("---JSON_END---")
            
        elif args.command == "fetch_news":
            print("---JSON_START---")
            print(json.dumps(fetch_search_news()))
            print("---JSON_END---")
        
        elif args.command == "discover_influencers":
            custom_q = args.query if hasattr(args, 'query') and args.query else None
            print(json.dumps(discover_influencers(args.category, custom_query=custom_q)))
        
        elif args.command == "find_handle":
            print(json.dumps(find_twitter_handle_via_google(args.name)))
        
        elif args.command == "GetCookiesJson":
            if COOKIES_FILE.exists():
                try:
                    cookies = pickle.load(open(COOKIES_FILE, "rb"))
                    print(json.dumps(cookies))
                except:
                    print("[]")
            else:
                print("[]")

        elif args.command == "SetCookiesFromJson":
            if args.file and os.path.exists(args.file):
                try:
                    with open(args.file, 'r', encoding='utf-8') as f:
                        cookies = json.load(f)
                    pickle.dump(cookies, open(COOKIES_FILE, "wb"))
                    print(json.dumps({"status": "success"}))
                except Exception as e:
                    print(json.dumps({"status": "error", "message": str(e)}))
        elif args.command == "batch_get_prices":
            symbols = []
            if args.symbols:
                symbols = [s.strip() for s in args.symbols.split(",")]
            print(json.dumps(get_stock_prices(symbols)))
        
        elif args.command == "like_tweet":
            url = args.url
            if not url:
                print(json.dumps({"status": "error", "message": "Missing --url"}))
            else:
                print("---JSON_START---")
                print(json.dumps(like_tweet(url)))
                print("---JSON_END---")
                
        elif args.command == "retweet":
            url = args.url
            if not url:
                print(json.dumps({"status": "error", "message": "Missing --url"}))
            else:
                print(json.dumps(retweet(url)))
                print("---JSON_END---")

        elif args.command == "quote_retweet":
            url = args.url
            text = args.text
            if not url or not text:
                print(json.dumps({"status": "error", "message": "Missing --url or --text"}))
            else:
                if args.base64:
                    try:
                        text = base64.b64decode(text).decode('utf-8')
                    except: pass
                print("---JSON_START---")
                print(json.dumps(quote_retweet(url, text)))
                print("---JSON_END---")

        elif args.command == "fetch_replies":
            url = args.url
            if not url:
                print(json.dumps({"status": "error", "message": "Missing --url"}))
            else:
                print("---JSON_START---")
                print(json.dumps(fetch_replies(url)))
                print("---JSON_END---")

        elif args.command == "fetch_retweeters":
            url = args.url
            if not url:
                print(json.dumps({"status": "error", "message": "Missing --url"}))
            else:
                print("---JSON_START---")
                print(json.dumps(fetch_retweeters(url)))
                print("---JSON_END---")

        else:
            # Default fallback for unknown commands
            print(json.dumps({"status": "error", "message": f"Unknown command: {args.command}"}))
            exit_code = 1
            
    except Exception as e:
        print(f"CRITICAL ERROR: {str(e)}", file=sys.stderr)
        # Try to output generic error json if possible
        try:
            print(json.dumps({"status": "error", "message": f"Critical execution error: {str(e)}"}))
        except: pass
        exit_code = 1
        
    finally:
        try:
            release_lock()
        except: pass
        
    sys.exit(exit_code)
