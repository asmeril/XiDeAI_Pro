import sys, asyncio
from playwright.async_api import async_playwright

async def run():
    p = await async_playwright().start()
    browser = await p.chromium.launch(headless=False)
    context = await browser.new_context()
    page = await context.new_page()
    
    sys.path.append(r'C:\Program Files (x86)\XiDeAI Pro\Scripts')
    import playwright_daemon
    daemon = playwright_daemon.XDaemonPlaywright(visible=True)
    daemon.playwright = p
    daemon.browser = browser
    daemon.context = context
    daemon.page = page
    await daemon.load_cookies()
    
    print("Navigating to profile...")
    await page.goto("https://x.com/X_Hive_Pro", wait_until="domcontentloaded", timeout=20000)
    await asyncio.sleep(5)
    
    print("Taking screenshot...")
    await page.screenshot(path="profile_test_screenshot.png")
    
    links = page.locator("article[data-testid='tweet'] a[href*='/status/']")
    count = await links.count()
    print(f"Found {count} links!")
    
    await browser.close()
    p.stop()

asyncio.run(run())
