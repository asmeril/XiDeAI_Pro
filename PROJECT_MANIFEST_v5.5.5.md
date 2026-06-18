# XiDeAI Pro - Release Manifest v5.5.5
Tarih: 2026-06-18

## 1. Temel Geliştirmeler (Features)
- **PackTweets Algoritması:** Kısa dönen veya parçalanan tweet parçalarının 280 karaktere sığacak şekilde birleştirilmesi (anlam bütünlüğü korundu).
- **Manuel Fenomen Tarama Butonu:** Bot etkileşim arayüzüne eklenen buton ile 45 dakikalık döngü beklenmeden hedeflerin taranması sağlandı.

## 2. Hata Düzeltmeleri (Bug Fixes)
- **Cümle Bölünme Sorunu:** Tweetlerin cümlenin ortasından kaba kuvvetle bölünmesi engellendi. Öncelik sırası paragraf (\n\n) -> satır sonu (\n) -> noktalama işareti (.!?) olarak değiştirildi.
- **Bot Konu Taraması Fallback:** Trendlerde eşleşme olmaması durumunda girdiğiniz sabit 'Konular' üzerinden rastgele tarama başlatılması sağlandı.

## 3. UI İyileştirmeleri (UI/UX)
- **Üstat Paneli (Guru Center):** Sol taraftaki tweet gridlerinin genişliği SplitterDistance 650'den 450'ye düşürülerek daraltıldı; analiz ve grafik tarafına daha fazla önizleme alanı bırakıldı.
- **Yapay Zeka Promptları:** Bot etkileşimlerindeki yanıtların şablon ve klişe olmaması için PromptManager kısıtlamaları güçlendirildi.
