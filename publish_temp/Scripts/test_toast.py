# -*- coding: utf-8 -*-
import sys, asyncio, json, time
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
        
        # Test _discover_profile behavior
        await page.goto("https://x.com/home", wait_until="domcontentloaded")
        await asyncio.sleep(2.0)
        await daemon._discover_profile()
        print('Profile Path after home:', daemon.profile_path)
        
        await page.goto('https://x.com/compose/post', wait_until='domcontentloaded')
        await asyncio.sleep(2)
        
        # Write a real tweet to test the toast
        box = page.locator('div[data-testid=\"tweetTextarea_0\"]').first
        await box.wait_for(state='visible', timeout=5000)
        chunk = f'Toast test {time.time()}'
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
        
        # Post
        post_btn = page.locator('button[data-testid=\"tweetButton\"]').first
        await post_btn.wait_for(state='visible', timeout=5000)
        await asyncio.wait_for(post_btn.evaluate('el => el.click()'), timeout=3.0)
        
        # Now wait for the toast and dump its HTML!
        print('Waiting for toast...')
        toast = page.locator('div[data-testid=\"toast\"], div[role=\"alert\"]').first
        await toast.wait_for(state='attached', timeout=10000)
        print('Toast appeared! Getting HTML...')
        html = await toast.evaluate('el => el.outerHTML')
        print('TOAST_HTML:', html)
        
        # Let's test _extract_latest_tweet_url directly
        url = await daemon._extract_latest_tweet_url()
        print('EXTRACTED_URL:', url)
        
        await browser.close()
        p.stop()
    except Exception as e:
        print("ERROR:", str(e))

asyncio.run(run())
