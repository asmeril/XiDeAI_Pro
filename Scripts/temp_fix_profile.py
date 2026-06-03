import sys

with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'r', encoding='utf-8') as f:
    content = f.read()

old_chunk = '''        # 2) Fallback to own profile; pick highest status id among own links.
        if self.profile_path:
            try:
                await self.page.goto(self.profile_path, wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(1.5)

                links = self.page.locator("article[data-testid='tweet'] a[href*='/status/']")
                count = await links.count()'''

new_chunk = '''        # 2) Fallback to own profile; pick highest status id among own links.
        if self.profile_path:
            try:
                await self.page.goto(self.profile_path, wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(2.0)

                links = self.page.locator("article[data-testid='tweet'] a[href*='/status/']")
                try:
                    await links.first.wait_for(state="attached", timeout=6000)
                except:
                    pass
                count = await links.count()'''

new_content = content.replace(old_chunk, new_chunk)
if new_content != content:
    with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Replaced successfully")
else:
    print("Replace failed")
