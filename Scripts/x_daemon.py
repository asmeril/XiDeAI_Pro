#!/usr/bin/env python3
"""
X Daemon - HTTP server for X (Twitter) automation
Manages a single Chrome driver instance for all operations.
Runs on localhost:5580
"""

import os
import sys
import json
import time
import pickle
import threading
import traceback
import re
from datetime import datetime, timezone
from pathlib import Path

# Ensure UTF-8 encoding for stdout/stderr to handle Turkish characters on Windows
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8')
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import parse_qs, urlparse

# Configuration
HOST = "127.0.0.1"
PORT = 5580

def _discover_appdata():
    paths = [
        Path(os.environ.get("LOCALAPPDATA", os.path.expanduser("~"))) / "XiDeAI",
        Path(os.path.dirname(__file__)).parent, # Root of app
        Path(os.path.dirname(__file__)).parent.parent / "XiDeAI", # Sibling XiDeAI folder
        Path(os.path.dirname(__file__)), # Current dir
    ]
    for p in paths:
        if (p / "twitter_cookies.json").exists() or (p / "twitter_cookies.pkl").exists():
            return p
    return paths[0] # Default

APPDATA_DIR = _discover_appdata()
COOKIES_FILE = APPDATA_DIR / "twitter_cookies.pkl"

# Global state
_driver = None
_driver_lock = threading.Lock()
_last_activity = time.time()
_shutdown_flag = False

# Import Selenium components
try:
    import undetected_chromedriver as uc
    from selenium.webdriver.common.by import By
    from selenium.webdriver.common.keys import Keys
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
except ImportError:
    print("ERROR: selenium or undetected_chromedriver not installed", file=sys.stderr)
    sys.exit(1)


def log(msg):
    """Log with timestamp"""
    ts = datetime.now().strftime("%H:%M:%S")
    print(f"[{ts}] {msg}", file=sys.stderr, flush=True)


def get_driver():
    """Get or create the Chrome driver instance"""
    global _driver, _last_activity
    _last_activity = time.time()
    
    with _driver_lock:
        if _driver is None:
            log("Creating new Chrome driver...")
            _driver = _create_driver()
            if _driver:
                _load_cookies(_driver)
        return _driver


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
        hints["platformVersion"] = "13.0.0"
    elif "Linux" in ua_string: 
        hints["platform"] = "Linux"
        hints["platformVersion"] = "5.4.0"
    
    ver = "120"
    match = re.search(r"(Chrome|Edg)/(\d+)", ua_string)
    if match: 
        ver = match.group(2)
        hints["fullVersion"] = f"{ver}.0.0.0"
    
    brand = "Google Chrome"
    if "Edg/" in ua_string: brand = "Microsoft Edge"
        
    hints["brands"] = [
        {"brand": brand, "version": ver},
        {"brand": "Chromium", "version": ver},
        {"brand": "Not=A?Brand", "version": "99"}
    ]
    return hints


