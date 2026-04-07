"""
X Compose Ekranı DOM İnceleme Scripti
--------------------------------------
Kullanım:
  1.  Chrome'u uzaktan debug modunda aç:
        chrome.exe --remote-debugging-port=9222
  2.  Tarayıcıda x.com'da oturum aç (zaten açıksa gerek yok)
  3.  Bu scripti çalıştır:
        python Scripts/inspect_compose_dom.py

Çıktı:
  - Konsola tüm [data-testid] elementleri listeler
  - Screenshots/ klasörüne ekran görüntüsü kaydeder
"""

import asyncio
import json
from pathlib import Path
from playwright.async_api import async_playwright


CDP_URL = "http://localhost:9222"
COMPOSE_URL = "https://x.com/compose/post"
SCREENSHOT_PATH = Path(__file__).parent.parent / "Screenshots" / "compose_dom_inspect.png"


async def main():
    SCREENSHOT_PATH.parent.mkdir(exist_ok=True)

    print(f"[inspect] Chrome CDP'ye bağlanılıyor: {CDP_URL}")
    async with async_playwright() as p:
        try:
            browser = await p.chromium.connect_over_cdp(CDP_URL)
        except Exception as e:
            print(f"[HATA] Chrome'a bağlanılamadı: {e}")
            print("Chrome'u şu komutla başlatın:")
            print('  chrome.exe --remote-debugging-port=9222')
            return

        # Mevcut contexti kullan (oturum açık)
        contexts = browser.contexts
        ctx = contexts[0] if contexts else await browser.new_context()
        page = await ctx.new_page()

        print(f"[inspect] Compose sayfasına gidiliyor: {COMPOSE_URL}")
        await page.goto(COMPOSE_URL, wait_until="networkidle", timeout=30000)
        await asyncio.sleep(2)

        current_url = page.url
        print(f"[inspect] Güncel URL: {current_url}")

        if "login" in current_url or "flow" in current_url:
            print("[HATA] Oturum açık değil veya yönlendirme oldu. Chrome'da X'e giriş yapın.")
            await browser.close()
            return

        # Tüm data-testid elementlerini topla
        testids = await page.evaluate("""
            () => {
                const results = [];
                document.querySelectorAll('[data-testid]').forEach(el => {
                    results.push({
                        testid: el.dataset.testid,
                        tag: el.tagName.toLowerCase(),
                        role: el.getAttribute('role') || '',
                        ariaLabel: el.getAttribute('aria-label') || '',
                        visible: el.offsetParent !== null
                    });
                });
                return results;
            }
        """)

        # Deduplicate + sırala
        seen = set()
        unique = []
        for item in testids:
            key = (item['testid'], item['tag'])
            if key not in seen:
                seen.add(key)
                unique.append(item)
        unique.sort(key=lambda x: x['testid'])

        print(f"\n{'='*60}")
        print(f"  Bulunan benzersiz [data-testid] elementleri ({len(unique)} adet)")
        print(f"{'='*60}")
        for item in unique:
            vis = "✓" if item['visible'] else "·"
            role = f" role={item['role']}" if item['role'] else ""
            aria = f" aria-label='{item['ariaLabel']}'" if item['ariaLabel'] else ""
            print(f"  {vis}  [{item['testid']}]  <{item['tag']}>{role}{aria}")

        # Özellikle + butonu var mı?
        print(f"\n{'='*60}")
        print("  ADD BUTTON araması:")
        print(f"{'='*60}")
        add_candidates = [x for x in testids if 'add' in x['testid'].lower()]
        if add_candidates:
            for c in add_candidates:
                print(f"  BULUNDU → [{c['testid']}]  <{c['tag']}>  aria-label='{c['ariaLabel']}'  görünür={c['visible']}")
        else:
            print("  *** 'add' içeren hiçbir data-testid bulunamadı! ***")
            print("  aria-label ile arama...")
            aria_adds = await page.evaluate("""
                () => {
                    const results = [];
                    document.querySelectorAll('[aria-label]').forEach(el => {
                        const label = el.getAttribute('aria-label').toLowerCase();
                        if (label.includes('add') || label.includes('ekle')) {
                            results.push({
                                tag: el.tagName.toLowerCase(),
                                ariaLabel: el.getAttribute('aria-label'),
                                testid: el.dataset.testid || '(yok)',
                                outerHTML: el.outerHTML.substring(0, 200)
                            });
                        }
                    });
                    return results;
                }
            """)
            for a in aria_adds:
                print(f"  aria → <{a['tag']}> aria-label='{a['ariaLabel']}' data-testid={a['testid']}")
                print(f"         {a['outerHTML']}")

        # Ekran görüntüsü al
        await page.screenshot(path=str(SCREENSHOT_PATH), full_page=False)
        print(f"\n[inspect] Ekran görüntüsü kaydedildi: {SCREENSHOT_PATH}")

        # JSON olarak da kaydet
        out_json = SCREENSHOT_PATH.with_suffix('.json')
        out_json.write_text(json.dumps(unique, ensure_ascii=False, indent=2), encoding='utf-8')
        print(f"[inspect] Tam lista JSON: {out_json}")

        await browser.close()


if __name__ == "__main__":
    asyncio.run(main())
