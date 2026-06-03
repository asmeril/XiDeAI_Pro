import sys, asyncio, time
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
        
        print("[TEST] Setting up profile path...")
        await daemon._discover_profile()
        print(f"[TEST] Profile Path: {daemon.profile_path}")
        
        # MOCK the toast selectors to simulate a missed toast
        print("[TEST] Mocking toast selectors to force Profile Fallback...")
        original_extract = daemon._extract_latest_tweet_url
        
        async def mocked_extract():
            print("[TEST] Entering mocked _extract_latest_tweet_url")
            # Skip toast logic directly and run fallback
            if daemon.profile_path:
                print(f"[TEST] Executing profile fallback to https://x.com{daemon.profile_path}...")
                try:
                    await daemon.page.goto(f"https://x.com{daemon.profile_path}", wait_until="domcontentloaded", timeout=20000)
                    await asyncio.sleep(2.0)

                    links = daemon.page.locator("article[data-testid='tweet'] a[href*='/status/']")
                    print("[TEST] Waiting for timeline links to attach...")
                    try:
                        await links.first.wait_for(state="attached", timeout=6000)
                    except Exception as e:
                        print(f"[TEST] Wait for attached failed: {e}")
                        pass
                    
                    count = await links.count()
                    print(f"[TEST] Found {count} tweet links on profile!")
                    for i in range(min(count, 3)):
                        href = await links.nth(i).get_attribute("href")
                        print(f"[TEST] Link {i}: {href}")
                        if href and "/status/" in href:
                            final_url = f"https://x.com{href}" if href.startswith("/") else href
                            print(f"[TEST] Selected URL: {final_url}")
                            return final_url
                except Exception as e:
                    print(f"[TEST] Profile fallback crashed: {e}")
            
            print("[TEST] Reached Plan C (current url)!")
            return daemon.page.url
            
        daemon._extract_latest_tweet_url = mocked_extract
        
        # Now let's post a single tweet!
        test_text = f"Final Fallback Verification Test {time.time()}"
        print(f"[TEST] Posting tweet: {test_text}")
        res = await daemon._post_single_tweet(test_text, images=None)
        
        print("\n--- TEST RESULT ---")
        print(res)
        
        await browser.close()
        p.stop()
    except Exception as e:
        print("ERROR:", str(e))

asyncio.run(run())
