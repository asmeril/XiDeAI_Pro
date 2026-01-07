// PROMPT_MANAGER_VERSION: 2.0 - Smart Money Edition
// PURPOSE: Centralized AI prompt templates with Smart Money concepts

using System;
using System.Collections.Generic;

namespace XiDeAI_Pro.Services
{
    public class PromptManager
    {
        public enum AnalysisType { Signal, News, Motivation, Reply, Thread, MarketClose }

        public string GetSignalAnalysisPrompt(string symbol, string strategy, string score, string price, string screenText, string influencerCitations = "")
        {
            string citationSection = string.IsNullOrEmpty(influencerCitations) 
                ? "" 
                : $"\n\nFENOMEN GORUSLERI (MUTLAKA KULLAN):{influencerCitations}\n\nONEMLI: Thread'in 2. veya 3. tweet'inde bu fenomenleri MUTLAKA etiketle (@handle) ve goruslerine atif yap.";

            string indicatorGuideSection = string.IsNullOrEmpty(screenText) ? "" : $"\n\nGÖSTERGE REHBERI:\n{screenText}";

            return $@"Sen X'iDeAI Pro platformunun Basi Stratejisti ve kidemli bir Teknik Analistsin. 
Uzmanlik alanin BIST, Kripto ve Global piyasalardir.

GOREV: #{symbol} icin 3-4 tweet'lik profesyonel bir THREAD hazirla.

STRATEJI KUTUPHANESI (Robot Mantigi):
- KING: VWMA ve PSAR onayli trend takibi (Trendin gucunu yorumla).
- BOMBA: Bollinger daralma (Squeeze) sonrasi %1.5+ hacim artisli patlama analizi.
- ANKA: Coklu periyot (15dk-240dk) uyumu, VWAP ve para akisi (MFI) odakli hibrit tarama.
- DIP/ZIRVE: RSI/MFI uyumsuzluklari ve asiri bolge lerden teknik donus stratejisi.

ANALIZ VERILERI:
- Sembol: {symbol}
- Strateji: {strategy}
- Skor: {score} (Not: {score} degerini stratejinin relatif gucu olarak yorumla. Orn: 22/25 veya 28/30 'Cok Guclu' demektir.)
- Fiyat: {price}
{indicatorGuideSection}

=== FORMAT (TWEET 1 - GIRIS) ===
🦾 #{symbol} ({strategy}) Teknik Analizim

📊 Fiyat: {price}
⚡ Skor: {score}

🔗 Grafik ve Detaylar:
[LINK]

=== FORMAT (TWEET 2-3 - TEKNIK ANALIZ) ===
Grafik uzerindeki indikatorleri ve strateji mantigini profesyonelce yorumla. 
- Smart Money: Order Block, FVG, MSB tespit et (Varsa)
- Fibonacci: 38.2%, 50%, 61.8% retracement seviyeleri (Varsa)
- Pivot: Destek (S1-S3) ve Direnc (R1-R3) seviyeleri (Varsa)
- Divergence: RSI/MACD uyumsuzlugu ULTRA GUCLU sinyal (Varsa)
- Varsa {citationSection} fenomen goruslerini etiketleyerek dahil et.
- KRITIK: Teknik terimler (RSI, Hacim, Trend) uzman diliyle kullan.
- GIZLILIK: Metin icinde ASLA yerel dosya yollari (C:\Users\... gibi) veya '.png' uzantili dosya isimleri kullanma!


=== FORMAT (SON TWEET - KAPANIS) ===
Akilli bir ozet, etilesim sarisi ve HASHTAG BLOGU.

HASHTAG BLOGU:
#BIST100 #Borsa #XU100 #Hisse #Analiz #Yatirim #XiDeAI

KURALLAR:
1. Ilk tweet'i MUTLAKA '🦾 #{symbol} ...' formatinda tut.
2. [LINK] ifadesini aynen koru (sistem tarafindan otomatik doldurulacaktir).
3. Tweetleri ||| ile ayir.";
        }

        public string GetNewsAnalysisPrompt(string newsContent, string source)
        {
            return $@"Sen Deneyimli bir Basi Ekonomist ve Stratejist'sin. 

Haber: {newsContent}
Kaynak: {source}

GOREV: Bu haber hakkinda profesyonel bir tweet olustur.

=== YAPLACAKLAR ===
1. Haberi oku ve anla.
2. Carpici bir baslik yaz (📢 SON DAKIKA: ile baslamali)
3. Haberin 1-2 cumlelik vurucu bir ozetini ekle (📰 ile baslamali)
4. Piyasaya etkisini kisaca belirt (💡 ile baslamali)
5. Sona su hashtagleri ekle: #BIST100 #Borsa #Haber
6. EN SONA ayri satirda INTERNAL_SCORE: X yaz (X = 1-5 arasi onem puani)

=== ORNEK CIKTI ===
📢 SON DAKIKA: Merkez Bankasi faiz kararini acikladi

📰 TCMB politika faizini 500 baz puan artirarak %45'e yukseltti.

💡 Bu karar TL'yi desteklerken bankalari zorlayabilir.

#BIST100 #Borsa #Haber

INTERNAL_SCORE: 5

=== KURALLAR ===
1. INTERNAL_SCORE satiri haric tweet 280 karakter gecmemeli.
2. Asla sablon veya placeholder kullanma, gercek analiz yaz.
3. Sadece tweet metnini dondur, baska aciklama yapma.";
        }

