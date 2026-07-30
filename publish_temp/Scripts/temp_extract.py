import sys

with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'r', encoding='utf-8') as f:
    content = f.read()

old_func = '''    async def _extract_latest_tweet_url(self) -> str:
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
                pass'''

new_func = '''    async def _extract_latest_tweet_url(self) -> str:
        # 1) Prefer post-success toast links immediately after publish.
        toast_selectors = [
            "div[data-testid='toast'] a[href*='/status/']",
            "div[role='alert'] a[href*='/status/']",
        ]
        for selector in toast_selectors:
            try:
                links = self.page.locator(selector)
                try:
                    await links.first.wait_for(state="attached", timeout=5000)
                except:
                    pass
                count = await links.count()
                for i in range(min(count, 3)):
                    href = await links.nth(i).get_attribute("href")
                    if href and "/status/" in href:
                        return f"https://x.com{href}" if href.startswith("/") else href
            except:
                pass'''

new_content = content.replace(old_func, new_func)
if new_content != content:
    with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Replaced successfully")
else:
    print("Replace failed")
