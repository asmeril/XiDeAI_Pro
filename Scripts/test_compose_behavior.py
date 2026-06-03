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
    
    print("Going to compose/post...")
    await daemon.page.goto("https://x.com/compose/post", wait_until="commit", timeout=20000)
    await asyncio.sleep(2)
    
    box = daemon.page.locator('div[data-testid="tweetTextarea_0"]').first
    await box.wait_for(state="visible", timeout=6000)
    await box.click()
    text = f"Behavior Test {time.time()}"
    await box.fill(text)
    await asyncio.sleep(1)
    
    post_button = daemon.page.locator("button[data-testid='tweetButton']").first
    await post_button.evaluate("el => el.click()")
    
    print("Clicked post. Monitoring URL and Toast for 15 seconds...")
    for i in range(15):
        url = daemon.page.url
        toast_count = await daemon.page.locator("div[data-testid='toast']").count()
        print(f"Sec {i}: URL={url}, Toasts={toast_count}")
        await asyncio.sleep(1)
    
    await browser.close()
    p.stop()

asyncio.run(run())
