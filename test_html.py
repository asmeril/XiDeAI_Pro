import asyncio, json
from playwright.async_api import async_playwright

async def run():
    try:
        p = await async_playwright().start()
        browser = await p.chromium.launch(headless=False)
        context = await browser.new_context()
        page = await context.new_page()
        
        import sys
        sys.path.append(r'C:\Program Files (x86)\XiDeAI Pro\Scripts')
        import playwright_daemon
        daemon = playwright_daemon.XDaemonPlaywright(visible=True)
        daemon.playwright = p
        daemon.browser = browser
        daemon.context = context
        daemon.page = page
        await daemon.load_cookies()
        
        await page.goto('https://x.com/compose/post', wait_until='commit')
        await asyncio.sleep(2)
        
        box = page.locator('div[data-testid=\"tweetTextarea_0\"]').first
        await box.wait_for(state='visible', timeout=6000)
        await box.click()
        await page.keyboard.insert_text('HTML toast check test: ' + str(asyncio.get_event_loop().time()))
        await asyncio.sleep(1)
        
        btn = page.locator('button[data-testid=\"tweetButton\"]').first
        await btn.evaluate('el => el.click()')
        await asyncio.sleep(2)
        
        toast = page.locator('div[data-testid=\"toast\"]')
        count = await toast.count()
        if count > 0:
            html = await toast.first.evaluate('el => el.innerHTML')
            print('TOAST_HTML:', html)
        else:
            print('TOAST_HTML: NOT_FOUND')
        
        await browser.close()
        p.stop()
    except Exception as e:
        print("ERROR:", str(e))

asyncio.run(run())
