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
    def get(cls, headless=True, use_undetected=False):
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

def _create_driver_internal(headless=True, use_undetected=False):
    """Internal driver creation (moved from setup_driver)"""
    ensure_dirs()
    
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
            
            driver = uc.Chrome(options=options, version_main=None, headless=headless)
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
    options.add_argument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
    
    # OPTIMIZATION: Fast page load (images enabled; blocking breaks X UI/media previews)
    options.page_load_strategy = 'eager'
    
    driver = None
    try:
        if HAS_WDM:
            driver = webdriver.Chrome(service=Service(ChromeDriverManager().install()), options=options)
        else:
            driver = webdriver.Chrome(options=options)
        
        driver.execute_cdp_cmd('Network.setUserAgentOverride', {
            "userAgent": 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
        })

        try:
            driver.set_page_load_timeout(25)
        except Exception:
            pass
        
        return driver
    except Exception as e:
        print(f"Chrome error: {e}", file=sys.stderr)
        return None

# App data directory
APPDATA_DIR = Path(os.environ.get('LOCALAPPDATA', os.path.expanduser('~'))) / "XiDeAI"
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
    "kutlu olsun", "mübarek olsun", "futbol", "gol", "maç sonucu", "penaltı", "fenerbahçe", "galatasaray", 
    "beşiktaş", "şampiyonlık", "kombine", "istifa", "ayet", "sure", "bakara", "allah", "amin", "siyaset", 
    "bakan", "parti", "chp", "akp", "mhp", "belediye", "başkan", "ziyaret", "hayırlı", "düğün", "nişan", 
    "açılış", "teşkilat", "muhtar"
]

