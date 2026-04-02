import sys
import json
import asyncio
import os
import re
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

class XDaemonPlaywright:
    def __init__(self, visible=False):
        self.visible = visible
        self.playwright = None
        self.browser = None
        self.context = None
        self.page = None

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
        await self.load_cookies()
        self.page = await self.context.new_page()

    async def load_cookies(self):
        pw_cookies = []
        
        # Try JSON first
        if JSON_COOKIES_FILE.exists():
            try:
                cookies = json.loads(JSON_COOKIES_FILE.read_text(encoding="utf-8"))
                for c in cookies:
                    domain = c.get("domain", c.get("Domain", ".x.com"))
                    if domain and not domain.startswith(".") and "." in domain:
                        domain = "." + domain
                    pc = {
                        "name": c.get("name", c.get("Name")),
                        "value": c.get("value", c.get("Value")),
                        "domain": domain,
                        "path": c.get("path", c.get("Path", "/")),
                        "secure": c.get("secure", c.get("Secure", c.get("isSecure", True))),
                        "httpOnly": c.get("httpOnly", c.get("HttpOnly", c.get("isHttpOnly", False))),
                    }
                    if "sameSite" in c:
                         pc["sameSite"] = c["sameSite"] if c["sameSite"] in ["None", "Strict", "Lax"] else "Lax"
                    else:
                         pc["sameSite"] = "None" if pc["secure"] else "Lax"
                    
                    if "expires" in c: pc["expires"] = int(c["expires"])
                    elif "Expires" in c: pc["expires"] = int(c["Expires"])
                    elif "expiry" in c: pc["expires"] = int(c["expiry"])
                    
                    pw_cookies.append(pc)
            except Exception as e:
                pass
                
        # Try Pickle if no JSON or it failed
        if not pw_cookies and COOKIES_FILE.exists():
            try:
                with open(COOKIES_FILE, "rb") as f:
                    sel_cookies = pickle.load(f)
                
                for c in sel_cookies:
                    domain = c.get("domain", "")
                    if domain and not domain.startswith(".") and "." in domain:
                        domain = "." + domain
                        
                    pc = {
                        "name": c["name"],
                        "value": c["value"],
                        "domain": domain,
                        "path": c.get("path", "/"),
                        "secure": c.get("secure", True),
                        "httpOnly": c.get("httpOnly", False),
                    }
                    if "sameSite" in c:
                        pc["sameSite"] = c["sameSite"] if c["sameSite"] in ["None", "Strict", "Lax"] else "Lax"
                    else:
                        pc["sameSite"] = "None" if c.get("secure") else "Lax"
                        
                    if "expiry" in c and int(c["expiry"]) > 0:
                        pc["expires"] = int(c["expiry"])
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
                await self.page.goto("https://x.com/compose/tweet", wait_until="commit", timeout=20000)
                await asyncio.sleep(1.5)
                
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
                await asyncio.sleep(0.3)

                await compose_box.evaluate("""
                    (element, text) => {
                        element.focus();
                        element.innerText = text;
                        document.execCommand('insertText', false, text);
                        element.dispatchEvent(new Event('input', { bubbles: true }));
                    }
                """, text)
                await asyncio.sleep(0.5)

                if images and isinstance(images, list):
                    for img in images:
                        try:
                            upload_input = self.page.locator('input[data-testid="fileInput"]').first
                            await upload_input.set_input_files(img)
                            await asyncio.sleep(1.5)
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
                await asyncio.sleep(3)

                tweet_url = await self._extract_latest_tweet_url()
                return {"status": "success", "tweet_url": tweet_url, "text": text}

            except PlaywrightTimeoutError as e:
                if attempt == 3:
                    return {"status": "error", "message": str(e)}
                await asyncio.sleep(2)
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

    async def _extract_latest_tweet_url(self) -> str:
        selectors = ["article[data-testid='tweet'] a[href*='/status/']", "a[href*='/status/']"]
        for selector in selectors:
            try:
                links = self.page.locator(selector)
                count = await links.count()
                for i in range(min(count, 5)):
                    href = await links.nth(i).get_attribute("href")
                    if href and "/status/" in href:
                        url = f"https://x.com{href}" if href.startswith("/") else href
                        if "photo" not in url and "video" not in url:
                            return url
            except: pass
        return self.page.url

    async def execute_post(self, payload):
        text = payload.get("text", "")
        media_path = payload.get("media")
        images = [media_path] if media_path and os.path.exists(media_path) else None

        # Determine if it's a thread (XiDeAI_Pro UI passes List<string> but translates it to json?)
        # Wait, social_intel.cs writes: new { tweets = tweets, media = mediaPath }
        tweets = payload.get("tweets", [])
        if not tweets and text:
            tweets = [text] # single tweet mode

        if not tweets:
            return {"status": "error", "message": "No tweets provided"}

        # Preserve the C# AI thread structure unless a chunk is strictly over 265 characters
        chunks = []
        for tweet_text in tweets:
            tweet_text = tweet_text.replace("|||", "").strip()
            if not tweet_text:
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
                    if test_chars > 240: split_pos += 2
                    else: split_pos += 15
                
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

        first_res = await self._post_single_tweet(numbered_chunks[0], images)
        if first_res.get("status") != "success":
            return first_res

        parent_url = first_res.get("tweet_url", "")
        for i, chunk in enumerate(numbered_chunks[1:], 2):
            await asyncio.sleep(2.0)
            res = await self._post_reply_in_thread(parent_url, chunk)
            if res.get("status") == "success":
                parent_url = res.get("tweet_url", parent_url)
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
