import sys
import json
import asyncio
import os

# Ensure UTF-8 encoding for stdout/stderr to handle Turkish characters on Windows
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8')
import re
import glob
from pathlib import Path
import pickle
from datetime import datetime
import argparse

try:
    from playwright.async_api import async_playwright, TimeoutError as PlaywrightTimeoutError
except ImportError:
    print(json.dumps({"status": "error", "message": "Playwright is not installed in this environment."}))
    sys.exit(1)

def _discover_appdata():
    paths = [
        Path(os.environ.get("LOCALAPPDATA", os.path.expanduser("~"))) / "XiDeAI",
        Path(os.path.dirname(__file__)).parent,
        Path(os.path.dirname(__file__)).parent.parent / "XiDeAI",
        Path(os.path.dirname(__file__)),
    ]
    for p in paths:
        if (p / "twitter_cookies.json").exists() or (p / "twitter_cookies.pkl").exists():
            return p
    return paths[0]

APPDATA_DIR = _discover_appdata()
COOKIES_FILE = APPDATA_DIR / "twitter_cookies.pkl"
JSON_COOKIES_FILE = APPDATA_DIR / "twitter_cookies.json"


def _to_bool(value, default=False):
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.strip().lower() in ["1", "true", "yes"]
    if value is None:
        return default
    return bool(value)


def _find_json_cookie_file():
    """Mirror old Selenium behavior: fallback to latest twitter_cookies*.json copy."""
    if JSON_COOKIES_FILE.exists():
        return JSON_COOKIES_FILE

    search_dirs = [
        APPDATA_DIR,
        Path(os.environ.get("LOCALAPPDATA", os.path.expanduser("~"))) / "XiDeAI",
    ]

    for sd in search_dirs:
        pattern = str(sd / "twitter_cookies*.json")
        candidates = sorted(glob.glob(pattern), key=lambda f: os.path.getmtime(f), reverse=True)
        if candidates:
            return Path(candidates[0])

    return None

