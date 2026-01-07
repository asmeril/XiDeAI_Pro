# XiDeAI Pro - Geçmiş ve Geri Alma Rehberi (History & Rollback)

Bu proje artık yerel olarak **Git** ile takip edilmektedir. Yapılan her hata veya istenmeyen değişiklik güvenli bir şekilde geri alınabilir.

## 🛠 Temel Komutlar

### 1. Geçmişi Görüntüleme
Yapılan tüm değişiklikleri ve "commit" mesajlarını görmek için:
```powershell
git log --oneline
```

### 2. Değişiklikleri Geri Alma (Rollback)
Eğer son yaptığın değişiklikleri bozduysan ve en son çalışan sürüme dönmek istiyorsan:
```powershell
git checkout .
```
*(Dikkat: Kaydedilmemiş tüm son değişikliklerin silinir)*

### 3. Belirli Bir Tarihe/Sürüme Dönme
`git log` ile gördüğün bir ID'ye (örneğin `a1b2c3d`) dönmek için:
```powershell
git checkout a1b2c3d
```

## 📦 Manuel Yedekleme
Önemli bir geliştirme yapmadan önce projenin tam bir kopyasını `.zip` olarak almak istersen:
1. `backup_project.ps1` dosyasını sağ tıkla -> **Run with PowerShell**.
2. Yedeklerin `d:\Projects\XiDeAI_Pro_Backups` klasöründe saklanacaktır.

---
**Not:** Bu sistem tamamen yerel çalışır, GitHub hesabına gerek duymaz.
