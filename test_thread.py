"""
XiDeAI Pro - Thread Üretim Testi
GeminiService.GeneratePremiumNewsAnalysisPrompt() ile birebir aynı prompt.
Model: gemini-2.5-flash (Benchmark kazananı, 416ms)
"""

import os, sys, json, urllib.request, textwrap

API_KEY = os.environ.get("GEMINI_API_KEY", "").strip()
MODEL   = "gemini-2.5-flash"

# --- Test Haberi (gerçekçi örnek) ---
HABER = {
    "title"  : "ABD Çin'e Ek Gümrük Vergisi Uygulayacak: Tarife %145'e Yükseldi",
    "source" : "Reuters",
    "summary": "ABD Başkanı tarafından açıklanan son karar ile Çin mallarına uygulanan gümrük vergisi %145'e çıkarıldı. "
               "Çin'in misilleme yapacağı ve küresel tedarik zincirlerinin olumsuz etkileneceği öngörülüyor.",
    "link"   : "https://www.reuters.com/world/us-china-tariffs-2026"
}

# --------------------------------------------------------------------------
def build_prompt(title, source, summary, link):
    """GeminiService.GeneratePremiumNewsAnalysisPrompt() ile birebir aynı."""
    link_section = f"🔗 {link}" if link else f"🔗 Kaynak: {source}"
    return f"""KİMLİK: Sen deneyimli ve profesyonel bir Baş Ekonomist ve Stratejistsin.

GÖREV: Aşağıdaki haberi, X platformunda viral olacak, derinlemesine yorumlayan, 3 ile 4 tweet arasında değişen bir analiz thread'ine dönüştür. Haberin sadece ekonomik etkileri değil, toplumsal, jeopolitik ve stratejik silsilesini (Second-Order Effects) düşün.
Haberin önemi yüksekse bunu yansıt — 'güvenli' ve sıkıcı olmaktan kaçın. Okuyucuyu durduracak bir kanca kur.

HABER BİLGİLERİ:
- Başlık: {title}
- Özet: {summary}
- Kaynak: {source}
- Link: {link_section}

X ALGORİTMASI DAĞITIM STRATEJİLERİ (UYGULA):
1. 🎯 AKILLI ETİKETLEME (Auto-Mention): SADECE resmi kurumları, kamu şirketlerini veya zararsız teknoloji oluşumlarını etiketle. KESİN YASAK: Asla siyasetçileri, parti liderlerini, bakanları veya tartışmalı figürleri etiketleme.
2. 🧲 KANCA (Hook): İlk tweet o kadar çarpıcı olmalı ki kullanıcı kaydırmayı durdurmalı. Şok edici bir veri, retorik soru veya sarsıcı bir tespitle başla.
3. 🧠 DERİN ANALİZ: Haberin yüzeyini değil, altını analiz et. "Bu neden oldu?" ve "Bunun sonucunda 3 ay, 1 yıl sonra ne olacak?" sorularını cevapla. Zincirleme etkiler (domino) ortaya çıkar.
4. 📈 SEMANTİK KELİMELER: X algoritmasının "For You" kısmında öne çıkardığı anahtar kelimeleri (Borsa, Ekonomi, Teknoloji, Yatırım, Savunma, Jeopolitik) hashtag kullanmadan metne doğal yedir.

ÇIKTI FORMATI VE KURALLAR:
- Analizini KISA ve ÖZ şekilde 3 veya 4 tweet halinde yaz. Asla destan yazma, kelime israfından kaçın. Her tweet 270 karakterin ALTINDA KALMAK ZORUNDADIR. (Haber çok önemliyse 4, orta düzeydeyse 3 tweet.)
- Tweetleri birbirinden ayırmak için KESİNLİKLE aralarına üç boru karakteri koy (ayırıcı).
- Bolca profesyonel emoji kullan.
- İlk tweet, çarpıcı kancayı ve sonuna {link_section} eklemeli.
- Son tweet akıllı hashtagler (#BIST100 #Ekonomi #Savunma vb.) ve Y.T.D. ile bitmelidir.
- Tweet numaraları veya bölüm başlıkları ASLA yazma. Çıktın SADECE tweet metinleri ve aralarındaki ayırıcılardan oluşmalı."""
# --------------------------------------------------------------------------

def call_gemini(prompt: str, api_key: str, model: str) -> str:
    url     = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"
    payload = json.dumps({
        "contents": [{"parts": [{"text": prompt}]}],
        "generationConfig": {"temperature": 0.3, "maxOutputTokens": 1500}
    }).encode()
    req = urllib.request.Request(url, data=payload,
                                 headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req, timeout=30) as r:
        data = json.loads(r.read())
    return data["candidates"][0]["content"]["parts"][0]["text"]

def print_thread(raw: str):
    separator = "|||"
    tweets = [t.strip() for t in raw.split(separator) if t.strip()]
    print("\n" + "="*60)
    print(f"  ÜRETILEN THREAD  ({len(tweets)} tweet)")
    print("="*60)
    for i, tweet in enumerate(tweets, 1):
        char_count = len(tweet)
        status = "✅" if char_count <= 280 else f"⚠️ UZUN ({char_count})"
        print(f"\n{'─'*60}")
        print(f"  Tweet {i}/{len(tweets)}  [{char_count} karakter] {status}")
        print(f"{'─'*60}")
        print(textwrap.fill(tweet, width=60, subsequent_indent="  "))
    print("\n" + "="*60)

# --------------------------------------------------------------------------
if __name__ == "__main__":
    if not API_KEY:
        print("HATA: GEMINI_API_KEY ortam değişkeni tanımlı değil.")
        print("  Kullanım: $env:GEMINI_API_KEY='API_ANAHTARIN' ; python test_thread.py")
        sys.exit(1)

    print(f"Model   : {MODEL}")
    print(f"Haber   : {HABER['title']}")
    print("API Key : ****" + API_KEY[-4:])
    print("\nÜretiliyor...")

    prompt = build_prompt(**HABER)
    try:
        raw = call_gemini(prompt, API_KEY, MODEL)
        print_thread(raw)
    except Exception as e:
        print(f"\n❌ HATA: {e}")