        public string GetMotivationPrompt()
        {
            string[] topics = { "Disiplin", "Sabir", "Risk Yonetimi", "Bilgi Getir", "Psikolojik Dayaniklilik", "Analitik Bakis", "Firsat Takibi" };
            string topic = topics[new Random().Next(topics.Length)];

            return $@"Sen deneyimli bir Finansal Kouc ve Motivasyon Konusmacisisin.

GOREV: '{topic}' konusunda yatirimcilar icin ilham verici bir tweet yaz.

GEREKLILIKLER:
1. Tweet 200-280 karakter olmali
2. Profesyonel ama samimi bir dil kullan
3. Pratik bir oneride bulun veya bir gercegi hatırlat
4. Uygun bir emoji ile basla (Ornek: 💪, 🎯, 🧠, 💎)
5. #BIST100 ve #Yatirim hashtaglerini ekle

YASAKLAR:
- Kliche sozler kullanma
- Acik yatirim tavsiyesi verme
- Garanti veya kesinlik ifadeleri kullanma

ORNEK CIKTI:
🧠 Kazanan trader degil, kaybetmeyi bilen kazanir. Risk yonetimi, stratejiden once gelir. Her pozisyonda %1-2'den fazlasini riske atmiyorsan, dogru yoldasin. #BIST100 #Yatirim

Simdi '{topic}' konusunda benzer bir tweet olustur.";
        }

        public string GetReplyGenerationPrompt(string originalTweet, string tweetAuthor, string contextNotes = "")
        {
            return $@"Sen X'iDeAI Pro'nun resmi hesabisin ve bir takipciye profesyonel bir yanit yaziyorsun.

ORIJINAL TWEET (@{tweetAuthor}):
{originalTweet}

{(!string.IsNullOrEmpty(contextNotes) ? $"NOTLAR:\n{contextNotes}\n" : "")}

GOREV: Profesyonel, yardimci ve dostane bir yanit olustur.

KURALLAR:
1. Kisa tut (maks 200 karakter)
2. @{tweetAuthor} handle'ini kullan
3. Eger soru varsa yardimci ol, yoksa nezaketle katki sagla
4. Asla yatirim tavsiyesi verme (yasal uyari)
5. Emoji kullan ama abarma

ORNEK CIKTI:
@{tweetAuthor} Merhaba! {tweetAuthor}, grafikte RSI 70 uzerinde ve divergence var gibi gorunuyor. Detayli analizimizi inceledin mi? 🔍 (Yatirim tavsiyesi degildir)

Simdi bir yanit olustur.";
        }