def _create_driver():
    """Create a new Chrome driver with optimal settings"""
    try:
        options = uc.ChromeOptions()
        # Removed anti-bot flags since undetected-chromedriver handles it natively
        options.add_argument("--no-sandbox")
        options.add_argument("--disable-dev-shm-usage")
        options.add_argument("--window-size=1920,1080")
        
        # Fast Page Load
        options.page_load_strategy = 'eager'
        
        # Image blocking
        prefs = {"profile.managed_default_content_settings.images": 2}
        options.add_experimental_option("prefs", prefs)
        
        # Use headless for background operations
        if os.environ.get("X_VISIBLE", "").lower() != "true":
            options.add_argument("--headless=new")
        
        # Create UC driver with auto-detected version fallback (v4.8.0)
        try:
            driver = uc.Chrome(options=options, use_subprocess=True)
        except Exception as driver_err:
            if "version" in str(driver_err).lower():
                # Auto-detect installed Chrome version instead of hardcoding
                detected_ver = None
                try:
                    import subprocess
                    # Windows: Read Chrome version from registry
                    result = subprocess.run(
                        ['reg', 'query', r'HKEY_CURRENT_USER\Software\Google\Chrome\BLBeacon', '/v', 'version'],
                        capture_output=True, text=True, timeout=5
                    )
                    if result.returncode == 0:
                        ver_match = re.search(r'(\d+)\.', result.stdout)
                        if ver_match:
                            detected_ver = int(ver_match.group(1))
                            log(f"Detected Chrome version: {detected_ver}")
                except Exception as detect_err:
                    log(f"Chrome version detection failed: {detect_err}")
                
                if not detected_ver:
                    detected_ver = 146  # Safe modern default
                
                log(f"Version mismatch detected in x_daemon, forcing version {detected_ver}...")
                # RECREATE OPTIONS: Cannot reuse after failure
                options = uc.ChromeOptions()
                options.add_argument("--no-sandbox")
                options.add_argument("--disable-dev-shm-usage")
                options.add_argument("--window-size=1920,1080")
                options.page_load_strategy = 'eager'
                prefs = {"profile.managed_default_content_settings.images": 2}
                options.add_experimental_option("prefs", prefs)
                if os.environ.get("X_VISIBLE", "").lower() != "true":
                    options.add_argument("--headless=new")
                
                driver = uc.Chrome(options=options, use_subprocess=True, version_main=detected_ver)
            else:
                raise driver_err
        
        # v4.4.7: Deep Identity Sync (Client Hints)
        json_file = APPDATA_DIR / "twitter_cookies.json"
        if json_file.exists():
            try:
                import json
                data = json.loads(json_file.read_text())
                use_ua = None
                use_hints = None
                
                
                for item in data:
                    if "meta_user_agent" in item:
                        use_ua = item["meta_user_agent"]
                    if "meta_client_hints" in item:
                        use_hints = item["meta_client_hints"]
                        if "mobile" in use_hints and isinstance(use_hints["mobile"], str):
                             use_hints["mobile"] = (use_hints["mobile"].lower() == "true")
                             
                # v4.4.8: Heuristic Generation if deep data missing
                if use_ua and not use_hints:
                    use_hints = _make_heuristic_hints(use_ua)
                    log(f"✅ Heuristic Identity Generated: {use_hints['brands'][0]['brand']} v{use_hints['brands'][0]['version']}")

                ua_override = {}
                if use_ua: ua_override["userAgent"] = use_ua
                if use_hints: ua_override["userAgentMetadata"] = use_hints
                
                if ua_override:
                    driver.execute_cdp_cmd('Network.setUserAgentOverride', ua_override)
                    if use_hints: log(f"✅ Deep Identity Synced with Metadata")
                    else: log(f"✅ User-Agent Synced: {use_ua[:30]}...")

            except Exception as e:
                log(f"Identity Sync Error: {e}")

        driver.set_page_load_timeout(30)
        log("Chrome driver created successfully")
        return driver
        
    except Exception as e:
        log(f"ERROR creating driver: {e}")
        return None


def _load_cookies(driver):
    """Load cookies into the driver"""
    json_file = APPDATA_DIR / "twitter_cookies.json"
    
    # v4.6.2: Google Drive conflict fallback - check for (1), (2) suffixed copies
    if not json_file.exists():
        import glob
        # Try finding in APPDATA_DIR or script's parent
        search_dirs = [APPDATA_DIR, Path(os.environ.get("LOCALAPPDATA", os.path.expanduser("~"))) / "XiDeAI"]
        for sd in search_dirs:
            pattern = str(sd / "twitter_cookies*.json")
            candidates = sorted(glob.glob(pattern), key=lambda f: os.path.getmtime(f), reverse=True)
            if candidates:
                json_file = Path(candidates[0])
                log(f"[COOKIE] Using fallback JSON: {json_file.name} from {sd}")
                break
    
    if not COOKIES_FILE.exists() and not json_file.exists():
        log("No cookies file found")
        return False
    
    try:
        driver.set_page_load_timeout(30)
        driver.get("https://x.com/home")
        
        # 1. Try JSON (Sync from WebView2)
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
                        
                    try: 
                        driver.add_cookie(cookie_dict)
                        added_count += 1
                    except Exception as cookie_err: 
                        log(f"Bypassed cookie err {cookie_dict.get('name')}: {cookie_err}")
                        
                log(f"✅ JSON Session Synced: {added_count}/{len(cookies)} cookies loaded from WebView2")
            except Exception as je:
                log(f"JSON Cookie Error: {je}")

        # 2. Try Pickle (Backup)
        if COOKIES_FILE.exists():
            try:
                cookies = pickle.load(open(COOKIES_FILE, "rb"))
                for cookie in cookies:
                    try:
                        if 'sameSite' in cookie:
                            if cookie['sameSite'] not in ["Strict", "Lax", "None"]:
                                del cookie['sameSite']
                        driver.add_cookie(cookie)
                    except:
                        pass
            except Exception as pe:
                 if not json_file.exists(): log(f"Pickle Error: {pe}")
        
        log(f"Cookies loaded. Refreshing session...")
        
        # FIX: Navigate to home to apply cookies
        try:
            driver.get("https://x.com/home")
            time.sleep(3)
            current = driver.current_url.lower()
            if "login" in current or "signin" in current or "i/flow" in current:
                log("❌ Cookie load FAILED: Redirected to Login Page")
                return False
        except: pass
        
        return True
    except Exception as e:
        log(f"ERROR loading cookies: {e}")
        return False