class XDaemonPlaywright:
    def __init__(self, visible=False):
        self.visible = visible
        self.playwright = None
        self.browser = None
        self.context = None
        self.page = None
        self.profile_url = None
        self.profile_path = None
        # Runtime tuning for slow/unstable environments.
        # Example: set X_POSTING_DELAY_FACTOR=1.5 to increase all short sleeps by 50%.
        self.delay_factor = self._read_delay_factor()

    def _read_delay_factor(self):
        raw = os.environ.get("X_POSTING_DELAY_FACTOR", "1.0")
        try:
            val = float(raw)
            return max(0.5, min(3.0, val))
        except Exception:
            return 1.0

    async def _sleep(self, seconds):
        await asyncio.sleep(seconds * self.delay_factor)

    async def start(self):
        self.playwright = await async_playwright().start()
        # Fast initialization
        self.browser = await self.playwright.chromium.launch(
            headless=not self.visible,
            args=[
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--no-sandbox",
            ]
        )
        self.context = await self.browser.new_context(
            user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            locale="tr-TR",
            timezone_id="Europe/Istanbul"
        )
        self.page = await self.context.new_page()
        await self.load_cookies()

        # Mirror old x_daemon verification: ensure session is really authenticated.
        await self.page.goto("https://x.com/home", wait_until="domcontentloaded", timeout=20000)
        await asyncio.sleep(2)
        current_url = self.page.url.lower()
        if "login" in current_url or "signin" in current_url or "i/flow" in current_url:
            raise Exception("Cookie load failed: redirected to login page")

        await self._discover_profile()

    async def _discover_profile(self):
        """Resolve current account profile URL to prevent replying to unrelated timeline tweets."""
        try:
            candidates = [
                'a[data-testid="AppTabBar_Profile_Link"]',
                'a[href*="/"].r-1loqt21.r-1otgn73',
            ]

            for sel in candidates:
                link = self.page.locator(sel).first
                if await link.count() == 0:
                    continue

                href = await link.get_attribute("href")
                if not href:
                    continue

                if href.startswith("/"):
                    self.profile_path = href
                    self.profile_url = f"https://x.com{href}"
                elif "x.com/" in href:
                    self.profile_url = href
                    m = re.search(r"https?://x\.com(/[^/?#]+)", href)
                    if m:
                        self.profile_path = m.group(1)

                if self.profile_url:
                    return
        except Exception:
            pass

    async def load_cookies(self):
        pw_cookies = []
        seen = set()
        json_file = _find_json_cookie_file()
        
        # Try JSON first
        if json_file and json_file.exists():
            try:
                cookies = json.loads(json_file.read_text(encoding="utf-8"))
                for c in cookies:
                    name = c.get("name", c.get("Name"))
                    value = c.get("value", c.get("Value"))
                    # Skip metadata rows (meta_user_agent/meta_client_hints) or malformed entries.
                    if not name or value is None:
                        continue

                    domain = c.get("domain", c.get("Domain", ".x.com"))
                    if domain and not domain.startswith(".") and "." in domain:
                        domain = "." + domain

                    if not domain:
                        domain = ".x.com"

                    pc = {
                        "name": name,
                        "value": value,
                        "domain": domain,
                        "path": c.get("path", c.get("Path", "/")),
                        "secure": _to_bool(c.get("secure", c.get("Secure", c.get("isSecure", True))), True),
                        "httpOnly": _to_bool(c.get("httpOnly", c.get("HttpOnly", c.get("isHttpOnly", False))), False),
                    }

                    if "sameSite" in c:
                        pc["sameSite"] = c["sameSite"] if c["sameSite"] in ["None", "Strict", "Lax"] else "Lax"
                    else:
                        pc["sameSite"] = "None" if pc["secure"] else "Lax"
                    
                    if "expires" in c: pc["expires"] = int(c["expires"])
                    elif "Expires" in c: pc["expires"] = int(c["Expires"])
                    elif "expiry" in c: pc["expires"] = int(c["expiry"])
                    
                    cookie_key = (pc["name"], pc["domain"], pc["path"])
                    if cookie_key in seen:
                        continue
                    seen.add(cookie_key)
                    pw_cookies.append(pc)
            except Exception as e:
                pass
                
        # Try Pickle backup too (old Selenium flow loaded both, we keep that parity)
        if COOKIES_FILE.exists():
            try:
                with open(COOKIES_FILE, "rb") as f:
                    sel_cookies = pickle.load(f)
                
                for c in sel_cookies:
                    name = c.get("name")
                    value = c.get("value")
                    if not name or value is None:
                        continue

                    domain = c.get("domain", "")
                    if domain and not domain.startswith(".") and "." in domain:
                        domain = "." + domain

                    if not domain:
                        domain = ".x.com"
                        
                    pc = {
                        "name": name,
                        "value": value,
                        "domain": domain,
                        "path": c.get("path", "/"),
                        "secure": _to_bool(c.get("secure", True), True),
                        "httpOnly": _to_bool(c.get("httpOnly", False), False),
                    }
                    if "sameSite" in c:
                        pc["sameSite"] = c["sameSite"] if c["sameSite"] in ["None", "Strict", "Lax"] else "Lax"
                    else:
                        pc["sameSite"] = "None" if c.get("secure") else "Lax"
                        
                    if "expiry" in c and int(c["expiry"]) > 0:
                        pc["expires"] = int(c["expiry"])
                    cookie_key = (pc["name"], pc["domain"], pc["path"])
                    if cookie_key in seen:
                        continue
                    seen.add(cookie_key)
                    pw_cookies.append(pc)
            except Exception as e:
                raise Exception(f"Failed to load cookies from pickle: {str(e)}")

        if not pw_cookies:
            raise Exception("Cookies file not found. Please log in using old social_intel/x_daemon logic first.")
            
        try:
            await self.context.add_cookies(pw_cookies)
        except Exception as e:
            raise Exception(f"Failed to load cookies: {str(e)}")

    async def stop(self):
        if self.browser:
            await self.browser.close()
        if self.playwright:
            await self.playwright.stop()

    def count_x_characters(self, text):
        emoji_pattern = r'[\U0001F600-\U0001F64F\U0001F300-\U0001F5FF\U0001F680-\U0001F6FF\U0001F1E0-\U0001F1FF\U00002600-\U000027BF\U0001F900-\U0001F9FF]'
        emojis = len(re.findall(emoji_pattern, text))
        url_pattern = r'https?://\S+|www\.\S+|[a-zA-Z0-9-]+\.[a-zA-Z]{2,}'
        urls = re.findall(url_pattern, text)
        url_chars = len(urls) * 23
        text_without_urls = re.sub(url_pattern, '', text)
        text_without_emojis = re.sub(emoji_pattern, '', text_without_urls)
        regular_chars = len(text_without_emojis)
        return emojis * 2 + url_chars + regular_chars

    async def _post_single_tweet(self, text: str, images=None) -> dict:
        for attempt in range(1, 4):
            try:
                await self.page.goto("https://x.com/compose/post", wait_until="commit", timeout=20000)
                await self._sleep(1.5)
                
                # Check if logged out
                current_url = self.page.url.lower()
                if "login" in current_url or "flow" in current_url:
                    return {"status": "error", "message": "Cookies expired. Please login again."}

                compose_selectors = [
                    'div[data-testid="tweetTextarea_0"]',
                    'div[role="textbox"][contenteditable="true"]'
                ]
                compose_box = None
                for selector in compose_selectors:
                    try:
                        cand = self.page.locator(selector).first
                        await cand.wait_for(state="visible", timeout=6000)
                        compose_box = cand
                        break
                    except:
                        pass
                
                if not compose_box:
                    raise PlaywrightTimeoutError("Compose box not found")

                await compose_box.click(timeout=3000)
                await compose_box.fill("")
                await self._sleep(0.3)

                await compose_box.evaluate("""
                    (element, text) => {
                        element.focus();
                        element.innerText = text;
                        document.execCommand('insertText', false, text);
                        element.dispatchEvent(new Event('input', { bubbles: true }));
                    }
                """, text)
                await self._sleep(0.5)

                if images and isinstance(images, list):
                    for img in images:
                        try:
                            upload_input = self.page.locator('input[data-testid="fileInput"]').first
                            await upload_input.set_input_files(img)
                            await self._sleep(1.5)
                        except:
                            pass

                post_selectors = [
                    "button[data-testid='tweetButton']",
                    "div[data-testid='tweetButtonInline']",
                    "div[data-testid='tweetButton']"
                ]
                post_button = None
                for selector in post_selectors:
                    buttons = self.page.locator(selector)
                    count = await buttons.count()
                    for i in range(count):
                        btn = buttons.nth(i)
                        try:
                            await btn.wait_for(state="visible", timeout=1500)
                            if await btn.get_attribute("aria-disabled") == "true": continue
                            if not await btn.is_enabled(): continue
                            post_button = btn
                            break
                        except:
                            pass
                    if post_button: break
                
                if not post_button:
                    raise PlaywrightTimeoutError("Post button disabled or not found. Text might be invalid/empty.")

                await post_button.click(timeout=4000)
                await self._sleep(3)

                tweet_url = await self._extract_latest_tweet_url()
                return {"status": "success", "tweet_url": tweet_url, "text": text}

            except PlaywrightTimeoutError as e:
                if attempt == 3:
                    return {"status": "error", "message": str(e)}
                await self._sleep(2)
            except Exception as e:
                return {"status": "error", "message": str(e)}

    async def _post_reply_in_thread(self, parent_url: str, text: str) -> dict:
        for attempt in range(1, 4):
            try:
                await self.page.goto(parent_url, wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(2.0)
                
                reply_box = None
                selectors = ['div[data-testid="tweetTextarea_0"]', 'div[role="textbox"][contenteditable="true"]']
                for sel in selectors:
                    try:
                        cand = self.page.locator(sel).first
                        await cand.wait_for(state="visible", timeout=5000)
                        reply_box = cand
                        break
                    except: pass
                
                if not reply_box:
                    reply_btn = self.page.locator('div[data-testid="reply"]').first
                    if await reply_btn.count() > 0:
                        await reply_btn.click()
                        await asyncio.sleep(1)
                        reply_box = self.page.locator('div[data-testid="tweetTextarea_0"]').first
                        await reply_box.wait_for(state="visible", timeout=3000)

                if not reply_box: raise PlaywrightTimeoutError("Reply box not found")

                await reply_box.click()
                await reply_box.fill("")
                await reply_box.evaluate("""
                    (element, text) => {
                        element.focus();
                        document.execCommand('insertText', false, text);
                        element.dispatchEvent(new Event('input', { bubbles: true }));
                    }
                """, text)
                await asyncio.sleep(0.5)

                post_btn = None
                for sel in ["button[data-testid='tweetButtonInline']", "div[data-testid='tweetButton']", "button[data-testid='tweetButton']"]:
                    cand = self.page.locator(sel).first
                    try:
                        await cand.wait_for(state="visible", timeout=1500)
                        if await cand.get_attribute("aria-disabled") != "true" and await cand.is_enabled():
                            post_btn = cand
                            break
                    except: pass
                
                if not post_btn: raise PlaywrightTimeoutError("Reply button disabled")
                
                await post_btn.click(timeout=4000)
                await asyncio.sleep(3)

                tweet_url = await self._extract_latest_tweet_url()
                return {"status": "success", "tweet_url": tweet_url}
            except PlaywrightTimeoutError as e:
                if attempt == 3: return {"status": "error", "message": str(e)}
            except Exception as e:
                return {"status": "error", "message": str(e)}

    async def _post_thread_compose(self, chunks: list, images=None) -> dict:
        """Post a multi-tweet thread using X compose screen + add (+) button.
        All tweets are composed in a single session and published at once as a native thread.
        This avoids the reply-chain approach which can attach replies to wrong/unrelated tweets."""
        # X changed the compose URL; try canonical first, fall back to legacy alias.
        COMPOSE_URLS = ["https://x.com/compose/post", "https://x.com/compose/tweet"]
        # data-testid for the «Add tweet» (+) button — try multiple to survive A/B changes.
        ADD_BTN_SELECTORS = [
            '[data-testid="addButton"]',        # element-agnostic (most resilient)
            'div[data-testid="addButton"]',     # historical form (div)
            'button[data-testid="addButton"]',  # in case X switched to <button>
            '[aria-label="Add post"]',          # aria-label (en/default locale)
            '[aria-label="Gönderi ekle"]',      # aria-label (tr locale)
            '[aria-label="Post ekle"]',         # possible mixed locale variant
            '[data-testid="tweetButton_add"]',  # speculative future testid
        ]

        for attempt in range(1, 4):
            try:
                compose_url = COMPOSE_URLS[0]
                await self.page.goto(compose_url, wait_until="commit", timeout=20000)
                await self._sleep(1.5)

                current_url = self.page.url.lower()
                # If X redirected back to login or flow page, try the legacy URL once.
                if "login" in current_url or "flow" in current_url:
                    if len(COMPOSE_URLS) > 1:
                        await self.page.goto(COMPOSE_URLS[1], wait_until="commit", timeout=20000)
                        await self._sleep(1.5)
                        current_url = self.page.url.lower()
                    if "login" in current_url or "flow" in current_url:
                        return {"status": "error", "message": "Cookies expired. Please login again."}

                for idx, chunk in enumerate(chunks):
                    # First tweet: tweetTextarea_0; after each + click: last visible textarea.
                    if idx == 0:
                        compose_box = self.page.locator('div[data-testid="tweetTextarea_0"]').first
                    else:
                        compose_box = self.page.locator('div[data-testid^="tweetTextarea_"]').last

                    await compose_box.wait_for(state="visible", timeout=6000)
                    await compose_box.click(timeout=3000)
                    await compose_box.fill("")
                    await self._sleep(0.3)

                    await compose_box.evaluate("""
                        (element, text) => {
                            element.focus();
                            element.innerText = text;
                            document.execCommand('insertText', false, text);
                            element.dispatchEvent(new Event('input', { bubbles: true }));
                        }
                    """, chunk)
                    await self._sleep(0.5)

                    # Upload media only with first tweet
                    if idx == 0 and images and isinstance(images, list):
                        for img in images:
                            try:
                                upload_input = self.page.locator('input[data-testid="fileInput"]').first
                                await upload_input.set_input_files(img)
                                await self._sleep(1.5)
                            except:
                                pass

                    # Click the + (add tweet) button unless this is the last tweet.
                    # Try each selector in priority order; first match wins.
                    if idx < len(chunks) - 1:
                        before_count = await self.page.locator('div[data-testid^="tweetTextarea_"]').count()
                        add_btn = None
                        for sel in ADD_BTN_SELECTORS:
                            try:
                                candidate = self.page.locator(sel).first
                                await candidate.wait_for(state="visible", timeout=2000)
                                if await candidate.get_attribute("aria-disabled") == "true":
                                    continue
                                if not await candidate.is_enabled():
                                    continue
                                add_btn = candidate
                                break
                            except Exception:
                                pass
                        if add_btn is None:
                            raise PlaywrightTimeoutError(
                                "Add tweet (+) button not found — tried: " + ", ".join(ADD_BTN_SELECTORS)
                            )
                        await add_btn.click(timeout=3000)
                        # Ensure new textarea really appears. If not, force-click once and recheck.
                        try:
                            await self.page.wait_for_function(
                                "(prev) => document.querySelectorAll('div[data-testid^=\"tweetTextarea_\"]').length > prev",
                                arg=before_count,
                                timeout=3000,
                            )
                        except Exception:
                            await add_btn.evaluate("el => el.click()")
                            await self.page.wait_for_function(
                                "(prev) => document.querySelectorAll('div[data-testid^=\"tweetTextarea_\"]').length > prev",
                                arg=before_count,
                                timeout=3000,
                            )
                        await self._sleep(0.8)

                # Click the final publish button once for the whole thread
                post_selectors = [
                    "button[data-testid='tweetButton']",
                    "div[data-testid='tweetButtonInline']",
                    "div[data-testid='tweetButton']"
                ]
                post_button = None
                for selector in post_selectors:
                    buttons = self.page.locator(selector)
                    count = await buttons.count()
                    for i in range(count):
                        btn = buttons.nth(i)
                        try:
                            await btn.wait_for(state="visible", timeout=1500)
                            if await btn.get_attribute("aria-disabled") == "true": continue
                            if not await btn.is_enabled(): continue
                            post_button = btn
                            break
                        except:
                            pass
                    if post_button: break

                if not post_button:
                    raise PlaywrightTimeoutError("Publish button not found or disabled after composing thread")

                await post_button.click(timeout=4000)
                await self._sleep(3)

                tweet_url = await self._extract_latest_tweet_url()
                return {"status": "success", "tweet_url": tweet_url}

            except PlaywrightTimeoutError as e:
                if attempt == 3:
                    return {"status": "error", "message": str(e)}
                await self._sleep(2)
            except Exception as e:
                return {"status": "error", "message": str(e)}

    async def _extract_latest_tweet_url(self) -> str:
        # 1) Prefer post-success toast links immediately after publish.
        toast_selectors = [
            "div[data-testid='toast'] a[href*='/status/']",
            "div[role='alert'] a[href*='/status/']",
        ]
        for selector in toast_selectors:
            try:
                links = self.page.locator(selector)
                count = await links.count()
                for i in range(min(count, 3)):
                    href = await links.nth(i).get_attribute("href")
                    if href and "/status/" in href:
                        return f"https://x.com{href}" if href.startswith("/") else href
            except:
                pass

        # 2) Fallback to own profile; pick highest status id among own links.
        if self.profile_url:
            try:
                await self.page.goto(self.profile_url, wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(1.5)

                links = self.page.locator("article[data-testid='tweet'] a[href*='/status/']")
                count = await links.count()
                best_url = ""
                best_id = -1

                for i in range(min(count, 20)):
                    href = await links.nth(i).get_attribute("href")
                    if not href or "/status/" not in href:
                        continue

                    if self.profile_path and href.startswith("/") and not href.startswith(self.profile_path + "/status/"):
                        continue

                    url = f"https://x.com{href}" if href.startswith("/") else href
                    if "photo" in url or "video" in url:
                        continue

                    m = re.search(r"/status/(\d+)", url)
                    if not m:
                        continue

                    status_id = int(m.group(1))
                    if status_id > best_id:
                        best_id = status_id
                        best_url = url

                if best_url:
                    return best_url
            except:
                pass

        # 3) Last resort: keep current page URL.
        return self.page.url

    async def execute_post(self, payload):
        text = payload.get("text", "")
        media_path = payload.get("media")
        preserve_chunks = bool(payload.get("preserve_chunks", False))
        images = [media_path] if media_path and os.path.exists(media_path) else None

        # Determine if it's a thread (XiDeAI_Pro UI passes List<string> but translates it to json?)
        # Wait, social_intel.cs writes: new { tweets = tweets, media = mediaPath }
        tweets = payload.get("tweets", [])
        if not tweets and text:
            tweets = [text] # single tweet mode

        if not tweets:
            return {"status": "error", "message": "No tweets provided"}

        # Preserve C# prepared parts when requested. Only split when a chunk exceeds safe limit.
        chunks = []
        for tweet_text in tweets:
            tweet_text = tweet_text.replace("|||", "").strip()
            if not tweet_text:
                continue

            if preserve_chunks and self.count_x_characters(tweet_text) <= 265:
                chunks.append(tweet_text)
                continue

            remaining = tweet_text
            while remaining:
                if self.count_x_characters(remaining) <= 265:
                    chunks.append(remaining)
                    break

                # If too long, split it carefully
                split_pos = min(180, len(remaining))
                while split_pos < len(remaining):
                    test_chars = self.count_x_characters(remaining[:split_pos])
                    if test_chars > 265:
                        split_pos -= 10
                        break
                    if test_chars > 240:
                        split_pos += 2
                    else:
                        split_pos += 15

                # Intelligent sentence breaking
                found_break = False
                for end_char in ['.\n', '!\n', '?\n', '\n\n', '. ', '? ', '! ', '\n', ', ']:
                    pos = remaining.rfind(end_char, max(50, split_pos - 80), split_pos + 20)
                    if pos > 50:
                        split_pos = pos + len(end_char)
                        found_break = True
                        break

                # If we couldn't find a good break, hard cut by space
                if not found_break:
                    pos = remaining.rfind(' ', max(50, split_pos - 30), split_pos)
                    if pos > 50:
                        split_pos = pos + 1

                chunk_text = remaining[:split_pos].strip()
                if chunk_text:
                    chunks.append(chunk_text)
                remaining = remaining[split_pos:].strip()
                
        if not chunks:
            return {"status": "error", "message": "All parsed chunks were empty"}
            
        # Avoid thread logic if there's only 1 chunk
        if len(chunks) == 1:
            return await self._post_single_tweet(chunks[0], images)

        numbered_chunks = []
        total = len(chunks)
        for i, chunk in enumerate(chunks, 1):
            if i == 1: numbered_chunks.append(f"{chunk}\n\n🧵 {i}/{total}")
            else: numbered_chunks.append(f"🧵 {i}/{total}\n\n{chunk}")

        # PRIMARY: Native compose thread — all tweets typed in one session, publish once.
        # X builds a proper thread internally, no risk of replying to wrong tweets.
        compose_result = await self._post_thread_compose(numbered_chunks, images)
        if compose_result.get("status") == "success":
            return compose_result

        # FALLBACK: reply-chain if compose approach failed (e.g. + button unavailable)
        print(f"[playwright_daemon] Compose thread failed ({compose_result.get('message')}), falling back to reply-chain.", flush=True)
        first_res = await self._post_single_tweet(numbered_chunks[0], images)
        if first_res.get("status") != "success":
            return first_res

        parent_url = first_res.get("tweet_url", "")
        for i, chunk in enumerate(numbered_chunks[1:], 2):
            await asyncio.sleep(2.0)
            res = await self._post_reply_in_thread(parent_url, chunk)
            if res.get("status") == "success":
                candidate = res.get("tweet_url", parent_url)
                if isinstance(candidate, str) and "/status/" in candidate:
                    if (self.profile_path and f"x.com{self.profile_path}/status/" in candidate) or (not self.profile_path):
                        parent_url = candidate
            else:
                return {
                    "status": "error",
                    "message": f"Thread part {i} failed: {res.get('message')}",
                    "tweet_url": first_res.get("tweet_url")
                }

        return {"status": "success", "tweet_url": first_res.get("tweet_url")}

async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", help="Command to run: post_thread")
    parser.add_argument("--file", help="Path to json file payload", required=True)
    parser.add_argument("--visible", action="store_true", help="Run visible browser")
    args = parser.parse_args()

    if args.command in ["post_thread", "post_tweet"]:
        try:
            with open(args.file, "r", encoding="utf-8") as f:
                payload = json.load(f)
        except Exception as e:
            print(json.dumps({"status": "error", "message": f"Failed to read payload: {e}"}))
            return

        daemon = XDaemonPlaywright(visible=args.visible)
        try:
            await daemon.start()
            result = await daemon.execute_post(payload)
            print(json.dumps(result))
        except Exception as e:
            print(json.dumps({"status": "error", "message": str(e)}))
        finally:
            await daemon.stop()
    else:
        print(json.dumps({"status": "error", "message": "Unknown command"}))

if __name__ == "__main__":
    asyncio.run(main())
