import asyncio
from playwright.async_api import async_playwright
import pickle
import time

async def main():
    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True)
        context = await browser.new_context(viewport={"width": 1200, "height": 1080})
        
        cookie_path = 'C:\\Users\\asmeril\\AppData\\Local\\XiDeAI\\twitter_cookies.pkl'
        with open(cookie_path, 'rb') as f:
            cookies = pickle.load(f)
            
        valid_cookies = []
        for c in cookies:
            if 'sameSite' in c:
                # Playwright expects Strict, Lax, None
                val = c['sameSite'].lower()
                if val == 'strict':
                    c['sameSite'] = 'Strict'
                elif val == 'lax':
                    c['sameSite'] = 'Lax'
                elif val == 'none':
                    c['sameSite'] = 'None'
                else:
                    del c['sameSite']
            if 'storeId' in c: del c['storeId']
            if 'id' in c: del c['id']
            valid_cookies.append(c)
            
        await context.add_cookies(valid_cookies)
        
        page = await context.new_page()
        await page.goto("https://x.com/")
        print("Went to x.com, waiting for load...")
        
        try:
            await page.wait_for_selector('[data-testid="AppTabBar_Profile_Link"]', timeout=30000)
            profile_link = await page.locator('[data-testid="AppTabBar_Profile_Link"]').get_attribute('href')
            username = profile_link.replace('/', '')
            print(f"Detected Username: {username}")
            
            await page.goto(f"https://x.com/{username}")
            await page.wait_for_selector('[data-testid="tweet"]', timeout=30000)
            print("Profile loaded, waiting 5 seconds for images...")
            await asyncio.sleep(5)
            
            screenshot_path = 'C:\\Users\\asmeril\\.gemini\\antigravity\\brain\\79a47994-3e7b-4b81-b87b-4d55e0f2c390\\artifacts\\kontr_tweet.png'
            await page.screenshot(path=screenshot_path)
            print(f"Screenshot saved to {screenshot_path}")
            
        except Exception as e:
            print(f"Error: {e}")
            screenshot_path = 'C:\\Users\\asmeril\\.gemini\\antigravity\\brain\\79a47994-3e7b-4b81-b87b-4d55e0f2c390\\artifacts\\kontr_error.png'
            await page.screenshot(path=screenshot_path)
            
        await browser.close()

asyncio.run(main())