def restart_driver():
    """Restart the Chrome driver"""
    global _driver
    with _driver_lock:
        if _driver:
            try:
                _driver.quit()
            except:
                pass
            _driver = None
        log("Driver restarted")


def shutdown_driver():
    """Clean shutdown of Chrome driver"""
    global _driver, _shutdown_flag
    _shutdown_flag = True
    with _driver_lock:
        if _driver:
            try:
                # v4.8.2: Suppress WinError 6 noise (known UC artifact)
                _driver.quit()
            except Exception as e:
                # Capture but don't spam if it's just a dead handler
                if "WinError 6" not in str(e):
                    log(f"Driver shutdown info: {e}")
            _driver = None
    log("Driver shutdown complete")



def robust_type_and_verify(driver, element, text, tweet_index=0):
    """ULTRA-ROBUST TYPING v4.9.2
    - clipboard FIRST (en güvenilir yöntem Twitter için)
    - js_insert SADECE element-scoped selectNodeContents kullanır (selectAll YOK)
    - sendkeys fallback
    - React nudge: end-of-line key press (boşluk+backspace YOK — tüm modal'ı etkileyebilir)
    """
    import pyperclip
    from selenium.webdriver.common.keys import Keys
    from selenium.webdriver.common.action_chains import ActionChains

    # clipboard önce çünkü Twitter'ın contenteditable'ı paste event'ini en iyi işliyor
    methods = ["clipboard", "js_insert", "sendkeys"]

    for method in methods:
        try:
            # 1. Scroll + focus — SADECE bu element'e
            driver.execute_script(
                "arguments[0].scrollIntoView({behavior:'instant',block:'center'});", element)
            time.sleep(0.3)

            # 2. Element'e tıkla (focus)
            try:
                ActionChains(driver).move_to_element(element).click().perform()
            except Exception:
                driver.execute_script("arguments[0].click();", element)
            time.sleep(0.3)

            # 3. SADECE bu element'in içini temizle — selectAll KULLANMA
            driver.execute_script("""
                var el = arguments[0];
                el.focus();
                var range = document.createRange();
                range.selectNodeContents(el);
                var sel = window.getSelection();
                sel.removeAllRanges();
                sel.addRange(range);
                document.execCommand('delete', false, null);
            """, element)
            time.sleep(0.3)

            log(f"[TYPE] Part {tweet_index} method={method}")

            if method == "clipboard":
                pyperclip.copy(text)
                time.sleep(0.2)
                # Paste
                ActionChains(driver).key_down(Keys.CONTROL).send_keys('v').key_up(Keys.CONTROL).perform()
                time.sleep(0.5)
                # React nudge: End tuşu — metin değiştirmez, sadece cursor'u sona götürür
                ActionChains(driver).send_keys(Keys.END).perform()
                time.sleep(0.2)

            elif method == "js_insert":
                # selectAll YOK — sadece element-scoped insertText
                driver.execute_script("""
                    var el = arguments[0];
                    el.focus();
                    var range = document.createRange();
                    range.selectNodeContents(el);
                    var sel = window.getSelection();
                    sel.removeAllRanges();
                    sel.addRange(range);
                    document.execCommand('delete', false, null);
                    document.execCommand('insertText', false, arguments[1]);
                """, element, text)
                time.sleep(0.4)
                # React nudge: click + END
                try:
                    ActionChains(driver).move_to_element(element).click().perform()
                    ActionChains(driver).send_keys(Keys.END).perform()
                except Exception:
                    pass
                time.sleep(0.3)

            elif method == "sendkeys":
                element.send_keys(text)
                time.sleep(0.3)
                ActionChains(driver).send_keys(Keys.END).perform()

            # React sync bekle
            time.sleep(1.2)

            # Verify
            current_text = driver.execute_script(
                "return arguments[0].innerText;", element).strip()
            input_len = len(text.strip())
            current_len = len(current_text)

            if input_len > 0 and current_len >= input_len * 0.80:
                log(f"✅ Part {tweet_index} verified ({current_len}/{input_len}) via {method}")
                return True
            else:
                log(f"⚠️ Part {tweet_index} mismatch ({current_len}/{input_len}) via {method}, next method...")

        except Exception as e:
            log(f"❌ Method {method} failed for part {tweet_index}: {type(e).__name__}: {e}")

    log(f"FATAL: All methods failed for part {tweet_index}")
    return False


# ============== COMMAND HANDLERS ==============

