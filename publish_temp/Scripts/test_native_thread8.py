# -*- coding: utf-8 -*-
import sys, asyncio, json
from playwright.async_api import async_playwright

async def run():
    try:
        p = await async_playwright().start()
        browser = await p.chromium.launch(headless=False)
        context = await browser.new_context()
        page = await context.new_page()
        
        sys.path.append(r'D:\MEGA\XiDeAI_Pro\Scripts')
        import playwright_daemon
        daemon = playwright_daemon.XDaemonPlaywright(visible=True)
        daemon.playwright = p
        daemon.browser = browser
        daemon.context = context
        daemon.page = page
        await daemon.load_cookies()
        
        print('Navigating to compose/post')
        await page.goto('https://x.com/compose/post', wait_until='domcontentloaded')
        await asyncio.sleep(2)
        
        chunks = [
            f'Native UI threading test pt1 {asyncio.get_event_loop().time()}',
            f'Native UI threading test pt2 {asyncio.get_event_loop().time()}',
            f'Native UI threading test pt3 {asyncio.get_event_loop().time()}'
        ]
        
        for i, chunk in enumerate(chunks):
            print(f'Writing chunk {i+1}')
            
            # 1. Wait for textarea to appear
            compose_boxes = page.locator('div[role=\"textbox\"][contenteditable=\"true\"]')
            await compose_boxes.nth(i).wait_for(state='visible', timeout=10000)
            box = compose_boxes.nth(i)
            
            # Robust text insertion
            await box.fill(chunk)
            await asyncio.sleep(0.5)
            await asyncio.wait_for(box.evaluate('''
                (element, text) => {
                    element.focus();
                    element.innerText = text;
                    element.dispatchEvent(new Event('input', { bubbles: true }));
                    element.dispatchEvent(new Event('change', { bubbles: true }));
                }
            ''', chunk), timeout=3.0)
            await box.press(' ')
            await box.press('Backspace')
            await asyncio.sleep(0.5)
            
            # 2. Click + (add) button if this is not the last chunk
            if i < len(chunks) - 1:
                print('Clicking + button')
                add_btn = page.locator('[aria-label=\"Add\"], [aria-label=\"Ekle\"], [aria-label=\"Gönderi ekle\"], [aria-label=\"G\u00f6nderi ekle\"], [data-testid=\"addTweetButton\"]')
                await add_btn.first.wait_for(state='visible', timeout=5000)
                await asyncio.wait_for(add_btn.first.evaluate('el => el.click()'), timeout=3.0)
                await asyncio.sleep(1.5)
        
        # 3. Post the thread
        print('Posting thread')
        post_btn = page.locator('button[data-testid=\"tweetButton\"]')
        await post_btn.first.wait_for(state='visible', timeout=5000)
        await asyncio.wait_for(post_btn.first.evaluate('el => el.click()'), timeout=3.0)
        await asyncio.sleep(3)
        
        print('SUCCESS')
        
        await browser.close()
        p.stop()
    except Exception as e:
        print("ERROR:", str(e))

asyncio.run(run())
