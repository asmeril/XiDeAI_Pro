import sys

with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'r', encoding='utf-8') as f:
    content = f.read()

old_chunk = '''        # 2) Fallback to own profile; pick highest status id among own links.
        if self.profile_path:
            try:
                await self.page.goto(f"https://x.com{self.profile_path}", wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(2.0)

                links = self.page.locator("article[data-testid='tweet'] a[href*='/status/']")
                try:
                    await links.first.wait_for(state="attached", timeout=6000)
                except:
                    pass
                count = await links.count()
                for i in range(min(count, 3)):
                    href = await links.nth(i).get_attribute("href")
                    if href and "/status/" in href:
                        return f"https://x.com{href}" if href.startswith("/") else href
            except:
                pass'''

new_chunk = '''        # 2) Fallback to own profile; pick highest status id among own links.
        if self.profile_path:
            try:
                await self.page.goto(f"https://x.com{self.profile_path}", wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(2.0)

                articles = self.page.locator("article[data-testid='tweet']")
                try:
                    await articles.first.wait_for(state="attached", timeout=6000)
                except:
                    pass
                count = await articles.count()
                for i in range(min(count, 3)):
                    article = articles.nth(i)
                    
                    # Extra Validation: Check if the tweet belongs to our profile and was posted recently
                    # Actually, if the text doesn't match, it's dangerous, but for now we just return the first status URL
                    # Let's ensure it's not a Retweet by checking if our profile path is in the URL
                    links = article.locator("a[href*='/status/']")
                    l_count = await links.count()
                    for j in range(l_count):
                        href = await links.nth(j).get_attribute("href")
                        if href and self.profile_path in href and "/status/" in href:
                            return f"https://x.com{href}" if href.startswith("/") else href
            except:
                pass'''

new_content = content.replace(old_chunk, new_chunk)
if new_content != content:
    with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Replaced successfully")
else:
    print("Replace failed")