def cmd_search(params):
    """Search for tweets"""
    query = params.get("query", "")
    market = params.get("market", "")
    limit = int(params.get("limit", 10))
    
    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}
    
    try:
        import urllib.parse
        encoded = urllib.parse.quote(query)
        driver.get(f"https://x.com/search?q={encoded}&src=typed_query&f=live")
        
        # Wait for tweets
        try:
            WebDriverWait(driver, 20).until(
                EC.presence_of_element_located((By.TAG_NAME, "article"))
            )
        except:
            return {"status": "success", "data": []}
        
        articles = driver.find_elements(By.CSS_SELECTOR, "article[data-testid='tweet']")
        results = []
        
        for art in articles[:limit]:
            try:
                # Extract text
                text = ""
                try:
                    content_el = art.find_element(By.CSS_SELECTOR, "[data-testid='tweetText']")
                    text = content_el.text
                except:
                    text = art.text[:500]
                
                if not text or len(text) < 10:
                    continue
                
                # Extract author
                author = ""
                try:
                    user_el = art.find_element(By.CSS_SELECTOR, "[data-testid='User-Names']")
                    handle_text = user_el.text
                    if "@" in handle_text:
                        author = "@" + handle_text.split("@")[1].split("\n")[0].split(" ")[0]
                except:
                    author = "X-User"
                
                # Extract URL and time
                url = ""
                time_str = ""
                try:
                    time_el = art.find_element(By.TAG_NAME, "time")
                    url = time_el.find_element(By.XPATH, "./..").get_attribute("href")
                    time_str = time_el.get_attribute("datetime")
                except:
                    url = "https://x.com"
                    time_str = datetime.now(timezone.utc).isoformat()
                
                # Extract image
                img_url = None
                try:
                    img_els = art.find_elements(By.CSS_SELECTOR, "img[src*='media']")
                    for img in img_els:
                        src = img.get_attribute("src")
                        if src and "profile_images" not in src:
                            img_url = src
                            break
                except:
                    pass
                
                # Extract engagement
                engagement = 0
                try:
                    like_el = art.find_element(By.CSS_SELECTOR, "[data-testid='like']")
                    engagement = int(''.join(filter(str.isdigit, like_el.text)) or '0')
                except:
                    pass
                
                results.append({
                    "author": author,
                    "content": text[:500],
                    "url": url,
                    "engagement": engagement,
                    "postDate": time_str,
                    "imageUrl": img_url
                })
            except Exception as e:
                log(f"Error parsing article: {e}")
        
        return {"status": "success", "data": results}
        
    except Exception as e:
        log(f"Search error: {e}")
        return {"status": "error", "message": str(e)}


def cmd_timeline(params):
    """Fetch tweets from a user's timeline"""
    handle = params.get("handle", "").lstrip("@")
    limit = int(params.get("limit", 10))
    
    if not handle:
        return {"status": "error", "message": "Handle required"}
    
    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}
    
    try:
        driver.get(f"https://x.com/{handle}")
        
        # Wait for tweets
        try:
            WebDriverWait(driver, 15).until(
                EC.presence_of_element_located((By.TAG_NAME, "article"))
            )
        except:
            return {"status": "success", "data": []}
        
        articles = driver.find_elements(By.CSS_SELECTOR, "article[data-testid='tweet']")
        results = []
        
        for art in articles[:limit]:
            try:
                text = ""
                try:
                    content_el = art.find_element(By.CSS_SELECTOR, "[data-testid='tweetText']")
                    text = content_el.text
                except:
                    text = art.text[:500]
                
                if not text or len(text) < 10:
                    continue
                
                url = ""
                time_str = ""
                try:
                    time_el = art.find_element(By.TAG_NAME, "time")
                    url = time_el.find_element(By.XPATH, "./..").get_attribute("href")
                    time_str = time_el.get_attribute("datetime")
                except:
                    url = f"https://x.com/{handle}"
                    time_str = datetime.now(timezone.utc).isoformat()
                
                img_url = None
                try:
                    img_els = art.find_elements(By.CSS_SELECTOR, "img[src*='media']")
                    for img in img_els:
                        src = img.get_attribute("src")
                        if src and "profile_images" not in src:
                            img_url = src
                            break
                except:
                    pass
                
                results.append({
                    "author": f"@{handle}",
                    "content": text[:500],
                    "url": url,
                    "postDate": time_str,
                    "imageUrl": img_url
                })
            except:
                pass
        
        return {"status": "success", "data": results}
        
    except Exception as e:
        log(f"Timeline error: {e}")
        return {"status": "error", "message": str(e)}


