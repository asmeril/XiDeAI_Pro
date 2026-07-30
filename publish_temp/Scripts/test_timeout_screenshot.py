import sys, asyncio, time
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
    
    await daemon._discover_profile()
    
    await daemon.page.goto("https://x.com/compose/post", wait_until="commit", timeout=20000)
    await asyncio.sleep(2)
    
    box = daemon.page.locator('div[data-testid="tweetTextarea_0"]').first
    await box.wait_for(state="visible", timeout=6000)
    await box.click()
    text = f"Test fallback {time.time()}"
    await box.fill(text)
    await asyncio.sleep(1)
    
    post_button = daemon.page.locator("button[data-testid='tweetButton']").first
    await post_button.evaluate("el => el.click()")
    await asyncio.sleep(3)
    
    print("Navigating to profile...")
    await daemon.page.goto(f"https://x.com{daemon.profile_path}", wait_until="domcontentloaded", timeout=20000)
    await asyncio.sleep(2)
    
    print("Taking screenshot BEFORE wait...")
    await daemon.page.screenshot(path="timeout_before_screenshot.png")
    
    links = daemon.page.locator("article[data-testid='tweet'] a[href*='/status/']")
    try:
        await links.first.wait_for(state="attached", timeout=6000)
    except Exception as e:
        print(f"Wait failed: {e}")
        
    count = await links.count()
    print(f"Found {count} links")
    
    print("Taking screenshot AFTER wait...")
    await daemon.page.screenshot(path="timeout_after_screenshot.png")
    
    await browser.close()
    p.stop()

asyncio.run(run())