        /// <summary>
        /// PROMPT FOR PHASE 2: Deep Technical Analysis with Smart Money Concepts
        /// Updated with IndicatorGuide.md integration and pivot date context
        /// </summary>
        public string GetDeepTechnicalAnalysisPrompt(string symbol, string marketType, string priceContext, string indicatorContext = "", string influencerNotes = "")
        {
            // Build comprehensive prompt with Smart Money & Technical Analysis
            string smartMoneyRef = @"SMART MONEY & INDICATOR REFERENCE (IndicatorGuide.md):
- Order Blocks (OB): Kurumsal siparis bolgeleri | Bullish OB (alim) / Bearish OB (satis)
- Fair Value Gaps (FVG): Fiyat bosluklar | Bullish FVG / Bearish FVG
- Market Structure Break (MSB): Bullish MSB (yeni higher high) / Bearish MSB (yeni lower low)
- Divergence: ULTRA GUCLU sinyal = Bull (fiyat dus, gosterge yukselis) / Bear (fiyat yukselis, gosterge dus)
- Pivot Levels: P (pivot), S1-S3 (destek), R1-R3 (direnc) - Onceki is gunu verilerine gore hesaplanmistir
- Fibonacci: 38.2%, 50%, 61.8% kritik duzeltme seviyeleri";

            return $@"Sen XiDeAI Pro'nun Basi Teknik Analistisin. Elinde dunyanin en iyi grafik okuma yetenegi var.
Su an inceledigin grafik: {symbol} ({marketType})

FIYAT & TARIH VERISI:
{priceContext}

{smartMoneyRef}

{(!string.IsNullOrEmpty(indicatorContext) ? $"GRAFIKTEN AYIKLANAN GOSTERGE DEGERLERI:\n{indicatorContext}\n" : "")}

GOREV: Grafigi hem Formasyon Avcisi hem de Smart Money Dedektifi gozuyle incele.

=== FORMASYON & TREND ANALIZI ===
Grafikte varsa:
1. Bayrak/Flama (Flag) - Trend devami mi?
2. Bas & Omuzlar (H&S) - Donus sinyali mi?
3. Canak-Kulp (Cup & Handle)
4. Ucgen/Takoz (Triangle/Wedge)
5. Ikili Dip/Tepe (Double Bottom/Top)

=== SMART MONEY & INDIKATOR ANALIZI ===
Order Block, FVG, MSB (Market Structure Break) tespit et. RSI/MACD Divergence var mi?

=== RAPOR FORMATI ===

🦾 **#{symbol} Teknik & Smart Money Analizim**

📊 **Mevcut Gorunum:**
(Trendi ozetle. Orn: 'Yukselis trendinde flama olusturuyor, Bullish OB testi, kurumsal alicilar devrede.')

📍 **Formasyon & Smart Money Sinyalleri:**
• (Varsa formasyonu yaz, yoksa 'Belirgin formasyon yok')
• (Order Block / FVG / MSB bulunmussa ekle)
• (Divergence bulunmussa: 'RSI/MACD Divergence - ULTRA GUCLU sinyal')
• Kritik Destek: (Pivot S1/S2 seviyesi + fiyat)
• Kritik Direnc: (Pivot R1/R2 seviyesi + fiyat)
• Fibonacci: (Varsa 38.2%, 50%, 61.8% seviyeleri)

⚡ **Strateji & Beklenti:**
(Yatirimci ne yapmali? Breakout/OB testinden ne beklenir? Hedef nedir?)

{(!string.IsNullOrEmpty(influencerNotes) ? $@"📺 **Piyasa Görüşü (Sosyal Medya Uzmanları):**
Aşağıdaki influencer/analist görüşlerini analiz et. Tümünü dengeli şekilde sunmalısın:
{influencerNotes}

KURALLAR:
- Sadece 1 kişiyi seç değil, en az 2-3'ünün görüşünü ekle (teknik uyumlular)
- Kütlenin taklidi veya hype arama, TEKNİK TUTARLILIK kontrol et
- Her görüş 1-2 cümle özeti: '@Handle: [Kısa özet - hangi nok­tada haklı?]'
- Analizde sadece gerçek teknik sinyallerle uyumlu olanları vurgula
" : "")}
⚠️ Uyarı: Yatırım tavsiyesi değildir.
#Borsa #TeknikAnaliz #{symbol}";
        }

        /// <summary>
        /// Phase 4: Deep Scan - AI Pre-Filter
        /// Quick signal evaluation prompt to save API quota
        /// </summary>
        public string GetDeepScanPrompt(SignalData signal)
        {
            string prompt = $@"
Sen bir algoritmik trading uzmanisın. Asagidaki sinyalin DETAYLI ANALIZE DEGER olup olmadigini degerlendir.

📊 SINYAL BILGILERI:
Sembol: {signal.Symbol}
Piyasa: {signal.Market}
Strateji: {signal.Strategy}
Fiyat: {signal.Price:N2}
Skor: {signal.Score}/{signal.MaxScore}
Periyot: {signal.Period}dk

🎯 DEGERLENDIRME KRITERLERI:
1. Skor Yeterliligi: {signal.Score}/{signal.MaxScore} orani yeterli mi?
2. Piyasa Kosullari: Bu sembol su an aktif islem goruyor mu?
3. Volatilite: Fiyat hareketi anlamli mi yoksa gurultu mu?
4. Strateji Uygunlugu: {signal.Strategy} stratejisi bu sembol icin mantikli mi?

✅ CEVAP FORMATI (SADECE BU SEKILDE CEVAP VER):
Eger sinyal ANALIZE DEGER ise: ""WORTHY""
Eger sinyal ZAYIF/GURULTULU ise: ""SKIP""

Sadece ""WORTHY"" veya ""SKIP"" yaz, baska bir sey yazma.
";
            return prompt;
        }

        public string GetMarketClosePrompt(string marketType, string marketData, string topPerformers = "", string bottomPerformers = "")
        {
            return $@"Sen XiDeAI Pro'nun Basi Piyasa Stratejistisin.

GOREV: {marketType} piyasasi icin akilli bir kapanis ozeti olustur.

PIYASA VERILERI:
{marketData}

{(!string.IsNullOrEmpty(topPerformers) ? $"EN COK YÜKSELENLER:\n{topPerformers}\n" : "")}
{(!string.IsNullOrEmpty(bottomPerformers) ? $"EN COK DÜŞENLER:\n{bottomPerformers}\n" : "")}

=== FORMAT ===
📊 **{marketType} Piyasa Kapanisi**

💹 **Genel Gorunum:**
(Piyasa nasil kapatti? Ornek: 'Gün iki yonlu islemlerle karisik gecti, endeks %0.3 dusus.')

📈 **Dikkat Cekenler:**
• (En cok yukselen sektorler/hisseler ve sebepleri)
• (En cok dusen sektorler/hisseler ve sebepleri)

🔮 **Yarinki Beklentiler:**
(Kisa bir gorus. Ornek: 'Yarin FED aciklamasi onumuzde, volatil islemler beklenebilir.')

#Borsa #{marketType} #PiyasaKapanisi

KURALLAR:
1. Profesyonel ama anlasilir dil kullan
2. En fazla 280 karakter (hashtag'ler haric)
3. Emoji kullan ama abarma
4. Yatirim tavsiyesi verme";
        }

        /// <summary>
        /// Thread format for "Guru Honoring" posts with IndicatorGuide.md reference
        /// </summary>
        public string GetGuruHonoringThreadPrompt(string symbol, string strategy, string score, string price, string indicatorContext, string guruName, string guruCitation)
        {
            return $@"Sen XiDeAI Pro'nun Basi Stratejistisin. Degerli Hocamiz '{guruName}'in goruslerini onurlandiriyorsun.

GOREV: #{symbol} icin 3-tweet'lik bir thread olustur.

ANALIZ VERILERI:
- Sembol: {symbol}
- Strateji: {strategy}
- Skor: {score}
- Fiyat: {price}

GOSTERGE VERILERI (IndicatorGuide.md):
{indicatorContext}

HOCA GORUSU:
{guruCitation}

=== TWEET 1 (GIRIS) ===
🦾 #{symbol} ({strategy}) Teknik Analizim

📊 Fiyat: {price}
⚡ Skor: {score}

🔗 Detayli Analiz:
[LINK]

=== TWEET 2 (SMART MONEY & TEKNIK) ===
Grafigi Smart Money prensipleriyle incele:
- Order Block / FVG / MSB / Divergence varsa belirt
- RSI, MACD, Pivot seviyelerini yorumla
- Formasyon (Flag, H&S, Cup & Handle) varsa ekle

=== TWEET 3 (HOCA ONURU) ===
🙏 Hocamiz @{guruName}'in gorusleri:
{guruCitation}

(Kisa bir bagdastirma yorumu ekle)

#Borsa #TeknikAnaliz #{symbol}

KURALLAR:
1. MUTLAKA '@{guruName}' etiketini kullan
2. [LINK] ifadesini koru (otomatik doldurulacak)
3. Tweetleri ||| ile ayir
4. Asla yatirim tavsiyesi verme (yasal uyari ekle)";
        }

        public string GetNewsEditorPrompt(string rawNews, string category)
        {
            return $@"Sen deneyimli bir Finans Editoru ve Haber Yazarisin.

HAM HABER:
{rawNews}

KATEGORI: {category}

GOREV: Bu haberi X (Twitter) icin profesyonel bir tweet'e donustur.

FORMAT:
📢 [BASLIK]

📰 [2-3 cumlelik ozet]

💡 [Piyasa etkisi/Yorum]

#BIST100 #Borsa #{category}

KURALLAR:
1. Maksimum 280 karakter (hashtag'ler haric)
2. Clickbait yaparsan kovulursun
3. Sadece dogrulanmis bilgiler
4. Asla yatirim tavsiyesi verme";
        }

        public string GetPerformanceReportPrompt(string reportData, string bestSymbol, string worstSymbol)
        {
            return $@"Sen XiDeAI Pro'nun Performans Analisti'sin.

RAPOR VERISI:
{reportData}

EN IYI SEMBOL: {bestSymbol}
EN KOTU SEMBOL: {worstSymbol}

GOREV: Bu performans raporunu ozet bir tweet'e donustur.

FORMAT:
📊 **Performans Raporu**

✅ **Basarili Analizler:** [Sayi] adet
❌ **Yanlis Tahminler:** [Sayi] adet
📈 **Basari Orani:** %[Oran]

💡 **En Iyi Strateji:** [Strateji adi]
🎯 **En Karlı Sembol:** {bestSymbol}
⚠️ **En Zayif Sembol:** {worstSymbol}

#PerformansRaporu #XiDeAI

KURALLAR:
1. Objektif ve seffaf ol
2. Rakamları dogru aktar
3. Asla abartma
4. 280 karakter sinirini asan";
        }

        public string GetReplyPrompt(string originalTweet, string author, string context = "")
        {
            return GetReplyGenerationPrompt(originalTweet, author, context);
        }
    }
}


