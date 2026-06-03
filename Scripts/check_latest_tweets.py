import sys, asyncio
from playwright.async_api import async_playwright

async def run():
    p = await async_playwright().start()
    browser = await p.chromium.launch(headless=True)
    context = await browser.new_context()
    page = await context.new_page()
    
    sys.path.append(r'C:\Program Files (x86)\XiDeAI Pro\Scripts')
    import playwright_daemon
    daemon = playwright_daemon.XDaemonPlaywright(visible=False)
    daemon.playwright = p
    daemon.browser = browser
    daemon.context = context
    daemon.page = page
    await daemon.load_cookies()
    
    await daemon._discover_profile()
    profile_url = f"https://x.com{daemon.profile_path}" if daemon.profile_path else "https://x.com/X_Hive_Pro"
    print(f"Checking profile: {profile_url}")
    
    await page.goto(profile_url, wait_until="domcontentloaded", timeout=20000)
    await asyncio.sleep(5)
    
    # Extract the texts of the first 3 tweets
    tweets = await page.locator("article[data-testid='tweet']").all()
    print(f"Found {len(tweets)} tweets on profile.")
    
    for i in range(min(len(tweets), 3)):
        try:
            text = await tweets[i].locator("div[data-testid='tweetText']").inner_text()
            print(f"\n--- TWEET {i} ---")
            print(text[:200])
        except Exception as e:
            print(f"Could not extract text for tweet {i}: {e}")
            
    await browser.close()
    p.stop()

asyncio.run(run())