def cmd_find_handle(params):
    """Find Twitter handle via Google search"""
    name = params.get("name", "")
    
    if not name:
        return {"status": "error", "message": "Name required"}
    
    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}
    
    try:
        import urllib.parse
        query = f"{name} official twitter"
        driver.get(f"https://www.google.com/search?q={urllib.parse.quote(query)}")
        
        try:
            WebDriverWait(driver, 10).until(
                EC.presence_of_element_located((By.ID, "search"))
            )
        except:
            return {"status": "error", "message": "Google search failed"}
        
        # Look for Twitter/X links
        import re
        links = driver.find_elements(By.TAG_NAME, "a")
        for link in links:
            href = link.get_attribute("href") or ""
            if "twitter.com/" in href or "x.com/" in href:
                match = re.search(r'(?:twitter|x)\.com/(@?\w+)', href)
                if match:
                    handle = match.group(1)
                    if handle and handle.lower() not in ["search", "explore", "home", "i", "settings"]:
                        handle = handle if handle.startswith("@") else f"@{handle}"
                        log(f"Found handle: {handle} for {name}")
                        return {"status": "success", "handle": handle}
        
        return {"status": "error", "message": "Handle not found"}
        
    except Exception as e:
        log(f"Find handle error: {e}")
        return {"status": "error", "message": str(e)}


def cmd_like(params):
    """Like a tweet"""
    url = params.get("url", "")
    
    if not url or "/status/" not in url:
        return {"status": "error", "message": "Valid tweet URL required"}
    
    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}
    
    try:
        driver.get(url)
        time.sleep(2)
        
        # Find and click like button
        like_btn = WebDriverWait(driver, 10).until(
            EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='like']"))
        )
        driver.execute_script("arguments[0].click();", like_btn)
        time.sleep(1)
        
        return {"status": "success", "message": "Liked"}
        
    except Exception as e:
        log(f"Like error: {e}")
        return {"status": "error", "message": str(e)}


def _click_post_button(driver):
    """
    Robustly find the final 'Post All' / 'Tweet All' button.
    Returns element if found and enabled, None otherwise. v4.9.1.
    """
    # Selector priority list — X changes these frequently
    selectors = [
        "[data-testid='tweetButton']",
        "[data-testid='sendReplies']",
        "[data-testid='tweetButtonInline']",
    ]

    # Label-based fallback
    post_labels = [
        "hepsini paylaş", "tümünü paylaş", "tweet at", "paylaş",
        "post all", "tweet all", "post", "send"
    ]

    def _is_enabled(btn):
        """Check if button is truly enabled (React-safe check)"""
        try:
            aria_disabled = btn.get_attribute("aria-disabled")
            if aria_disabled == "true":
                return False
            # React disabled buttons often have specific class patterns
            class_attr = btn.get_attribute("class") or ""
            if "disabled" in class_attr.lower():
                return False
            # Check data-testid has no 'disabled' hint
            style = btn.get_attribute("style") or ""
            if "pointer-events: none" in style:
                return False
            return True
        except Exception:
            return False

    for sel in selectors:
        try:
            btns = driver.find_elements(By.CSS_SELECTOR, sel)
            for b in btns:
                try:
                    if b.is_displayed() and _is_enabled(b):
                        log(f"Post button found via selector: {sel}")
                        return b
                except Exception:  # StaleElementReferenceException
                    pass
        except Exception: pass

    # Label search
    try:
        all_btns = driver.find_elements(By.CSS_SELECTOR,
            "div[role='button'][aria-label], button[aria-label], div[role='button'], button[type='button']")
        for b in all_btns:
            try:
                if not b.is_displayed():
                    continue
                label = (b.get_attribute("aria-label") or b.text or "").lower()
                if any(k in label for k in post_labels) and _is_enabled(b):
                    log(f"Post button found via label: '{label}'")
                    return b
            except Exception:  # StaleElementReferenceException
                pass
    except Exception: pass

    return None


def _safe_click_element(driver, elem, label="element"):
    """
    Click an element with StaleElement protection: re-finds by testid if stale. v4.9.1.
    Returns True on success.
    """
    from selenium.webdriver.common.action_chains import ActionChains
    from selenium.common.exceptions import StaleElementReferenceException

    for attempt in range(3):
        try:
            # First try JS click (most reliable for React)
            driver.execute_script("arguments[0].click();", elem)
            log(f"{label} clicked via JS (attempt {attempt+1})")
            return True
        except StaleElementReferenceException:
            log(f"{label} stale on attempt {attempt+1}, re-finding...")
            # Re-find the button
            elem = _click_post_button(driver)
            if not elem:
                log(f"{label} re-find failed")
                return False
            time.sleep(0.5)
        except Exception as e:
            log(f"{label} JS click failed: {type(e).__name__}: {e}")
            # Try ActionChains as last resort
            try:
                ActionChains(driver).move_to_element(elem).click().perform()
                log(f"{label} clicked via ActionChains")
                return True
            except Exception as ae:
                log(f"{label} ActionChains failed: {type(ae).__name__}: {ae}")
                return False
    return False


