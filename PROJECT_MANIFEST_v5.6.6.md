# XiDeAI Pro - v5.6.6 Manifest

## Sürüm Bilgisi
- **Versiyon:** 5.6.6
- **Tarih:** 15 Temmuz 2026
- **Ana Odak:** Selenium/ChromeDriver arka plan CMD penceresi kapatılması (beyaz flash sorunu) ve XHive Telegram OTP döngüsünün durdurulması.

## Yapılan Değişiklikler

### 1. Beyaz Pencere (Flash) Düzeltmesi
- **Sorun:** undetected_chromedriver'in `use_subprocess=True` durumunda başlattığı Chrome süreçlerinin Windows üzerinde siyah/beyaz CMD pencerelerini bir anlığına gösterip kapatması (`CREATE_NO_WINDOW` eksikliği).
- **Çözüm:** Kullanıcıya ait global Python ortamındaki `undetected_chromedriver` paketinin (`__init__.py`) içine `creationflags=0x08000000` (`subprocess.CREATE_NO_WINDOW`) bayrağı eklendi.
- **Temizlik:** Önceki denemelerden kalma, global `subprocess.Popen` yapısını bozan yamalar `social_intel.py` ve `x_daemon.py` betiklerinden silindi.

### 2. XHive Telegram OTP Döngüsü
- **Sorun:** XHive'ın `telegram_source.py` modülü `client.start()` kullanarak Telegram'a bağlanıyordu. Oturum süresi dolduğunda bu metot otomatik olarak SMS veya Telegram üzerinden kullanıcıya kod gönderiyordu (sürekli giriş mesajı yağmuru).
- **Çözüm:** `start()` yerine sadece bağlantı kuran `connect()` kullanıldı. Oturum geçersizse kod atmadan hata dönülecek şekilde (`is_user_authorized()` kontrolü ile) düzeltildi. 

### 3. Telegram Hub Aktif Edildi
- XHive Telegram hub özelliği (`TELEGRAM_HUB_ENABLED=true`) yeniden aktifleştirildi (öncesinde çakışma olduğu sanılarak kapatılmıştı ancak XiDeAI Pro'nun Telegram bot tokenı olmadığı için çakışma ihtimali yoktu).

## Etkilenen Dosyalar
- `Scripts/social_intel.py` (XiDeAI Pro)
- `Scripts/x_daemon.py` (XiDeAI Pro)
- `C:\Users\ttevf\AppData\Local\XHive\worker\intel\telegram_source.py` (XHive)
- `C:\Users\ttevf\AppData\Local\XHive\worker\.env` (XHive)

## Yayınlama (Deploy)
- v5.6.6 sürümü başarıyla derlendi ve Inno Setup kurulum paketi oluşturuldu.
- Master branch'ine push edildi.
