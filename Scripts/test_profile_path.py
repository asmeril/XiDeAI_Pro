import sys, asyncio, json, time
from playwright.async_api import async_playwright

async def run():
    try:
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
        
        await daemon.page.goto("https://x.com/compose/post", wait_until="domcontentloaded", timeout=20000)
        await asyncio.sleep(2)
        
        print("Testing _discover_profile from compose/post:")
        await daemon._discover_profile()
        print("Profile Path:", daemon.profile_path)
        
        await browser.close()
        p.stop()
    except Exception as e:
        print("ERROR:", str(e))

asyncio.run(run())