def _post_single_tweet(driver, text, reply_to_url=None, media_path=None):
    """
    Post a single tweet, optionally as a reply.
    Returns the URL of the posted tweet, or None on failure.
    v4.9.2: Reply-chain approach — her tweet ayrı ayrı gönderilir.
    """
    from selenium.webdriver.common.action_chains import ActionChains
    import pyperclip

    try:
        if reply_to_url:
            # Reply: direkt reply URL'e git
            driver.get(reply_to_url)
            time.sleep(2.5)
            # Reply butonunu bul
            try:
                reply_btn = WebDriverWait(driver, 10).until(
                    EC.element_to_be_clickable((By.CSS_SELECTOR,
                        "[data-testid='reply'], [aria-label*='Reply'], [aria-label*='Yanıtla']"))
                )
                driver.execute_script("arguments[0].click();", reply_btn)
                log(f"Reply button clicked for: {reply_to_url}")
                time.sleep(2)
            except Exception as e:
                log(f"Reply button not found, trying compose URL: {e}")
                # Fallback: compose URL ile reply
                tweet_id = reply_to_url.rstrip("/").split("/")[-1]
                driver.get(f"https://x.com/intent/tweet?in_reply_to={tweet_id}")
                time.sleep(2.5)
        else:
            # İlk tweet: compose modal aç
            compose_opened = False
            try:
                sidenav_btn = WebDriverWait(driver, 5).until(
                    EC.element_to_be_clickable((By.CSS_SELECTOR,
                        "[data-testid='SideNav_NewTweet_Button'], [data-testid='tweetButtonInline']"))
                )
                driver.execute_script("arguments[0].click();", sidenav_btn)
                log("Compose opened via SideNav button")
                compose_opened = True
                time.sleep(2)
            except Exception:
                pass

            if not compose_opened:
                driver.get("https://x.com/compose/tweet")
                log("Compose opened via URL")
                time.sleep(3)

        # Textbox'ı bul
        try:
            box = WebDriverWait(driver, 15).until(
                EC.presence_of_element_located((By.CSS_SELECTOR,
                    "[data-testid='tweetTextarea_0'], div[role='textbox']"))
            )
        except Exception as e:
            log(f"Textbox not found: {e}")
            return None

        # Yaz
        if not robust_type_and_verify(driver, box, text, 0):
            log("Warning: text verify failed, continuing anyway...")

        # Medya yükle (sadece media_path verilmişse)
        if media_path and os.path.exists(media_path):
            try:
                file_input = driver.find_element(By.CSS_SELECTOR,
                    "input[data-testid='fileInput'], input[accept*='image'][type='file']")
                file_input.send_keys(media_path)
                log(f"Media uploaded: {media_path}")
                time.sleep(3)
            except Exception as e:
                log(f"Media upload failed (non-fatal): {e}")

        post_btn = None
        for attempt in range(8):
            post_btn = _click_post_button(driver)
            if post_btn:
                log(f"Post button ready (attempt {attempt+1})")
                break
            log(f"Post button not ready (attempt {attempt+1}/8), waiting...")
            time.sleep(2.5)

        if not post_btn:
            log("FATAL: Post button not found")
            return None

        try:
            driver.execute_script(
                "arguments[0].scrollIntoView({behavior:'instant',block:'center'});", post_btn)
            time.sleep(0.4)
        except Exception:
            pass

        if not _safe_click_element(driver, post_btn, "Post button"):
            log("Post button click failed")
            return None

        # Gönderilmesini bekle
        time.sleep(3)
        try:
            WebDriverWait(driver, 10).until(
                EC.invisibility_of_element_located((By.CSS_SELECTOR,
                    "[data-testid='tweetTextarea_0']"))
            )
            log("Compose dialog closed — tweet posted")
        except Exception:
            log("Dialog close timeout — continuing...")

        # ── Yeni tweet URL'sini al ────────────────────────────────────────────
        # Yöntem 1: current_url zaten /status/ID formatına geçmiş olabilir
        cur = driver.current_url
        if "/status/" in cur:
            log(f"Tweet URL captured from current_url: {cur}")
            return cur

        # Yöntem 2: Toast bildirimindeki "Görüntüle" linkini yakala
        # Twitter bazen compose kapandıktan sonra kısa süre bir toast gösterir.
        try:
            for _ in range(5):
                toast_links = driver.find_elements(By.CSS_SELECTOR,
                    "a[href*='/status/'], [data-testid='toast'] a[href*='/status/']")
                for lnk in toast_links:
                    href = lnk.get_attribute("href") or ""
                    if "/status/" in href:
                        if not href.startswith("http"):
                            href = "https://x.com" + href
                        log(f"Tweet URL captured from toast/DOM: {href}")
                        return href
                time.sleep(0.5)
        except Exception as e:
            log(f"Toast URL method failed: {e}")

        # Yöntem 3: Profil sayfasında retry döngüsüyle en yeni tweet — SADECE profil, HOME ASLA
        try:
            handle = driver.execute_script("""
                var el = document.querySelector('a[data-testid="AppTabBar_Profile_Link"]');
                return el ? el.getAttribute('href') : null;
            """)
            if handle:
                profile_url = f"https://x.com{handle}"
                driver.get(profile_url)
                handle_frag = handle.strip("/")

                # Yeni tweet'in profilde görünmesini beklemek için retry döngüsü
                for attempt in range(6):
                    time.sleep(2)
                    status_links = driver.find_elements(By.CSS_SELECTOR,
                        "article[data-testid='tweet'] a[href*='/status/']")
                    for lnk in status_links:
                        href = lnk.get_attribute("href") or ""
                        # Sadece kendi profilimize ait URL'yi döndür
                        if handle_frag in href and "/status/" in href:
                            if not href.startswith("http"):
                                href = "https://x.com" + href
                            log(f"Tweet URL captured from profile (attempt {attempt+1}): {href}")
                            return href
                    log(f"Profile retry {attempt+1}/6: tweet URL not yet visible, waiting...")
        except Exception as e:
            log(f"Profile URL method failed: {e}")

        # ── Güvenli hata: URL bulunamadı, x.com/home KULLANILMAZ ────────────
        # HOME üzerinden yapılan arama, zaman çizelgesindeki BAŞKA bir kullanıcının
        # tweetine yanlışlıkla yanıt verilmesine yol açar. Bu yüzden None döndürüyoruz.
        log("FATAL: Could not capture tweet URL after all attempts. Returning None to prevent wrong reply.")
        return None

    except Exception as e:
        log(f"_post_single_tweet error: {type(e).__name__}: {e}")
        return None


