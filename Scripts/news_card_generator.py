# -*- coding: utf-8 -*-
"""
news_card_generator.py — XiDeAI Pro v4.7.3
Haber görseli oluşturucu.

Kullanım:
    python news_card_generator.py --title "Haberin Başlığı" --output "C:\\path\\card.png" [--flash] [--source "AA"] [--logo "C:\\path\\logo.png"]

    --flash   : "FLAŞ HABER" kartı üretir (kırmızı kenarlı)
    --normal  : "ÖNEMLİ HABER" kartı üretir (mavi kenarlı, varsayılan)
"""

import argparse
import os
import sys
import textwrap

# PIL yoksa kurulumu öner
try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    print("PIL kütüphanesi bulunamadı. Yükleniyor...", file=sys.stderr)
    os.system("pip install Pillow")
    from PIL import Image, ImageDraw, ImageFont

# ─── AYARLAR ─────────────────────────────────────────────────────────────────
CARD_W, CARD_H = 1200, 675          # Twitter/X önerilen 1.78:1 oranı
BRAND_ACCENT_FLASH  = "#E63946"     # Kırmızı — Flaş Haber
BRAND_ACCENT_NORMAL = "#1D3557"     # Lacivert — Önemli Haber
BG_DARK    = "#0D1117"              # Arka plan
BG_PANEL   = "#161B22"              # Panel rengi
TEXT_WHITE  = "#FFFFFF"
TEXT_SUBTLE = "#8B949E"

FONT_MAX_SIZE = 54
FONT_MIN_SIZE = 30
# ─────────────────────────────────────────────────────────────────────────────


