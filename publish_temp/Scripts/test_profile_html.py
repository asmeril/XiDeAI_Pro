import sys, asyncio
from playwright.async_api import async_playwright

async def run():
    p = await async_playwright().start()
    browser = await p.chromium.launch(headless=True)
    page = await browser.new_page()
    await page.goto("https://x.com/X_Hive_Pro", wait_until="domcontentloaded", timeout=20000)
    await asyncio.sleep(5.0)
    
    html = await page.evaluate("document.body.innerHTML")
    with open('profile_html.txt', 'w', encoding='utf-8') as f:
        f.write(html)
        
    await browser.close()
    p.stop()

asyncio.run(run())