def cmd_post_thread(params):
    """
    Post a tweet thread — v4.9.2 REPLY-CHAIN APPROACH
    Her tweet ayrı ayrı gönderilir: 1. tweet normal, sonrakiler reply olarak.
    Bu yaklaşım Twitter'ın compose modal Add buton sorununu tamamen bypass eder.
    """
    tweets = params.get("tweets", [])
    media_path = params.get("media")

    if not tweets:
        return {"status": "error", "message": "Tweets required"}

    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}

    try:
        log(f"[Thread] Starting reply-chain approach: {len(tweets)} tweets")

        # ── 1. tweet: normal compose ─────────────────────────────────────────
        log(f"[Thread] Posting tweet 1/{len(tweets)}...")
        first_url = _post_single_tweet(driver, tweets[0], reply_to_url=None, media_path=media_path)

        if not first_url:
            log("FATAL: Could not post first tweet — first_url is None")
            return {"status": "error", "message": "First tweet failed"}

        # Güvenlik kontrolü: URL gerçek bir tweet sayfası mı?
        if "/status/" not in first_url:
            log(f"FATAL: First tweet URL is invalid ('{first_url}'). Thread aborted to prevent wrong reply.")
            return {"status": "error", "message": f"First tweet URL invalid: {first_url}"}

        log(f"[Thread] ✅ Tweet 1 posted: {first_url}")
        time.sleep(4)  # Twitter'ın indexlemesi için bekle

        # ── Kalan tweetler: reply olarak ─────────────────────────────────────
        last_url = first_url
        for i, tweet_text in enumerate(tweets[1:], 2):
            log(f"[Thread] Posting tweet {i}/{len(tweets)} as reply to: {last_url}")
            reply_url = _post_single_tweet(driver, tweet_text, reply_to_url=last_url)

            if not reply_url or "/status/" not in reply_url:
                # Geçersiz URL varsa son bilinen GEÇERLİ URL ile devam et (değil, durdur)
                log(f"ERROR: Tweet {i} reply URL is invalid ('{reply_url}'). Stopping thread to prevent wrong reply.")
                return {"status": "error", "message": f"Thread interrupted at tweet {i}: reply URL capture failed"}

            log(f"[Thread] ✅ Tweet {i} posted: {reply_url}")
            last_url = reply_url
            time.sleep(4)  # Her tweet arasında bekle

        log(f"✅ Thread posted successfully ({len(tweets)} parts) via reply-chain")
        return {"status": "success", "message": f"Thread posted ({len(tweets)} tweets)"}

    except Exception as e:
        log(f"Post thread FATAL error: {type(e).__name__}: {e}")
        return {"status": "error", "message": str(e)}