def find_font(bold=False):
    """Sistemde kullanılabilecek Türkçe destekli bir yazı tipi bul."""
    candidates_bold = [
        "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/calibrib.ttf",
        "C:/Windows/Fonts/arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ]
    candidates_regular = [
        "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/calibri.ttf",
        "C:/Windows/Fonts/arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    ]
    candidates = candidates_bold if bold else candidates_regular
    for path in candidates:
        if os.path.exists(path):
            return path
    return None  # Pillow varsayılan fontu kullanır


def auto_fit_font(draw, text, max_width, font_path, max_size=FONT_MAX_SIZE, min_size=FONT_MIN_SIZE):
    """Yazıyı verilen genişliğe sığdıran en büyük font boyutunu döndürür."""
    size = max_size
    while size >= min_size:
        try:
            font = ImageFont.truetype(font_path, size) if font_path else ImageFont.load_default()
        except Exception:
            font = ImageFont.load_default()
        bbox = draw.textbbox((0, 0), text, font=font)
        if bbox[2] - bbox[0] <= max_width:
            return font, size
        size -= 2
    return font, size


def wrap_text_to_lines(text, draw, font, max_width):
    """Metni satırlara böler."""
    words = text.split()
    lines = []
    current = ""
    for word in words:
        test = (current + " " + word).strip()
        bbox = draw.textbbox((0, 0), test, font=font)
        if bbox[2] - bbox[0] <= max_width:
            current = test
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines


def generate_news_card(title: str, output_path: str, is_flash: bool = False,
                       source: str = "XiDeAI Pro", logo_path: str | None = None):
    """Haber kartı PNG dosyası oluşturur."""
    accent = BRAND_ACCENT_FLASH if is_flash else BRAND_ACCENT_NORMAL
    label  = "🚨  FLAŞ HABER" if is_flash else "📰  ÖNEMLİ HABER"

    # ── Taban görüntüsü ──────────────────────────────────────────────────────
    img  = Image.new("RGB", (CARD_W, CARD_H), BG_DARK)
    draw = ImageDraw.Draw(img)

    # ── Sol şerit (accent rengi) ─────────────────────────────────────────────
    draw.rectangle([(0, 0), (10, CARD_H)], fill=accent)

    # ── Üst panel ────────────────────────────────────────────────────────────
    draw.rectangle([(0, 0), (CARD_W, 100)], fill=BG_PANEL)
    draw.line([(10, 100), (CARD_W, 100)], fill=accent, width=2)

    # ── Logo ─────────────────────────────────────────────────────────────────
    logo_w = 0
    if logo_path and os.path.exists(logo_path):
        try:
            logo = Image.open(logo_path).convert("RGBA")
            logo.thumbnail((80, 80), Image.LANCZOS)
            # Beyaz arka plan üzerine yapıştır
            logo_bg = Image.new("RGBA", logo.size, BG_PANEL)
            logo_bg.paste(logo, mask=logo.split()[3])
            img.paste(logo_bg.convert("RGB"), (20, 10))
            logo_w = logo.width + 35
        except Exception as e:
            print(f"Logo yüklenemedi: {e}", file=sys.stderr)
            logo_w = 0

    # ── Üst bant — Etiket ────────────────────────────────────────────────────
    label_font_path = find_font(bold=True)
    try:
        label_font = ImageFont.truetype(label_font_path, 32) if label_font_path else ImageFont.load_default()
    except Exception:
        label_font = ImageFont.load_default()

    draw.text((logo_w + 20, 30), label, font=label_font, fill=accent)

    # ── Başlık alanı ─────────────────────────────────────────────────────────
    title_area_x1 = 40
    title_area_y1 = 130
    title_area_w  = CARD_W - 80

    title_font_path = find_font(bold=True)
    title_font, used_size = auto_fit_font(
        draw, title, title_area_w,
        title_font_path, max_size=56, min_size=28
    )
    lines = wrap_text_to_lines(title, draw, title_font, title_area_w)

    line_h = used_size + 10
    total_text_h = line_h * len(lines)
    y_start = title_area_y1 + (CARD_H - 100 - 80 - total_text_h) // 2

    for line in lines:
        draw.text((title_area_x1, y_start), line, font=title_font, fill=TEXT_WHITE)
        y_start += line_h

    # ── Alt panel — Kaynak bilgisi ────────────────────────────────────────────
    draw.rectangle([(0, CARD_H - 70), (CARD_W, CARD_H)], fill=BG_PANEL)
    draw.line([(10, CARD_H - 70), (CARD_W, CARD_H - 70)], fill=accent, width=1)

    src_font_path = find_font(bold=False)
    try:
        src_font = ImageFont.truetype(src_font_path, 22) if src_font_path else ImageFont.load_default()
    except Exception:
        src_font = ImageFont.load_default()

    draw.text((30, CARD_H - 50), f"Kaynak: {source}", font=src_font, fill=TEXT_SUBTLE)

    # XiDeAI watermark (sağ alt)
    wm_text = "xideai.pro"
    wb = draw.textbbox((0, 0), wm_text, font=src_font)
    wm_w = wb[2] - wb[0]
    draw.text((CARD_W - wm_w - 20, CARD_H - 50), wm_text, font=src_font, fill=TEXT_SUBTLE)

    # ── Kaydet ───────────────────────────────────────────────────────────────
    out_dir = os.path.dirname(output_path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)
    img.save(output_path, "PNG", optimize=True)
    
    # stdout hatasini onlemek icin guvenli ASCII metin
    print(f"OK Image created: {output_path}", file=sys.stdout)


if __name__ == "__main__":
    # Konsol encoding hatalarini onlemek icin
    if sys.stdout.encoding.lower() != 'utf-8':
        try:
            sys.stdout.reconfigure(encoding='utf-8')
        except AttributeError:
            pass
    parser = argparse.ArgumentParser(description="XiDeAI Haber Kartı Üreticisi")
    parser.add_argument("--title",  required=True, help="Haber başlığı")
    parser.add_argument("--output", required=True, help="Çıktı PNG dosya yolu")
    parser.add_argument("--flash",  action="store_true", default=False, help="Flaş haber modu")
    parser.add_argument("--source", default="XiDeAI Pro", help="Kaynak adı")
    parser.add_argument("--logo",   default=None, help="Logo dosyası (.ico veya .png)")
    args = parser.parse_args()

    generate_news_card(
        title=args.title,
        output_path=args.output,
        is_flash=args.flash,
        source=args.source,
        logo_path=args.logo
    )
