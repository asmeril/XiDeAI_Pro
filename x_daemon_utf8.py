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
from datetime import datetime, timezone
from pathlib import Path
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
        
        # Create UC driver with version fallback
        try:
            driver = uc.Chrome(options=options, use_subprocess=True)
        except Exception as driver_err:
            if "version" in str(driver_err).lower():
                log("Version mismatch detected in x_daemon, forcing version 145...")
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
                
                driver = uc.Chrome(options=options, use_subprocess=True, version_main=145)
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
                    log(f"Γ£à Heuristic Identity Generated: {use_hints['brands'][0]['brand']} v{use_hints['brands'][0]['version']}")

                ua_override = {}
                if use_ua: ua_override["userAgent"] = use_ua
                if use_hints: ua_override["userAgentMetadata"] = use_hints
                
                if ua_override:
                    driver.execute_cdp_cmd('Network.setUserAgentOverride', ua_override)
                    if use_hints: log(f"Γ£à Deep Identity Synced with Metadata")
                    else: log(f"Γ£à User-Agent Synced: {use_ua[:30]}...")

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
                        
                log(f"Γ£à JSON Session Synced: {added_count}/{len(cookies)} cookies loaded from WebView2")
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
                log("Γ¥î Cookie load FAILED: Redirected to Login Page")
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
                # Force kill for WinError 32 lock prevention
                import psutil
                try:
                    proc = psutil.Process(_driver.browser_pid)
                    for child in proc.children(recursive=True):
                        child.kill()
                    proc.kill()
                except: pass
                _driver.quit()
            except Exception as e:
                if "WinError 6" not in str(e) and "WinError 32" not in str(e):
                    log(f"Driver shutdown info: {e}")
            _driver = None
    log("Driver shutdown complete")



def robust_type_and_verify(driver, element, text, tweet_index=0):
    """ULTRA-ROBUST TYPING WITH JS FALLBACK (v4.6.6)"""
    import json
    import pyperclip
    from selenium.webdriver.common.keys import Keys
    
    # Try multiple methods (JS is most robust for Turkish chars / React)
    methods = ["js_insert", "clipboard", "sendkeys"]
    
    for method in methods:
        try:
            # 1. Clear box first (Force focus + select all)
            driver.execute_script("arguments[0].scrollIntoView({behavior: 'instant', block: 'center'});", element)
            time.sleep(0.3)
            driver.execute_script("arguments[0].focus();", element)
            time.sleep(0.2)
            
            # Use JS to clear if send_keys fails
            driver.execute_script("arguments[0].innerText = '';", element)
            element.send_keys(Keys.CONTROL, 'a')
            element.send_keys(Keys.BACKSPACE)
            time.sleep(0.5)
            
            log(f"[TYPE] Part {tweet_index} using {method}")
            
            if method == "js_insert":
                js_text = json.dumps(text)
                driver.execute_script(f"""
                    var elem = arguments[0];
                    elem.focus();
                    document.execCommand('insertText', false, {js_text});
                    elem.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    elem.dispatchEvent(new Event('change', {{ bubbles: true }}));
                """, element)
                
                # WAKE UP REACT: Send dummy key press to force state update 
                # (Prevents ghost/empty tweet error)
                time.sleep(0.3)
                try:
                    element.send_keys(" ")
                    time.sleep(0.1)
                    element.send_keys(Keys.BACKSPACE)
                except: pass
                
            elif method == "clipboard":
                pyperclip.copy(text)
                from selenium.webdriver.common.action_chains import ActionChains
                ActionChains(driver).key_down(Keys.CONTROL).send_keys('v').key_up(Keys.CONTROL).perform()
                
            elif method == "sendkeys":
                # Batch send
                element.send_keys(text)
            
            # VERIFICATION WAIT (Increased to 2s for React sync stability)
            time.sleep(2.0)
            
            # Visual verification: Check text length
            current_text = element.text.strip()
            input_len = len(text.strip())
            current_len = len(current_text)
            
            # If we have something close to input length, it's a success
            if input_len > 0 and current_len >= input_len * 0.9:
                log(f"Γ£à Part {tweet_index} verified ({current_len}/{input_len})")
                return True
            else:
                log(f"ΓÜá∩╕Å Part {tweet_index} length mismatch ({current_len}/{input_len})")
                
        except Exception as e:
            log(f"Γ¥î Method {method} failed: {e}")
            
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
                    from selenium.common.exceptions import StaleElementReferenceException
                    try:
                        text = art.text[:500]
                    except StaleElementReferenceException:
                        continue # Skip stale element safely
                
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


