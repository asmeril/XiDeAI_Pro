import asyncio
from playwright.async_api import async_playwright
import pickle

async def main():
    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True)
        context = await browser.new_context()
        
        cookie_path = 'C:\\Users\\asmeril\\AppData\\Local\\XiDeAI\\twitter_cookies.pkl'
        with open(cookie_path, 'rb') as f:
            cookies = pickle.load(f)
            
        valid_cookies = []
        for c in cookies:
            if 'sameSite' in c:
                val = c['sameSite'].lower()
                if val == 'strict': c['sameSite'] = 'Strict'
                elif val == 'lax': c['sameSite'] = 'Lax'
                elif val == 'none': c['sameSite'] = 'None'
                else: del c['sameSite']
            if 'storeId' in c: del c['storeId']
            if 'id' in c: del c['id']
            valid_cookies.append(c)
            
        await context.add_cookies(valid_cookies)
        
        page = await context.new_page()
        await page.goto("https://x.com/X_Hive_Pro")
        print("Went to profile, waiting for load...")
        
        try:
            await page.wait_for_selector('[data-testid="tweet"]', timeout=30000)
            await asyncio.sleep(2)
            
            tweets = await page.locator('[data-testid="tweet"]').all()
            for i, tweet in enumerate(tweets[:5]):
                text = await tweet.inner_text()
                print(f"--- TWEET {i+1} ---")
                print(text.replace('\n', ' | '))
                
        except Exception as e:
            print(f"Error: {e}")
            
        await browser.close()

asyncio.run(main())
