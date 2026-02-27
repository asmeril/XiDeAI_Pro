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
        
        # Create UC driver
        driver = uc.Chrome(options=options, use_subprocess=True)
        
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
        
        # FIX: Refresh to apply cookies
        try:
            driver.refresh()
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
                _driver.quit()
            except:
                pass
            _driver = None
    log("Driver shutdown complete")


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
        
        # Type first tweet
        try:
            pyperclip.copy(tweets[0])
            tweet_box.click()
            time.sleep(0.3)
            from selenium.webdriver.common.action_chains import ActionChains
            ActionChains(driver).key_down(Keys.CONTROL).send_keys('v').key_up(Keys.CONTROL).perform()
        except Exception as paste_err:
            log(f"Clipboard paste failed, falling back to send_keys: {paste_err}")
            tweet_box.clear()
            tweet_box.send_keys(tweets[0])
        time.sleep(1)
        
        # Add more tweets to thread
        for i, tweet_text in enumerate(tweets[1:], 1):
            # Click "Add another Tweet" button
            try:
                add_btn = driver.find_element(By.CSS_SELECTOR, "[data-testid='addButton'], [aria-label*='Add']")
                driver.execute_script("arguments[0].click();", add_btn)
                time.sleep(1)
                
                # Find new text area
                text_areas = driver.find_elements(By.CSS_SELECTOR, "[data-testid^='tweetTextarea_'], [role='textbox']")
                if len(text_areas) > i:
                    try:
                        pyperclip.copy(tweet_text)
                        text_areas[i].click()
                        time.sleep(0.3)
                        ActionChains(driver).key_down(Keys.CONTROL).send_keys('v').key_up(Keys.CONTROL).perform()
                    except Exception as paste_err:
                         log(f"Clipboard paste failed for reply {i}, falling back to send_keys: {paste_err}")
                         text_areas[i].send_keys(tweet_text)
                    time.sleep(0.5)
            except Exception as e:
                log(f"Error adding tweet {i+1}: {e}")
        
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
