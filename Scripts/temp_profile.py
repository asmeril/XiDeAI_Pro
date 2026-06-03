import sys

with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'r', encoding='utf-8') as f:
    content = f.read()

old_func = '''    async def _discover_profile(self):
        if self.profile_path: return
        try:
            profile_link = self.page.locator('a[data-testid="AppTabBar_Profile_Link"]')
            await profile_link.wait_for(state="visible", timeout=3000)
            href = await profile_link.get_attribute("href")
            if href:
                self.profile_path = href
                print(f"[playwright_daemon] Discovered profile path: {self.profile_path}")
        except:
            pass'''

new_func = '''    async def _discover_profile(self):
        if self.profile_path: return
        try:
            current_url = self.page.url
            if "x.com/home" not in current_url:
                await self.page.goto("https://x.com/home", wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(2.0)
                
            profile_link = self.page.locator('a[data-testid="AppTabBar_Profile_Link"]')
            await profile_link.wait_for(state="attached", timeout=6000)
            href = await profile_link.get_attribute("href")
            if href:
                self.profile_path = href
                print(f"[playwright_daemon] Discovered profile path: {self.profile_path}", flush=True)
        except Exception as e:
            print(f"[playwright_daemon] Failed to discover profile: {e}", flush=True)'''

new_content = content.replace(old_func, new_func)
if new_content != content:
    with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Replaced _discover_profile successfully")
else:
    print("Replace failed")