TECHNICAL_KEYWORDS = [
    "grafik", "analiz", "teknik", "rsi", "macd", "direnç", "destek", "kırılım", "hedef", 
    "mum", "formasyon", "fibo", "pivot", "borsa", "endeks", "hisse", "finans", "trade", "chart"
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
    
    # 1. Blacklist check
    for kw in BLACKLIST_KEYWORDS:
        if kw.upper() in text_upper:
            return -1000
            
    # 2. Symbol match (+50)
    has_symbol = False
    if symbol_hint:
        sym = symbol_hint.upper().replace("#", "").replace("$", "")
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

def setup_driver(headless=True, use_undetected=False):
    """
    DEPRECATED: Use ChromeDriverPool.get() instead for better performance.
    Kept for backward compatibility with interactive login.
    """
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
    """Load cookies from pickle file if exists"""
    if not COOKIES_FILE.exists():
        return False
    try:
        driver.get("https://x.com/home") # Must be on domain to set cookies
        cookies = pickle.load(open(COOKIES_FILE, "rb"))
        # Removed debug logs - only log errors
        for idx, cookie in enumerate(cookies):
            try:
                if 'sameSite' in cookie:
                    if cookie['sameSite'] not in ["Strict", "Lax", "None"]:
                        del cookie['sameSite']
                driver.add_cookie(cookie)
            except Exception as ce:
                print(f"[COOKIE-ERROR] {idx+1}/{len(cookies)}: {cookie.get('name','?')} eklenemedi: {ce}", file=sys.stderr)
        return True
    except Exception as e:
        print(f"[COOKIE-ERROR] Genel hata: {e}", file=sys.stderr)
        return False

def login_interactive():
    """Opens visible browser for user to login manually"""
    print("Opening browser for login...")
    driver = setup_driver(headless=False)
    if not driver:
        return {"status": "error", "message": "Failed to start driver"}

    try:
        driver.get("https://x.com/i/flow/login")
        
        print("Waiting for login... (Time limit: 120s)")
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

def find_influencer_posts(query, market):
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
            posts = find_influencer_tweets_from_timeline(vip_handles, symbol_hint, market)
        else:
            posts = search_influencer_feed(search_q, symbol_hint)
            
        # Sort by relevance score
        if isinstance(posts, list):
            posts = [p for p in posts if p.get("relevance_score", 0) > -500]
            posts.sort(key=lambda x: x.get("relevance_score", 0), reverse=True)
            
        return json.dumps({"status": "success", "data": posts})
    except Exception as e:
        print(f"❌ find_influencer_posts error: {e}", file=sys.stderr)
        return json.dumps({"status": "error", "message": str(e), "data": []})

def find_influencer_tweets_from_timeline(vip_handles, symbol_query, market):
    """Fetch tweets from VIP timelines when query includes from: handles"""
    if not COOKIES_FILE.exists():
        return []

    driver = setup_driver(headless=True, use_undetected=True)
    if not driver:
        return []

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
                timeline_url = f"https://x.com/{handle_clean}"
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

                # DEBUG LOGGING SETUP
                debug_path = APPDATA_DIR / "debug_social_intel.txt"
                def log_debug(msg):
                    try:
                        with open(debug_path, "a", encoding="utf-8") as f:
                            f.write(f"[{datetime.now().strftime('%H:%M:%S')}] {msg}\n")
                    except: pass
                
                log_debug(f"--- START SCAN FOR {handle} ---")

                # CRITICAL: Parse tweets BEFORE scrolling to capture newest ones at top
                def parse_visible_tweets(round_idx):
                    parsed = []
                    tweets = driver.find_elements(By.TAG_NAME, "article")
                    log_debug(f"Round {round_idx}: Found {len(tweets)} article elements")
                    
                    for idx, tweet in enumerate(tweets):
                        try:
                            # Log raw text for debugging
                            raw_text = tweet.text.replace("\n", " ")[:100]
                            
                            text = tweet.text.replace("\n", " ")
                            if len(text) < 10:
                                log_debug(f"  Tweet {idx}: SKIPPED (Too short: {len(text)})")
                                continue
                            
                            # Parse URL first
                            url = ""
                            try:
                                link = tweet.find_element(By.CSS_SELECTOR, "a[href*='/status/']")
                                url = link.get_attribute("href")
                            except Exception:
                                pass
                            
                            # Skip if already seen (by URL)
                            if url and any(r.get("url") == url for r in results):
                                log_debug(f"  Tweet {idx}: SKIPPED (Duplicate URL: {url})")
                                continue
                            
                            # Parse time
                            time_str = ""
                            try:
                                time_el = tweet.find_element(By.TAG_NAME, "time")
                                time_str = time_el.get_attribute("datetime")
                            except Exception:
                                pass
                            
                            # Find images
                            img_url = None
                            try:
                                img_els = tweet.find_elements(By.CSS_SELECTOR, "img[src*='media']")
                                for img in img_els:
                                    src = img.get_attribute("src")
                                    if src and "profile_images" not in src:
                                        img_url = src
                                        break
                            except: pass

                            # SCORING FILTER - PASS has_image flag
                            score = calculate_relevance_score(text, symbol_query, has_image=(img_url is not None))
                            log_debug(f"  Tweet {idx}: Score={score} | Img={img_url is not None} | Date={time_str} | Txt={raw_text}...")
                            
                            if score < 10:
                                log_debug(f"  Tweet {idx}: REJECTED (Score {score} < 10)")
                                continue
                            
                            log_debug(f"  Tweet {idx}: ACCEPTED ✅ (URL: {url})")
                            parsed.append({
                                "author": f"@{handle_clean}",
                                "content": text[:500],
                                "url": url,
                                "engagement": 0,
                                "relevance_score": score,
                                "postDate": time_str or datetime.now(timezone.utc).isoformat(),
                                "imageUrl": img_url
                            })
                        except Exception as tweet_err:
                            print(f"Tweet parse error: {tweet_err}", file=sys.stderr)
                            log_debug(f"  Tweet {idx}: ERROR {tweet_err}")
                    return parsed
                
                # STEP 1: Parse TOP tweets first (newest are at top)
                time.sleep(2)  # Wait for initial load
                results.extend(parse_visible_tweets(0))
                print(f"DEBUG: Found {len(results)} tweets before scroll", file=sys.stderr)
                
                # STEP 2: Scroll and get more if needed
                for i in range(2):
                    if len(results) >= 10:
                        break
                    driver.execute_script("window.scrollBy(0, window.innerHeight);")
                    time.sleep(1.5)
                    results.extend(parse_visible_tweets(i+1))
                
                log_debug(f"--- END SCAN FOR {handle} : Total {len(results)} collected ---")

                # Sort by date (newest first) using postDate
                results.sort(key=lambda x: x.get("postDate", ""), reverse=True)
                
                if len(results) >= 10:
                    break
            except Exception as timeline_err:
                print(f"Timeline load error for {handle}: {timeline_err}", file=sys.stderr)
        print(f"✅ Timeline method found {len(results)} posts", file=sys.stderr)
        return results[:10]  # Return top 10 newest
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
                    if "@" in handle_text:
                        handle = "@" + handle_text.split("@")[1].split("\n")[0].split(" ")[0]
                    else:
                        handle = handle_text.split("\n")[0]
                except Exception:
                    try:
                        # Priority 2: Direct link to profile
                        links = art.find_elements(By.CSS_SELECTOR, "a[role='link']")
                        for link in links:
                            href = link.get_attribute("href") or ""
                            if "status" not in href and "x.com/" in href:
                                handle = "@" + href.split("x.com/")[1].split("/")[0]
                                break
                    except:
                        handle = "X-User"
                
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
                    digits = ''.join([c for c in like_el.text if c.isdigit()])
                    engagement = int(digits) if digits else 0
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
            
            # Robust Text Entry
            import json
            js_text = json.dumps(text, ensure_ascii=True)
            
            driver.execute_script(f"""
                const editor = document.querySelector("[data-testid='tweetTextarea_0']");
                if (editor) {{
                    editor.focus();
                    const text = {js_text};
                    document.execCommand('insertText', false, text);
                }}
            """)
            time.sleep(1.5)
            
            # Click Tweet button using State Verification
            tweet_btn = WebDriverWait(driver, 10).until(
                EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetButton']"))
            )
            
            # Use JS Click for better reliability
            driver.execute_script("arguments[0].scrollIntoView(true);", tweet_btn)
            time.sleep(0.5)
            driver.execute_script("arguments[0].click();", tweet_btn)
            
            # Verify closure or confirmation
            time.sleep(5)
            return {"status": "success", "message": "Tweet posted successfully!"}
            
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
        
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    try:
        if not load_cookies(driver): return {"status": "error", "message": "Cookie fail"}
        
        driver.get(tweet_url)
        
        # Calculate reply box selector
        # Usually it's the main draft editor.
        try:
            # Click the "Reply" text area (placeholder usually says 'Post your reply')
            reply_area = WebDriverWait(driver, 15).until(
                EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetTextarea_0']"))
            )
            reply_area.click()
            
            # Use JavaScript to set text (handles emoji/non-BMP characters)
            # using json.dumps(ensure_ascii=True) guarantees ONLY ASCII chars are sent to driver
            import json
            js_text = json.dumps(text, ensure_ascii=True)
            
            driver.execute_script(f"""
                const editor = document.querySelector("[data-testid='tweetTextarea_0']");
                if (editor) {{
                    editor.focus();
                    const text = {js_text};
                    document.execCommand('insertText', false, text);
                }}
            """)
            time.sleep(0.5)
            
            # Click Reply button
            reply_btn = WebDriverWait(driver, 5).until(
                EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetButton']"))
            )
            reply_btn.click()
            
            time.sleep(3)
            return {"status": "success", "message": "Reply sent!"}
            
        except Exception as e:
             return {"status": "error", "message": f"Reply interact fail: {str(e)}"}
             
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
    """Scrape BIST100, USD, EUR, Gold data"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    # Using Google Finance or a stable source for basic rates
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error"}
    
    data = {}
    try:
        # BIST 100
        driver.get("https://www.google.com/finance/quote/XU100:INDEXBIST?hl=tr")
        time.sleep(2)
        try:
            val = WebDriverWait(driver, 10).until(EC.presence_of_element_located((By.CSS_SELECTOR, ".YMlKec.fxKbKc, .YMlKec"))).text
            data["BIST100"] = val
        except: data["BIST100"] = "N/A"

        # USD/TRY
        driver.get("https://www.google.com/finance/quote/USD-TRY?hl=tr")
        time.sleep(2)
        try:
            val = WebDriverWait(driver, 10).until(EC.presence_of_element_located((By.CSS_SELECTOR, ".YMlKec.fxKbKc, .YMlKec"))).text
            data["USD"] = val
        except: data["USD"] = "N/A"

        # EUR/TRY
        driver.get("https://www.google.com/finance/quote/EUR-TRY?hl=tr")
        time.sleep(2)
        try:
            val = WebDriverWait(driver, 10).until(EC.presence_of_element_located((By.CSS_SELECTOR, ".YMlKec.fxKbKc, .YMlKec"))).text
            data["EUR"] = val
        except: data["EUR"] = "N/A"
        
        # Gram Gold
        try:
             driver.get("https://www.google.com/search?q=gram+altın+fiyatı")
             val = WebDriverWait(driver, 5).until(EC.presence_of_element_located((By.CSS_SELECTOR, "span.IsqQVc.NprOob, .SwHCTb"))).text
             data["Gold"] = val
        except: 
             data["Gold"] = "N/A"

        # Gümüş (Silver)
        try:
            driver.get("https://www.google.com/search?q=gümüş+gram+fiyatı")
            val = WebDriverWait(driver, 5).until(EC.presence_of_element_located((By.CSS_SELECTOR, "span.IsqQVc.NprOob, .SwHCTb"))).text
            data["Silver"] = val
        except:
            data["Silver"] = "N/A"

        return {"status": "success", "data": data}

    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def post_thread_chain(tweets, media_path=None):
    """Post a chain of tweets (thread) using compose modal's 'Add another post' button"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC

    # Fresh driver for posting to avoid stale state from prior scrapes
    ChromeDriverPool.close()
    
    if not tweets: return {"status": "error", "message": "No tweets provided"}
    
    # -------------------------------------------------------------
    # SMART SPLIT LOGIC
    # -------------------------------------------------------------
    def smart_split_text(text, limit=4000): # Updated for Premium/Blue (was 280)
        if len(text) <= limit: return [text]
        parts = []
        words = text.split(' ')
        current_chunk = ""
        for word in words:
            if len(current_chunk) + len(word) + 1 > limit:
                parts.append(current_chunk.strip())
                current_chunk = word + " "
            else:
                current_chunk += word + " "
        if current_chunk: parts.append(current_chunk.strip())
        return parts

    final_tweets = []
    for i, t in enumerate(tweets):
        # Only split if REALLY long (Premium limit)
        if len(t) > 4000:
            print(f"Warning: Tweet {i} is extraordinarily long ({len(t)} chars). Splitting automatically.", file=sys.stderr)
            chunks = smart_split_text(t, limit=4000)
            final_tweets.extend(chunks)
        else:
            final_tweets.append(t)
    
    tweets = final_tweets
    # -------------------------------------------------------------

    if not COOKIES_FILE.exists():
        return {"status": "error", "message": "No cookies found."}
        
    start_ts = time.time()
    def elapsed():
        return time.time() - start_ts

    print(f"DEBUG: post_thread_chain start | tweets={len(tweets)} | media={media_path}", file=sys.stderr); sys.stderr.flush()
    driver = setup_driver(headless=False)
    if not driver: return {"status": "error", "message": "Failed to start driver"}

    try:
        if not load_cookies(driver):
             return {"status": "error", "message": "Failed to load cookies"}

        print("DEBUG: Navigating to compose...", file=sys.stderr); sys.stderr.flush()
        try:
            driver.get("https://x.com/compose/tweet")
        except Exception as nav_err:
            print(f"Navigation timeout, stopping load: {nav_err}", file=sys.stderr)
            try:
                driver.execute_script("window.stop();")
            except Exception:
                pass
        time.sleep(2)

        if elapsed() > 60:
            return {"status": "error", "message": "Timeout before compose load"}
        
        try:
            # Initial wait for first box
            # Huge wait for initial load (sometimes X is slow or shows spinner)
            print("Waiting for tweet box...", file=sys.stderr)
            print("DEBUG: Waiting for first tweet box...", file=sys.stderr); sys.stderr.flush()
            tweet_box = WebDriverWait(driver, 30).until(
                EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='tweetTextarea_0'], [role='textbox'], [class*='public-DraftEditor-content']"))
            )
            print("DEBUG: First tweet box found", file=sys.stderr); sys.stderr.flush()
            time.sleep(2) # Extra buffer

            if elapsed() > 60:
                return {"status": "error", "message": "Timeout before typing"}
            
            # 0. Upload Media (if provided) to the FIRST tweet (graceful fallback)
            if media_path:
                try:
                    if os.path.exists(media_path):
                        abs_path = os.path.abspath(media_path)

                        # Try multiple selectors (Twitter UI changes frequently)
                        selectors = [
                            "input[type='file']",
                            "input[data-testid='fileInput']",
                            "input[accept*='image']",
                            "div[data-testid='toolBar'] input[type='file']"
                        ]
                        file_input = None
                        for sel in selectors:
                            try:
                                file_input = WebDriverWait(driver, 20).until(
                                    EC.presence_of_element_located((By.CSS_SELECTOR, sel))
                                )
                                if file_input:
                                    break
                            except Exception:
                                continue
                        if not file_input:
                            raise Exception("File input not found")

                        # Force visible before send_keys (hidden inputs can ignore send_keys)
                        try:
                            driver.execute_script("arguments[0].style.display='block'; arguments[0].removeAttribute('hidden');", file_input)
                        except Exception:
                            pass

                        file_input.send_keys(abs_path)

                        # Wait for media to load (look for attachments)
                        print("DEBUG: Uploading media...", file=sys.stderr); sys.stderr.flush()
                        WebDriverWait(driver, 40).until(
                            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='attachments'], [data-testid='mediaAttachment']"))
                        )
                        print("DEBUG: Media uploaded", file=sys.stderr); sys.stderr.flush()
                        time.sleep(2)
                    else:
                        print(f"Media path not found, posting without image: {media_path}", file=sys.stderr)
                except Exception as media_err:
                    # Continue without media instead of failing the whole thread
                    print(f"Media upload warning (skip image): {str(media_err)}", file=sys.stderr)

            # ---------------------------------------------------------
            # ULTRA-ROBUST TYPING WITH BUTTON STATE CHECK
            # ---------------------------------------------------------
            def robust_type_and_verify(element, text, tweet_index):
                # LOG TEXT STATS
                print(f"DEBUG: Tweet {tweet_index} text length: {len(text)} chars", file=sys.stderr)
                
                # ATOMIC CLEANUP FUNCTION
                def atomic_clear(elem):
                    try:
                        elem.click()
                        driver.execute_script("arguments[0].focus();", elem)
                        time.sleep(0.2)
                        
                        # 1. Select All + Backspace (Standard)
                        elem.send_keys(Keys.CONTROL, 'a')
                        time.sleep(0.1)
                        elem.send_keys(Keys.BACKSPACE)
                        
                        # 2. Hard JS Clear (React Reset)
                        driver.execute_script("""
                            var e = arguments[0];
                            e.value = '';
                            e.innerText = '';
                            e.textContent = '';
                            document.execCommand('delete');
                        """, elem)
                        time.sleep(0.5)
                        
                        # 3. Check emptiness
                        if len(elem.text.strip()) > 0:
                            print("Warning: Element not empty after clean. Forcing delete loop...", file=sys.stderr)
                            for _ in range(20): # Force delete loop
                                elem.send_keys(Keys.BACKSPACE)
                                if len(elem.text.strip()) == 0: break
                    except: pass

                methods = ["clipboard", "sendkeys", "js_insert"]
                
                for attempt, method in enumerate(methods):
                    # ALWAYS CLEAN BEFORE TYPING
                    atomic_clear(element)
                    
                    print(f"Typing tweet {tweet_index} using {method}...", file=sys.stderr)
                    
                    if method == "clipboard":
                        try:
                            import pyperclip
                            pyperclip.copy(text)
                            element.send_keys(Keys.CONTROL, 'v')
                        except: pass
                    
                    elif method == "sendkeys":
                        try:
                             # Batch send (faster than char-by-char)
                            element.send_keys(text)
                        except: pass

                    elif method == "js_insert":
                        try:
                            import json
                            js_text = json.dumps(text)
                            driver.execute_script(f"""
                                var elem = arguments[0];
                                elem.focus();
                                document.execCommand('insertText', false, {js_text});
                                elem.dispatchEvent(new Event('input', {{ bubbles: true }}));
                                elem.dispatchEvent(new Event('change', {{ bubbles: true }}));
                            """, element)
                            
                            # WAKE UP REACT: Send dummy key press to force state update
                            # This fixes the "Ghost Text" issue where placeholder remains visible
                            time.sleep(0.2)
                            try:
                                element.send_keys(" ")
                                time.sleep(0.1)
                                element.send_keys(Keys.BACKSPACE)
                            except: pass
                            
                        except: pass
                    
                    # VERIFICATION WAIT
                    time.sleep(3.0) 
                    
                    # VISUAL VERIFICATION: Check length ratio
                    current_text = element.text.strip()
                    input_len = len(text.strip())
                    current_len = len(current_text)
                    
                    # If duplication occurred, length will be roughly 2x (or > 1.5x)
                    if input_len > 50 and current_len > input_len * 1.5:
                         print(f"DUPLICATION DETECTED! Input: {input_len}, Current: {current_len}. Retrying...", file=sys.stderr)
                         continue # Fail this method, try next (which triggers clean)
                    
                    # SUCCESS CHECK: Is button enabled?
                    try:
                        btn = driver.find_element(By.CSS_SELECTOR, "[data-testid='tweetButton']")
                        if btn.is_enabled() and btn.get_attribute("aria-disabled") != "true":
                            print(f"Success! Tweet button enabled after {method}.", file=sys.stderr)
                            return True
                    except: pass
                    
                    # Fallback text check
                    if current_len > 5: return True
                
                return False

            # Post Loop
            for i, tweet_text in enumerate(tweets):
                # PACING: Slow down for human-like behavior
                print(f"Processing tweet {i}...", file=sys.stderr); sys.stderr.flush()
                time.sleep(2.0)

                if elapsed() > 80:
                    return {"status": "error", "message": f"Timeout while typing tweet {i}"}

                # 1. Add "Plus" button if 2nd+ tweet
                if i > 0:
                    try:
                        # FORCE SCROLL DOWN before looking for buttons
                        try:
                            driver.find_element(By.TAG_NAME, 'body').send_keys(Keys.PAGE_DOWN)
                            time.sleep(0.5)
                        except: pass

                        # Count existing textboxes
                        existing_boxes = driver.find_elements(By.CSS_SELECTOR, "div[role='textbox']")
                        old_count = len(existing_boxes)

                        target_btn = None
                        
                        # 1. Try strict ID (Best case)
                        try:
                            btns = driver.find_elements(By.CSS_SELECTOR, "[data-testid='addButton']")
                            for btn in btns:
                                if btn.is_displayed():
                                    target_btn = btn
                                    print("Found Add Button via data-testid='addButton'", file=sys.stderr)
                                    break
                        except: pass
                        
                        # 2. Try Exact Localized Labels (Expanded)
                        if not target_btn:
                            # Strict list of valid labels for the (+) button
                            # Added English/Turkish variations
                            valid_labels = ["Tweet ekle", "Add Tweet", "Add another Tweet", "Başka bir gönderi ekle", "Gönderi ekle", "Zincir ekle", "Add", "Ekle"]
                            
                            # Construct XPath for EXACT match or STARTS WITH to be safer
                            xpath_parts = [f"@aria-label='{label}'" for label in valid_labels]
                            xpath_join = " or ".join(xpath_parts)
                            xpath = f"//div[@role='button'][{xpath_join}] | //button[{xpath_join}]"
                            
                            try:
                                btns = driver.find_elements(By.XPATH, xpath)
                                for btn in btns:
                                    if btn.is_displayed():
                                        label = btn.get_attribute("aria-label").lower()
                                        
                                        # NUCLEAR EXCLUSION LIST: Ban all other toolbar icons
                                        # "Medya", "Fotoğraf", "Video", "GIF", "Anket", "Emoji", "Planla", "Konum"
                                        forbidden_terms = [
                                            "medya", "media", 
                                            "fotoğraf", "photo", 
                                            "video", 
                                            "gif", 
                                            "anket", "poll", 
                                            "emoji", 
                                            "planla", "schedule", 
                                            "konum", "location", 
                                            "kalın", "bold", 
                                            "italik", "italic",
                                            "liste", "list"
                                        ]
                                        
                                        if any(term in label for term in forbidden_terms):
                                            print(f"Ignoring toolbar button: {label}", file=sys.stderr)
                                            continue
                                            
                                        target_btn = btn
                                        print(f"Found Add Button via label: {label}", file=sys.stderr)
                                        break
                            except: pass

                        if target_btn:
                            # Scroll and Click
                            driver.execute_script("arguments[0].scrollIntoView({behavior: 'instant', block: 'center'});", target_btn)
                            time.sleep(1.0)
                            driver.execute_script("arguments[0].click();", target_btn)
                            time.sleep(2.0) # Wait for animation/modal expansion
                        else:
                            print("Warning: 'Plus' (+) button not found with STRICT selectors! Threading might fail.", file=sys.stderr)
                        
                        # WAIT & VERIFY Logic (10s timeout)
                        box_spawned = False
                        try:
                            WebDriverWait(driver, 10).until(
                                lambda d: len(d.find_elements(By.CSS_SELECTOR, "div[role='textbox']")) > old_count
                            )
                            box_spawned = True
                            print(f"New tweet box spawned. Total: {old_count + 1}", file=sys.stderr)
                        except:
                            print("Warning: Timed out waiting for new textbox.", file=sys.stderr)
                        
                        if not box_spawned:
                            print("CRITICAL: Failed to create new tweet box! Retrying click...", file=sys.stderr)
                            # ONE LAST TRY
                            try:
                                if target_btn: 
                                    driver.execute_script("arguments[0].click();", target_btn)
                                    time.sleep(3.0)
                                    if len(driver.find_elements(By.CSS_SELECTOR, "div[role='textbox']")) > old_count:
                                        print("Recovery successful! Box spawned.", file=sys.stderr)
                                        box_spawned = True
                            except: pass

                        if not box_spawned:
                            print("ABORTING THREAD: Cannot spawn new box. Preventing overwrite.", file=sys.stderr)
                            break # Dangerous to continue, we might overwrite previous tweet

                    except Exception as e:
                        print(f"Error clicking Add button: {e}", file=sys.stderr)

                # 2. Find the active box for THIS tweet
                try:
                    # Universal Selector logic
                    time.sleep(1.0)
                    textboxes = driver.find_elements(By.CSS_SELECTOR, "div[role='textbox']")
                    
                    if not textboxes:
                        print(f"Error: No textboxes found for tweet {i}!", file=sys.stderr)
                        active_box = driver.switch_to.active_element
                    else:
                        if len(textboxes) <= i:
                             print(f"CRITICAL ERROR: New textbox for tweet {i} not found. Skipping.", file=sys.stderr)
                             continue
                        active_box = textboxes[i]
                    
                    # FORCE SCROLL TO ACTIVE BOX
                    driver.execute_script("arguments[0].scrollIntoView({behavior: 'instant', block: 'center'});", active_box)
                    time.sleep(0.5)
                    # Extra nudge for sticky headers
                    try:
                        active_box.send_keys(Keys.PAGE_DOWN) 
                        time.sleep(0.2)
                    except: pass
                    
                    # TYPE AND VERIFY
                    success = robust_type_and_verify(active_box, tweet_text, i)
                    
                    if not success:
                         print(f"Warning: Failed to verify entry for tweet {i}", file=sys.stderr)
                    
                except Exception as e:
                     print(f"Error handling tweet {i}: {e}", file=sys.stderr)

            # 3. Post All
            try:
                tweet_btn = WebDriverWait(driver, 5).until(
                    EC.element_to_be_clickable((By.CSS_SELECTOR, "[data-testid='tweetButton']"))
                )
                
                # Check directly attribute
                is_disabled = tweet_btn.get_attribute("aria-disabled")
                if is_disabled == "true":
                     print("Final check: Button is disabled. Attempting one last check...", file=sys.stderr)
                     # Maybe focus the last box again?
                     try:
                        textareas = driver.find_elements(By.CSS_SELECTOR, "[data-testid^='tweetTextarea_']")
                        if textareas: textareas[-1].click()
                        time.sleep(1)
                     except: pass
                
                driver.execute_script("arguments[0].click();", tweet_btn)
                print("Final Post button clicked. Verifying modal closure...", file=sys.stderr)
                
                # Verify Success (Modal should disappear)
                modal_closed = False
                for _ in range(10):
                    time.sleep(1)
                    modals = driver.find_elements(By.CSS_SELECTOR, "[role='dialog']")
                    if not modals:
                        modal_closed = True
                        break
                    # Sometimes the modal stays but the button is gone (success)
                    btns = driver.find_elements(By.CSS_SELECTOR, "[data-testid='tweetButton']")
                    if not btns:
                        modal_closed = True
                        break
                
                if not modal_closed:
                    print("Warning: Post modal still visible after 10s. Might have failed.", file=sys.stderr)
                    # Don't return error yet, X might be slow
            except:
                 try:
                    debug_path = os.path.join(APPDATA_DIR, "screenshots", "debug_post_error.png")
                    driver.save_screenshot(debug_path)
                 except: pass
                 return {"status": "error", "message": "Post button not clickable."}
            
            time.sleep(5)
            print("DEBUG: Thread post flow finished", file=sys.stderr); sys.stderr.flush()
            return {"status": "success", "message": f"Thread with {len(tweets)} tweets posted!"}
            
        except Exception as e:
            try:
                debug_path = os.path.join(APPDATA_DIR, "screenshots", "debug_error.png")
                driver.save_screenshot(debug_path)
                print(f"DEBUG: Screenshot saved to {debug_path}", file=sys.stderr)
            except: pass
            
            import traceback
            return {"status": "error", "message": f"Thread fail at general step: {str(e)} | {traceback.format_exc()}"}

    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

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
        # Reduced threshold (min_faves:5) and URL-encoded to avoid query issues
        query = "(borsa OR hisse OR bist100 OR ekonomi OR dolar OR altın OR fed OR tcmb OR \"son dakika\") (haber OR gelişme) min_faves:5 filter:media -filter:replies"
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
        for tweet in tweets[:20]:
            try:
                # 1. Clean Text
                try:
                    text_elem = tweet.find_element(By.CSS_SELECTOR, "[data-testid='tweetText']")
                    content = text_elem.text
                except:
                    content = tweet.text.replace("\n", " ")
                
                # 2. Extract handle
                handle = "Unknown"
                try:
                    user_elem = tweet.find_element(By.CSS_SELECTOR, "[data-testid='User-Name']")
                    if "@" in user_elem.text:
                         handle = user_elem.text.split("@")[-1].split("\n")[0]
                except:
                    if "@" in content:
                        parts = content.split("@")
                        if len(parts) > 1:
                            handle = parts[1].split(" ")[0]
                
                # 3. Real URL
                url = driver.current_url
                try:
                    time_elem = tweet.find_element(By.TAG_NAME, "time")
                    link_elem = time_elem.find_element(By.XPATH, "./..")
                    url = link_elem.get_attribute("href")
                except: pass

                results.append({
                    "text": content,
                    "source": handle,
                    "url": url
                })
            except: continue
            
        return {"status": "success", "data": results}
        
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_top_gainers():
    """Scrape BIST100 top gainers (en çok yükselen hisseler) from Google Finance"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    
    driver = setup_driver(headless=True)
    if not driver: 
        return {"status": "error", "data": []}
    
    gainers = []
    try:
        # Go to BIST100 page
        driver.get("https://www.google.com/finance/quote/XU100:INDEXBIST?hl=tr")
        time.sleep(3)
        
        # Try to find gainers section - Google Finance shows top movers
        try:
            # Look for table rows with stock data
            rows = driver.find_elements(By.CSS_SELECTOR, "div[data-symbol], tr[data-symbol]")
            
            for row in rows[:10]:  # Top 10
                try:
                    # Extract symbol
                    symbol_elem = row.find_element(By.CSS_SELECTOR, "[data-symbol]")
                    symbol = symbol_elem.get_attribute("data-symbol") or ""
                    
                    if not symbol:
                        continue
                    
                    # Extract price
                    price_text = ""
                    try:
                        price_elem = row.find_element(By.CSS_SELECTOR, ".YMlKec")
                        price_text = price_elem.text
                    except:
                        pass
                    
                    # Extract change percentage
                    change_text = ""
                    try:
                        change_elem = row.find_element(By.CSS_SELECTOR, ".nwtPWb")
                        change_text = change_elem.text
                    except:
                        pass
                    
                    if price_text and change_text:
                        try:
                            price = float(price_text.replace(",", "."))
                            change_pct = float(change_text.replace(",", ".").replace("%", "").strip())
                            gainers.append({
                                "Symbol": symbol,
                                "Close": price,
                                "ChangePercent": change_pct
                            })
                        except:
                            pass
                except Exception as e:
                    print(f"Error parsing row: {e}", file=sys.stderr)
                    continue
        except Exception as e:
            print(f"Error extracting gainers row: {e}", file=sys.stderr)
            pass
        
        if not gainers:
             return {"status": "error", "message": "Could not scrape gainers and no fallback allowed."}
        
        return {"status": "success", "data": gainers}
    except Exception as e:
        print(f"Gainers error: {e}", file=sys.stderr)
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_stock_prices(symbols):
    """Fetch current prices for a list of symbols from Google Finance"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    
    driver = setup_driver(headless=True)
    if not driver: return {"status": "error", "message": "Driver fail"}
    
    prices = {}
    try:
        for sym in symbols:
            try:
                # Format symbol for Google Finance (e.g., SASA -> SASA:BIST)
                # Heuristic: 3-5 chars uppercase usually BIST if not Crypto
                gf_sym = sym
                if ":" not in sym:
                    if len(sym) <= 5: gf_sym = f"{sym}:IST"
                
                driver.get(f"https://www.google.com/finance/quote/{gf_sym}?hl=tr")
                val = WebDriverWait(driver, 5).until(EC.presence_of_element_located((By.CSS_SELECTOR, ".YMlKec.fxKbKc"))).text
                prices[sym] = val
            except:
                prices[sym] = "0.00"
        
        return {"status": "success", "data": prices}
    except Exception as e:
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_top_losers():
    """Scrape BIST100 top losers (en çok düşen hisseler)"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    
    driver = setup_driver(headless=True)
    if not driver: 
        return {"status": "error", "data": []}
    
    losers = []
    try:
        # Go to BIST100 page
        driver.get("https://www.google.com/finance/quote/XU100:INDEXBIST?hl=tr")
        time.sleep(3)
        
        # Try to find losers section
        try:
            rows = driver.find_elements(By.CSS_SELECTOR, "div[data-symbol], tr[data-symbol]")
            
            for row in rows[-10:]:  # Last 10 (losers typically at end)
                try:
                    symbol_elem = row.find_element(By.CSS_SELECTOR, "[data-symbol]")
                    symbol = symbol_elem.get_attribute("data-symbol") or ""
                    
                    if not symbol:
                        continue
                    
                    price_text = ""
                    try:
                        price_elem = row.find_element(By.CSS_SELECTOR, ".YMlKec")
                        price_text = price_elem.text
                    except:
                        pass
                    
                    change_text = ""
                    try:
                        change_elem = row.find_element(By.CSS_SELECTOR, ".nwtPWb")
                        change_text = change_elem.text
                    except:
                        pass
                    
                    if price_text and change_text:
                        try:
                            price = float(price_text.replace(",", "."))
                            change_pct = float(change_text.replace(",", ".").replace("%", "").strip())
                            losers.append({
                                "Symbol": symbol,
                                "Close": price,
                                "ChangePercent": change_pct
                            })
                        except:
                            pass
                except Exception as e:
                    print(f"Error parsing row: {e}", file=sys.stderr)
                    continue
        except Exception as e:
            print(f"Error extracting losers table: {e}", file=sys.stderr)
            pass
        
        if not losers:
            return {"status": "error", "message": "Could not scrape losers and no fallback allowed."}
        
        return {"status": "success", "data": losers}
    except Exception as e:
        print(f"Losers error: {e}", file=sys.stderr)
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def get_top_volume():
    """Scrape BIST100 top volume stocks (en yüksek hacimli hisseler)"""
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    
    driver = setup_driver(headless=True)
    if not driver: 
        return {"status": "error", "data": []}
    
    volume_stocks = []
    try:
        # Go to BIST100 page
        driver.get("https://www.google.com/finance/quote/XU100:INDEXBIST?hl=tr")
        time.sleep(3)
        
        # Try to find volume data
        try:
            rows = driver.find_elements(By.CSS_SELECTOR, "div[data-symbol], tr[data-symbol]")
            
            for row in rows[:10]:  # Top 10 by volume
                try:
                    symbol_elem = row.find_element(By.CSS_SELECTOR, "[data-symbol]")
                    symbol = symbol_elem.get_attribute("data-symbol") or ""
                    
                    if not symbol:
                        continue
                    
                    price_text = ""
                    try:
                        price_elem = row.find_element(By.CSS_SELECTOR, ".YMlKec")
                        price_text = price_elem.text
                    except:
                        pass
                    
                    change_text = ""
                    try:
                        change_elem = row.find_element(By.CSS_SELECTOR, ".nwtPWb")
                        change_text = change_elem.text
                    except:
                        pass
                    
                    if price_text and change_text:
                        try:
                            price = float(price_text.replace(",", "."))
                            change_pct = float(change_text.replace(",", ".").replace("%", "").strip())
                            volume_stocks.append({
                                "Symbol": symbol,
                                "Close": price,
                                "ChangePercent": change_pct
                            })
                        except:
                            pass
                except Exception as e:
                    print(f"Error parsing row: {e}", file=sys.stderr)
                    continue
        except Exception as e:
            print(f"Error extracting volume table: {e}", file=sys.stderr)
            pass
        
        if not volume_stocks:
            return {"status": "error", "message": "Could not scrape volume stocks and no fallback allowed."}
        
        return {"status": "success", "data": volume_stocks}
    except Exception as e:
        print(f"Volume error: {e}", file=sys.stderr)
        return {"status": "error", "message": str(e)}
    finally:
        if driver: driver.quit()

def interact_with_targets(targets_str):
    """Like + RT the last tweet of target accounts
    targets_str: comma-separated handles (e.g., "@handle1,@handle2" or "handle1,handle2")
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
        "CRYPTO": ["BTCTurk", "Paribu", "bitcointr"],
        "FOREX": ["InvestingTR", "gcmforex", "ForeksHaber"]
    }
    keywords = {
        "BIST": ["yatırım", "finans", "borsa", "ekonomi", "hisse"],
        "CRYPTO": ["kripto", "crypto", "bitcoin", "btc", "eth"],
        "FOREX": ["forex", "döviz", "altın", "gold", "emtia"]
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
    
    # Discover Influencers command
    cmd_discover = subparsers.add_parser("discover_influencers", parents=[parent_parser])
    cmd_discover.add_argument("--category", required=True, help="BIST, CRYPTO, or FOREX")
    cmd_discover.add_argument("--query", required=False, help="Custom search query (optional)")

    args = parser.parse_args()
    
    # Global visibility override
    if hasattr(args, 'visible') and args.visible:
        os.environ["X_VISIBLE"] = "1"
    
    # ---------------------------------------------------------
    # HANDLERS
    # ---------------------------------------------------------

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
        print("---JSON_START---")
        result_json = find_influencer_posts(query, args.market)
        try:
             with open(APPDATA_DIR / "debug_social_intel.txt", "a", encoding="utf-8") as f:
                 f.write(f"\n[JSON_OUTPUT] Length: {len(result_json)}\n[JSON_CONTENT]: {result_json[:2000]}...\n")
        except: pass
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
        print(json.dumps(reply_to_tweet(args.url, text_content)))

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
    
    else:
        # Default fallback for unknown commands
        print(json.dumps({"status": "error", "message": f"Unknown command: {args.command}"}))