def cmd_reply(params):
    """Reply to a tweet"""
    url = params.get("url", "")
    text = params.get("text", "")
    
    if not url or "/status/" not in url:
        return {"status": "error", "message": "Valid tweet URL required"}
    if not text:
        return {"status": "error", "message": "Reply text required"}
    
    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}
    
    try:
        import pyperclip
        from selenium.webdriver.common.action_chains import ActionChains
        
        driver.get(url)
        time.sleep(3)
        
        # Verify we're on a tweet page
        if "/status/" not in driver.current_url:
            return {"status": "error", "message": "Failed to navigate to tweet"}
        
        # Find reply text area
        reply_box = WebDriverWait(driver, 20).until(
            EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetTextarea_0']"))
        )
        
        # Type reply
        pyperclip.copy(text)
        reply_box.click()
        time.sleep(0.3)
        ActionChains(driver).key_down(Keys.CONTROL).send_keys('v').key_up(Keys.CONTROL).perform()
        time.sleep(1)
        
        # Click Reply button
        reply_btn = WebDriverWait(driver, 10).until(
            EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetButtonInline']"))
        )
        driver.execute_script("arguments[0].click();", reply_btn)
        time.sleep(2)
        
        return {"status": "success", "message": "Reply posted"}
        
    except Exception as e:
        log(f"Reply error: {e}")
        return {"status": "error", "message": str(e)}


def cmd_fetch_news(params):
    """Fetch news from financial accounts"""
    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}
    
    try:
        # News sources
        sources = [
            "BloombergHT", "baborasi", "finansgundem", "enaborsa", 
            "yaborsa", "Faborsa", "UAEconomist"
        ]
        
        results = []
        for source in sources:
            try:
                driver.get(f"https://x.com/{source}")
                WebDriverWait(driver, 10).until(
                    EC.presence_of_element_located((By.TAG_NAME, "article"))
                )
                
                articles = driver.find_elements(By.CSS_SELECTOR, "article[data-testid='tweet']")
                for art in articles[:3]:  # Top 3 from each
                    try:
                        text = ""
                        try:
                            content_el = art.find_element(By.CSS_SELECTOR, "[data-testid='tweetText']")
                            text = content_el.text
                        except:
                            continue
                        
                        if not text or len(text) < 20:
                            continue
                        
                        time_str = ""
                        url = ""
                        try:
                            time_el = art.find_element(By.TAG_NAME, "time")
                            time_str = time_el.get_attribute("datetime")
                            url = time_el.find_element(By.XPATH, "./..").get_attribute("href")
                        except:
                            pass
                        
                        results.append({
                            "source": f"@{source}",
                            "text": text[:500],
                            "time": time_str,
                            "url": url
                        })
                    except:
                        pass
            except Exception as e:
                log(f"Error fetching from {source}: {e}")
        
        return {"status": "success", "data": results}
        
    except Exception as e:
        log(f"Fetch news error: {e}")
        return {"status": "error", "message": str(e)}


def cmd_health(params):
    """Health check"""
    global _driver, _last_activity
    return {
        "status": "ok",
        "driver_active": _driver is not None,
        "last_activity": _last_activity,
        "uptime": time.time() - _last_activity
    }


# ============== HTTP SERVER ==============

class DaemonHandler(BaseHTTPRequestHandler):
    """HTTP request handler for daemon"""
    
    def log_message(self, format, *args):
        # Suppress default logging
        pass
    
    def _send_json(self, data, status=200):
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(json.dumps(data).encode())
    
    def do_GET(self):
        path = urlparse(self.path).path
        
        if path == "/health":
            self._send_json(cmd_health({}))
        else:
            self._send_json({"error": "Not found"}, 404)
    
    def do_POST(self):
        path = urlparse(self.path).path
        
        # Read body
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length).decode() if content_length else "{}"
        
        try:
            params = json.loads(body) if body else {}
        except:
            params = {}
        
        # Route to handler
        handlers = {
            "/search": cmd_search,
            "/timeline": cmd_timeline,
            "/find_handle": cmd_find_handle,
            "/like": cmd_like,
            "/post_thread": cmd_post_thread,
            "/reply": cmd_reply,
            "/fetch_news": cmd_fetch_news,
            "/health": cmd_health,
            "/restart": lambda p: (restart_driver(), {"status": "ok"})[1],
            "/shutdown": lambda p: (shutdown_driver(), {"status": "shutdown"})[1],
        }
        
        handler = handlers.get(path)
        if handler:
            try:
                result = handler(params)
                self._send_json(result)
            except Exception as e:
                log(f"Handler error: {e}\n{traceback.format_exc()}")
                self._send_json({"status": "error", "message": str(e)}, 500)
        else:
            self._send_json({"error": "Not found"}, 404)


def run_server():
    """Run the HTTP server"""
    server = HTTPServer((HOST, PORT), DaemonHandler)
    log(f"X Daemon starting on http://{HOST}:{PORT}")
    
    try:
        while not _shutdown_flag:
            server.handle_request()
    except KeyboardInterrupt:
        log("Keyboard interrupt")
    finally:
        shutdown_driver()
        log("Server stopped")


if __name__ == "__main__":
    log("=" * 50)
    log("X Daemon v1.0")
    log("=" * 50)
    run_server()

