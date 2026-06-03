import sys, re

with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'r', encoding='utf-8') as f:
    content = f.read()

start_idx = content.find('    async def _post_reply_in_thread(self, parent_url: str, text: str) -> dict:')
if start_idx == -1:
    print('Start not found')
    sys.exit(1)

end_str = '            except Exception as e:\n                return {"status": "error", "message": str(e)}'
end_idx = content.find(end_str, start_idx)
if end_idx == -1:
    print('End not found')
    sys.exit(1)

end_idx += len(end_str)
old_chunk = content[start_idx:end_idx]

replacement = '''    async def _post_reply_in_thread(self, parent_url: str, text: str) -> dict:
        for attempt in range(1, 4):
            try:
                parent_id = None
                m = re.search(r'/status/(\d+)', parent_url)
                if m:
                    parent_id = m.group(1)

                if not parent_id:
                    raise PlaywrightTimeoutError("Tweet id could not be parsed for thread chaining")

                # Bypass the reply button modal completely to avoid DOM detachment timeouts.
                compose_url = f"https://x.com/compose/post?in_reply_to={parent_id}"
                await self.page.goto(compose_url, wait_until="domcontentloaded", timeout=20000)
                await asyncio.sleep(2.0)

                # 2) Compose box'u bul
                compose_box = None
                for sel in [
                    'div[data-testid="tweetTextarea_0"]',
                    'div[data-testid^="tweetTextarea_"]',
                    'div[role="textbox"][contenteditable="true"]',
                ]:
                    try:
                        cand = self.page.locator(sel).first
                        await cand.wait_for(state="visible", timeout=6000)
                        compose_box = cand
                        break
                    except:
                        pass

                if not compose_box:
                    # Ikinci fallback: prefilled URL
                    from urllib.parse import quote as urlquote
                    prefilled = f"https://x.com/compose/post?in_reply_to={parent_id}&text={urlquote(text, safe='')}"
                    await self.page.goto(prefilled, wait_until="domcontentloaded", timeout=20000)
                    await asyncio.sleep(1.5)
                    for sel in ['div[data-testid="tweetTextarea_0"]', 'div[role="textbox"][contenteditable="true"]']:
                        try:
                            cand = self.page.locator(sel).first
                            await cand.wait_for(state="visible", timeout=4000)
                            compose_box = cand
                            break
                        except:
                            pass

                if not compose_box:
                    raise PlaywrightTimeoutError("Reply compose box not found")

                try:
                    await self._click_publish(compose_box, "compose_focus")
                except:
                    try:
                        await asyncio.wait_for(compose_box.evaluate("el => el.focus()"), timeout=3.0)
                    except:
                        pass

                # Robust text insertion (from XHive x_daemon)
                text_inserted = False
                try:
                    await compose_box.fill(text)
                    await asyncio.sleep(0.6)
                    current_text = await compose_box.inner_text()
                    if len(current_text.strip()) >= len(text.strip()) * 0.8:
                        text_inserted = True
                except:
                    pass
                
                if not text_inserted:
                    try:
                        await compose_box.focus()
                        await compose_box.type(text, delay=20)
                        await asyncio.sleep(0.5)
                        current_text = await compose_box.inner_text()
                        if len(current_text.strip()) >= len(text.strip()) * 0.8:
                            text_inserted = True
                    except:
                        pass
                
                if not text_inserted:
                    try:
                        await asyncio.wait_for(self.page.evaluate("""
                            (element, text) => {
                                element.focus();
                                element.innerText = text;
                                element.dispatchEvent(new Event('input', { bubbles: true }));
                                element.dispatchEvent(new Event('change', { bubbles: true }));
                            }
                        """, await compose_box.element_handle(), text), timeout=3.0)
                        await asyncio.sleep(0.5)
                    except:
                        pass
                
                # Wake up React
                try:
                    await compose_box.press(" ")
                    await asyncio.sleep(0.1)
                    await compose_box.press("Backspace")
                    await asyncio.sleep(0.5)
                except:
                    pass

                # 4) Post butonunu bekle (max 5 sn)
                post_btn = None
                for _ in range(10):
                    for sel in [
                        "button[data-testid='tweetButton']",
                        "div[data-testid='tweetButton']",
                        "button[data-testid='tweetButtonInline']",
                        "div[data-testid='tweetButtonInline']",
                    ]:
                        cand = self.page.locator(sel).first
                        try:
                            await cand.wait_for(state="visible", timeout=500)
                            if await cand.get_attribute("aria-disabled") != "true" and await cand.is_enabled():
                                post_btn = cand
                                break
                        except:
                            pass
                    if post_btn:
                        break
                    await asyncio.sleep(0.5)

                if not post_btn:
                    raise PlaywrightTimeoutError("Reply button disabled")

                await self._click_publish(post_btn, "reply")
                await asyncio.sleep(3)

                tweet_url = await self._extract_latest_tweet_url()
                return {"status": "success", "tweet_url": tweet_url}

            except PlaywrightTimeoutError as e:
                if attempt == 3:
                    return {"status": "error", "message": str(e)}
                await asyncio.sleep(3)
            except Exception as e:
                return {"status": "error", "message": str(e)}'''

new_content = content.replace(old_chunk, replacement)
if new_content != content:
    with open(r'D:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Replaced successfully")
else:
    print("Replace failed")
