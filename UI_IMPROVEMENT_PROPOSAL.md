# 🎨 XiDeAI Pro - UI/UX Modernizasyon Planı (Revize: v2.0)

Kullanıcı geri bildirimleri ve "Finansal Analiz Odaklı" vizyon doğrultusunda revize edilmiş arayüz geliştirme planıdır.

---

## 1. 📉 Finansal Analiz Çekirdeği (Öncelik: Yüksek)
Uygulamanın kalbi olan **Sinyal**, **Manuel Analiz** ve **Üstat** modülleri en öne çıkarılacak ve kullanım kolaylığı artırılacaktır.

### A. Sinyal Merkezi 2.0 (Signal Center)
*   **KPI Paneli (Üst Bant):**
    *   Günlük Sinyal Sayısı / Başarılı Sinyal Oranı
    *   En Çok Kazandıran Strateji (Örn: "Bugün KING %80 başarılı")
    *   Piyasa Yönü (Trend) İndikatörü
*   **Akıllı Tablo (Smart Grid):**
    *   Metin tabanlı `DataGridView` yerine görselleştirilmiş liste.
    *   **Renkli Skorlar:** 25+ Yeşil, 20-25 Sarı, <20 Gri.
    *   **Yön Okları:** 🟢 Yükseliş, 🔴 Düşüş ikonları.
    *   **Inline Actions:** Satır üzerinde "Hemen Tweetle", "Grafik Aç", "Yoksay" butonları.
*   **Hızlı Filtre Barı:**
    *   Karmaşık checkboxlar yerine, iPhone tarzı "Chip" filtreler.
    *   `[KING] [BOMBA] [DIP] [15dk] [Saatlik]` (Seçili olan parlar).

### B. Manuel Analiz Masası
*   **Split View (Bölünmüş Ekran):**
    *   Sol: Analiz Parametreleri (Sembol, Periyot, Pazar).
    *   Sağ: Canlı Grafik Önizleme (WebView2 TradingView).
*   **Tek Tıkla Sihir:** "Analiz Et + Görsel Al + Tweetle" akışını tek butona indirme opsiyonu (Otomatik Onay modu).

---

## 2. 💬 Communication Hub (İletişim & Etkileşim)
Dağınık olan `Bot Etkileşim` ve `Etkileşim Merkezi` modülleri tek çatı altında birleştiriliyor.

### Yapı: Tab'lı İletişim Paneli
1.  **📥 Gelen Kutusu (Sentinel Feed):**
    *   Mevcut `Engagement Hub` buraya taşınacak.
    *   Kullanıcıdan gelen sorular, talepler ve yanıtlar burada akacak.
    *   Renkli etiketleme: `[TALEP]`, `[SORU]`, `[TEŞEKKÜR]`.
2.  **📤 Giden Kutusu & Onay (Pending):**
    *   `Guru` onay tablosu ve `Bot` viral tweet önerileri burada toplanacak.
    *   Operatörün onayına sunulan tüm içerikler (Drafts) burada listelenecek.
3.  **🤖 Bot Konfigürasyon:**
    *   Botun hedeflediği hashtagler, kişi listesi ve spam ayarları bu sekmede olacak.

---

## 3. ⚙️ Ayarlar Modernizasyonu (Settings 2.0)
Mevcut karışık TextBox yığını, kategorize edilmiş profesyonel bir yapıya dönüştürülecek.

### Tasarım: Split View (Sol Menü - Sağ Detay)
*   **Sol Menü (Kategoriler):**
    *   🔑 API & Bağlantılar (X, Telegram, Gemini, TradingView)
    *   🛡️ Limitler & Spam (Günlük Kota, Spam Koruması)
    *   📊 Strateji Ayarları (Sinyal Puanları, Zamanlayıcılar)
    *   🎨 Görünüm & Sistem
*   **Sağ Panel:**
    *   Seçilen kategoriye ait ayarlar.
    *   Her ayar için "?" ikonu ile açıklama (ToolTip).
    *   Geçersiz girişlerde anlık uyarı (Kırmızı çerçeve).

---

## 4. 🧭 Navigasyon (Sidebar) Düzenlemesi
12+ butonluk liste, fonksiyonel gruplara ayrılarak sadeleştirilecek.

*   **📊 ANALİZ (Finans)**
    *   Ana Ekran (Dashboard)
    *   Sinyal Merkezi
    *   Manuel Analiz
    *   Üstat Paneli
*   **🧠 ZEKA (HIVE)**
    *   HIVE Intel (Apex/Meta/Wisdom)
    *   Fenomenler
    *   Haberler
*   **⚙️ SİSTEM**
    *   Communication Hub (İletişim)
    *   Geçmiş & Loglar
    *   Ayarlar

---

**Sonuç:** Bu plan, XiDeAI Pro'yu "Finansal Analiz Asistanı" kimliğine tam olarak oturtacak ve operasyonel verimliliği %40+ artıracaktır.
