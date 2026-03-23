---
description: XiDeAI Pro Güvenli Çıkış (Sadece Kayıt ve Temizlik)
---
// turbo-all

# /end - XiDeAI Pro Kapatma (Yazılımı Durdurmaz)

## 1) Gereksiz Dosya Temizliği
```powershell
Get-ChildItem -Path "Output" -Include "*.tmp", "*.log" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path "Scripts" -Include "__pycache__", "*.pyc" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
```

## 2) GitHub Yedekleme (Zorunlu)
```powershell
Set-Location "d:\MEGA\XiDeAI_Pro"
git status
git add .
git commit -m "chore: end workflow checkpoint - session saved"
git push origin master
```

## 3) NOT
Bu workflow **XiDeAI Pro** veya **x_daemon.py** süreçlerini durdurmaz. Yazılım sunucuda çalışmaya devam edecektir.
Eğer her şeyi tamamen kapatmak isterseniz `/kill-all` komutunu veya Görev Yöneticisi'ni kullanınız.