def cmd_post_thread(params):
    """Post a tweet thread"""
    tweets = params.get("tweets", [])
    media_path = params.get("media")
    
    if not tweets:
        return {"status": "error", "message": "Tweets required"}
    
    driver = get_driver()
    if not driver:
        return {"status": "error", "message": "Driver not available"}
    
    try:
        import pyperclip
        driver.get("https://x.com/compose/tweet")
        time.sleep(2)
        
        # Wait for tweet box
        tweet_box = WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='tweetTextarea_0'], [role='textbox']"))
        )
        
        # Type first tweet (Using robust engine)
        if not robust_type_and_verify(driver, tweet_box, tweets[0], 0):
            log("Warning: First tweet verification failed, continuing anyway...")
        time.sleep(1)
        
        # Add more tweets to thread (Robust v4.6.3 Engine)
        for i, tweet_text in enumerate(tweets[1:], 1):
            log(f"Processing thread part {i+1}/{len(tweets)}...")
            try:
                # 1. Count existing textboxes to verify spawn
                old_count = len(driver.find_elements(By.CSS_SELECTOR, "div[role='textbox']"))
                
                # 2. Find "Add another Tweet" button [Robust Selectors]
                add_btn = None
                
                # Try data-testid first
                try:
                    btns = driver.find_elements(By.CSS_SELECTOR, "[data-testid='addButton']")
                    for b in btns:
                        if b.is_displayed():
                            add_btn = b
                            break
                except: pass
                
                if not add_btn:
                    # Try labels (localized)
                    labels = ["Tweet ekle", "Add Tweet", "Add another Tweet", "Ba┼ƒka bir g├╢nderi ekle", "G├╢nderi ekle", "Add post", "Ekle", "Post ekle"]
                    # Simplified XPath for daemon efficiency
                    xpath = " | ".join([f"//*[@aria-label='{l}']" for l in labels])
                    try:
                        btns = driver.find_elements(By.XPATH, xpath)
                        for b in btns:
                            if b.is_displayed():
                                add_btn = b
                                break
                    except: pass
                
                if not add_btn:
                    # Fallback to SVG plus icon detection
                    try:
                        svg_btns = driver.find_elements(By.CSS_SELECTOR, "div[role='button']:has(svg), button:has(svg)")
                        for b in svg_btns:
                            if b.is_displayed():
                                # Heuristic: addButton usually has a plus-like SVG
                                add_btn = b
                                break
                    except: pass

                if add_btn:
                    driver.execute_script("arguments[0].scrollIntoView({behavior: 'instant', block: 'center'});", add_btn)
                    time.sleep(0.5)
                    driver.execute_script("arguments[0].click();", add_btn)
                    
                    # Wait for new box
                    try:
                        WebDriverWait(driver, 5).until(lambda d: len(d.find_elements(By.CSS_SELECTOR, "div[role='textbox']")) > old_count)
                        time.sleep(1)
                    except:
                        log("Warning: New box did not spawn after click.")
                else:
                    log(f"Warning: 'Add' button not found for part {i+1}. Attempting direct fallback...")
                
                # 3. Find new text area and type
                text_areas = driver.find_elements(By.CSS_SELECTOR, "div[role='textbox']")
                if len(text_areas) > i:
                    active_box = text_areas[i]
                    if not robust_type_and_verify(driver, active_box, tweet_text, i):
                        log(f"Warning: Part {i+1} verification failed.")
                    time.sleep(0.5)
                else:
                    log(f"CRITICAL: New textbox for part {i+1} NOT found after click.")
            except Exception as e:
                log(f"Error adding thread part {i+1}: {e}")
        
        # Click Post button
        time.sleep(1)
        post_btn = WebDriverWait(driver, 10).until(
            EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetButton']"))
        )
        driver.execute_script("arguments[0].click();", post_btn)
        time.sleep(3)
        
        return {"status": "success", "message": f"Thread posted ({len(tweets)} tweets)"}
        
    except Exception as e:
        log(f"Post thread error: {e}")
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
        # Fallback approach using intent URL
        tweet_id = url.rstrip("/").split("/")[-1]
        driver.get(f"https://x.com/intent/tweet?in_reply_to={tweet_id}")
        time.sleep(3)
        
        # Wait for the modal textarea
        reply_box = WebDriverWait(driver, 20).until(
            EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetTextarea_0']"))
        )
        
        # Type reply using robust method
        success = robust_type_and_verify(driver, reply_box, text, tweet_index=0)
        time.sleep(1)
        
        if not success:
            return {"status": "error", "message": "Reply interact fail: robust_type_and_verify failed"}
        
        # Click Reply/Post button inside the modal
        reply_btn = WebDriverWait(driver, 10).until(
            EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetButton']"))
        )
        driver.execute_script("arguments[0].click();", reply_btn)
        time.sleep(2)
        
        return {"status": "success", "message": "Reply posted"}
        
    except Exception as e:
        log(f"Reply error: {e}")
        return {"status": "error", "message": f"Reply interact fail: {str(e)}"}


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
