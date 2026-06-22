"""
TradingView Screenshot Script with OHLC Data Extraction
Kullanım: python screenshot.py SYMBOL PERIOD OUTPUT_DIR CHART_ID [CHROMEDRIVER_PATH]
Uses webdriver-manager for automatic ChromeDriver download
Fetches OHLC data from yfinance for pivot calculation
"""
import sys
import os
import io

# Force UTF-8 for Windows console to prevent 'charmap' encoding errors
if sys.platform == "win32":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
import time
import json
from datetime import datetime, timedelta
import threading
import re

# Try to use webdriver-manager for automatic driver management
try:
    from webdriver_manager.chrome import ChromeDriverManager
    USE_WDM = True
except ImportError:
    USE_WDM = False

# Try to use yfinance for OHLC data
try:
    import yfinance as yf
    import pandas as pd
    USE_YFINANCE = True
except ImportError:
    USE_YFINANCE = False
    print("Warning: yfinance not installed. Install with: pip install yfinance")


def wait_for_chart(driver, timeout=30):
    """Wait until the TradingView chart canvas is visible before shooting."""
    try:
        WebDriverWait(driver, timeout).until(
            EC.visibility_of_element_located((By.CSS_SELECTOR, ".chart-gui-wrapper canvas"))
        )
        time.sleep(2)  # small settle for paints
        return True
    except Exception as e:
        print(f"Warning: Chart canvas not ready after {timeout}s - {e}")
        return False

def normalize_symbol(symbol):
    if not symbol:
        return ""
    raw = symbol.strip().upper().replace("'", "").replace('"', "").replace("`", "")
    if ":" in raw:
        prefix, rest = raw.split(":", 1)
        return f"{prefix}:{normalize_symbol(rest)}"
    while raw.startswith("VIPVIP-"):
        raw = "VIP-" + raw[len("VIPVIP-"):]
    if raw.startswith("VIP-"):
        raw = raw[4:]
    return re.sub(r"[^A-Z0-9!._-]", "", raw)


def get_ohlc_data(symbol, interval='1d'):
    """
    Get OHLC data from yfinance for pivot calculation
    interval: '1d' for daily, '1wk' for weekly, '1mo' for monthly
    Returns: dict with High, Low, Close, Open from previous closed trading session
    """
    if not USE_YFINANCE:
        print("Warning: yfinance not available, skipping OHLC fetch")
        return None
    
    try:
        # Convert symbol format for yfinance
        ticker = normalize_symbol(symbol)
        if ':' in ticker:
            ticker = ticker.split(':')[1]  # BIST:THYAO -> THYAO
        
        # v3.9.3: Clear VIP- prefix which causes yfinance 404
        ticker = ticker.replace("VIP-", "")

        # Crypto: BTCUSDT -> BTC-USD, ETHUSDT -> ETH-USD, etc.
        if ticker.endswith("USDT"):
            ticker = ticker[:-4] + "-USD"
        elif ticker.endswith("BUSD"):
            ticker = ticker[:-4] + "-USD"

        if not any(x in ticker for x in ['.', '^', '=', '-USD', '-BTC']):
            ticker = f"{ticker}.IS"        # Calculate start date based on interval to ensure enough data
        end_date = datetime.now()
        days_back = 15
        if interval == '1wk': days_back = 60
        if interval == '1mo': days_back = 365
        
        start_date = end_date - timedelta(days=days_back)
        
        print(f"[OHLC] Downloading {ticker} interval={interval} from {start_date.date()} to {end_date.date()}...")
        data = yf.download(ticker, start=start_date, end=end_date, interval=interval, progress=False, group_by='ticker')
        
        if data.empty:
            print(f"Warning: No OHLC data found for {ticker}")
            return None

        # Handle multi-index columns if they exist (Robust check)
        if isinstance(data.columns, pd.MultiIndex):
             try:
                 # Check if ticker is in the top level
                 if ticker in data.columns.levels[0]:
                    data = data[ticker]
                 # Or maybe level 1? Just try to xs or simple access
                 elif ticker in data.columns.levels[1]:
                    data = data.xs(ticker, axis=1, level=1)
                 else:
                    # Fallback: Just drop the top level if it's Price
                    data = data.droplevel(0, axis=1)
             except Exception:
                 # Last resort: if 1st level is Price/Adj Close, drop it
                 if data.columns.nlevels > 1:
                     data.columns = data.columns.droplevel(0)

        # Normalize columns to Title Case (Open, High, Low, Close) to match expected keys
        # This handles cases where yfinance returns 'open' or 'OPEN'
        old_cols = list(data.columns)
        new_cols = []
        for c in old_cols:
            if isinstance(c, str):
                new_cols.append(c.capitalize()) # 'open' -> 'Open'
            else:
                new_cols.append(c)
        data.columns = new_cols

        # LOGIC FIX: Determine if the last bar is "Today" (incomplete) or "Previous session" (closed)
        # On Sunday, the last bar in the 1d dataframe will likely be Friday's close.
        # We check the date of the last row.
        last_row_date = data.index[-1].date()
        today = datetime.now().date()
        
        # If today is Monday-Friday and the last row is today, today's bar is live/incomplete.
        # We want the last CLOSED session.
        if last_row_date == today:
            target_idx = -2 # Use previous day/week/month
            print(f"[OHLC] Today's bar ({last_row_date}) detected. Using previous bar for pivot calculation.")
        else:
            target_idx = -1 # The last bar is a fully closed session (e.g. Friday data on a Sunday)
            print(f"[OHLC] Last bar date ({last_row_date}) is not today. Using last bar for pivot calculation.")

        if len(data) < abs(target_idx):
             print(f"Warning: Not enough history for {ticker} at {interval}")
             return None

        row = data.iloc[target_idx]
        
        # Helper to extract scalar from series/dataframe safely
        def get_val(series, col):
            val = series[col]
            if hasattr(val, 'iloc'): return float(val.iloc[0])
            return float(val)

        ohlc_data = {
            'date': str(data.index[target_idx].date()),
            'open': get_val(row, 'Open'),
            'high': get_val(row, 'High'),
            'low': get_val(row, 'Low'),
            'close': get_val(row, 'Close'),
            'symbol': symbol,
            'interval': interval,
            'source': 'yfinance'
        }
        
        print(f"OHLC Data ({ohlc_data['date']}): O={ohlc_data['open']:.2f} H={ohlc_data['high']:.2f} L={ohlc_data['low']:.2f} C={ohlc_data['close']:.2f}")
        return ohlc_data
        
    except Exception as e:
        import traceback
        print(f"Warning: Could not fetch OHLC data for {symbol} - {e}")
        traceback.print_exc()
        return None


def calculate_pivots(ohlc_data):
    """
    Calculate pivot levels from OHLC data
    Formula: P = (H+L+C)/3
    """
    if not ohlc_data:
        return None
    
    try:
        h = ohlc_data['high']
        l = ohlc_data['low']
        c = ohlc_data['close']
        
        p = (h + l + c) / 3
        s1 = (2 * p) - h
        s2 = p - (h - l)
        s3 = l - 2 * (h - p)
        r1 = (2 * p) - l
        r2 = p + (h - l)
        r3 = h + 2 * (p - l)
        
        interval = ohlc_data.get('interval', '1d')
        interval_label = "GÜNLÜK"
        if interval == '1wk': interval_label = "HAFTALIK"
        if interval == '1mo': interval_label = "AYLIK"

        pivots = {
            'pivot': round(p, 2),
            's1': round(s1, 2),
            's2': round(s2, 2),
            's3': round(s3, 2),
            'r1': round(r1, 2),
            'r2': round(r2, 2),
            'r3': round(r3, 2),
            'interval_label': interval_label,
            'calculated_from_date': ohlc_data['date'],
            'valid_for_date': str((datetime.strptime(ohlc_data['date'], '%Y-%m-%d') + timedelta(days=1)).date())
        }
        
        print(f"Pivots calculated - P:{pivots['pivot']} R1:{pivots['r1']} S1:{pivots['s1']}")
        return pivots
        
    except Exception as e:
        print(f"Error calculating pivots: {e}")
        return None


def take_screenshot(symbol, period="60", output_dir="screenshots", chart_id="GDHgGCEv", chromedriver_path=None):
    """
    TradingView'den grafik ekran görüntüsü al ve pivot hesapla
    chart_id: Kullanıcının özel TradingView chart ID'si
    chromedriver_path: Opsiyonel, özel chromedriver.exe yolu
    """
    # Import PIL early to avoid scope issues with json module
    try:
        from PIL import Image, ImageDraw, ImageFont
        PILLOW_AVAILABLE = True
    except ImportError:
        PILLOW_AVAILABLE = False
    
    # Çıktı klasörü
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)

    # STEP 1: Determine interval and start OHLC fetch in background
    yf_interval = '1d' # Default daily
    if period.upper() in ["H", "W", "WEEKLY"]: 
        yf_interval = '1wk'
    elif period.upper() in ["A", "M", "MONTHLY"]:
        yf_interval = '1mo'
        
    ohlc_container = {"data": None}
    def fetch_ohlc_bg():
        print(f"\n[OHLC] Fetching {yf_interval} OHLC data for {symbol} in background...")
        ohlc_container["data"] = get_ohlc_data(symbol, interval=yf_interval)
    
    ohlc_thread = threading.Thread(target=fetch_ohlc_bg, daemon=True)
    ohlc_thread.start()
    
    # ... (Wait for pivots later) ...

    # Chrome options (QHD @ 4.0x - Optimized for readable pivot labels)
    chrome_options = Options()
    chrome_options.add_argument("--headless=new")
    chrome_options.add_argument("--window-size=2560,1440") # QHD resolution
    chrome_options.add_argument("--disable-gpu")
    chrome_options.add_argument("--no-sandbox")
    chrome_options.add_argument("--disable-dev-shm-usage")
    chrome_options.add_argument("--disable-blink-features=AutomationControlled")
    chrome_options.add_argument("--force-device-scale-factor=4.0") # 4x scale for maximum label readability
    chrome_options.add_experimental_option("excludeSwitches", ["enable-automation"])

    # ... (lines 214-290 omitted, standard logic) ... (Wait I cannot omit lines in replacement content unless I use multi replace or match exact block)
    # Actually, I will replace the options block and the mouse interaction block separately or rewritten whole function if easier.
    # Let's replace the whole TAKE_SCREENSHOT function body parts to be safe.
    # Wait, replace_file_content requires exact match.
    # I will replace the chrome_options block first.


    # Periyot mapping
    period_map = {
        "5": "5",
        "15": "15", 
        "60": "60",
        "240": "240",
        "G": "D",
        "D": "D",
        "H": "W",
        "A": "M",
        "Y": "12M"
    }
    tv_period = period_map.get(period, "60")
    
    # Build URL based on symbol format
    tv_symbol = normalize_symbol(symbol)
    
    if ":" in tv_symbol:
        pass
    elif "USDT" in tv_symbol:
        tv_symbol = f"BINANCE:{tv_symbol}"
    elif any(fx in tv_symbol for fx in ["XAUUSD", "XAGUSD", "EURUSD", "GBPUSD", "USDJPY", "USDTRY"]):
        tv_symbol = f"FX:{tv_symbol}"
    elif tv_symbol.endswith("1!"):
        tv_symbol = f"BIST:{tv_symbol}"
    elif len(tv_symbol) >= 3 and len(tv_symbol) <= 6 and tv_symbol.isalpha():
        tv_symbol = f"BIST:{tv_symbol}"
    
    # Build URL with toolbar hiding parameters
    url = f"https://tr.tradingview.com/chart/{chart_id}/?symbol={tv_symbol}&interval={tv_period}&theme=dark"

    driver = None
    try:
        # Priority 1: Use provided chromedriver path
        if chromedriver_path and os.path.exists(chromedriver_path):
            print(f"DEBUG: Using custom chromedriver: {chromedriver_path}")
            service = Service(executable_path=chromedriver_path)
            try:
                driver = webdriver.Chrome(service=service, options=chrome_options)
            except Exception as e:
                # Catch version mismatch (SessionNotCreatedException)
                if "SessionNotCreated" in str(e) or "version" in str(e).lower():
                    print(f"Warning: Custom driver version mismatch! Falling back to auto-manager. Error: {e}")
                    chromedriver_path = None # Trigger fallback below
                else:
                    raise e # Re-raise other errors

        # Priority 2: Use webdriver-manager (Auto Update)
        if not driver:
            if USE_WDM:
                print("DEBUG: Using webdriver-manager (Auto Update)")
                try:
                    driver = webdriver.Chrome(service=Service(ChromeDriverManager().install()), options=chrome_options)
                except Exception as wdm_e:
                     print(f"Warning: webdriver-manager failed - {wdm_e}")

        # Priority 3: Fallback to system PATH
        if not driver:
            print("DEBUG: Using system PATH chromedriver")
            driver = webdriver.Chrome(options=chrome_options)
        
        driver.set_page_load_timeout(60)
        driver.get(url)
        
        # Load Cookies
        try:
             cookie_locations = [
                 os.path.join(os.path.dirname(os.path.abspath(__file__)), "tradingview_cookies.json"),
                 os.path.join(os.environ.get("LOCALAPPDATA", ""), "XiDeAI", "tradingview_cookies.json"), 
                 os.path.join(output_dir, "..", "..", "tradingview_cookies.json"), 
                 "d:\\MEGA\\IdealSmartNotifier\\tradingview_cookies.json", 
                 "tradingview_cookies.json"
             ]
             
             cookie_file = next((f for f in cookie_locations if os.path.exists(f)), None)
             
             if cookie_file:
                 with open(cookie_file, 'r', encoding='utf-8') as f:
                     cookies = json.load(f)
                 for cookie in cookies:
                     cookie_dict = {
                         'name': cookie.get('name'),
                         'value': cookie.get('value'),
                         'domain': cookie.get('domain', '.tradingview.com'),
                         'path': cookie.get('path', '/')
                     }
                     try: driver.add_cookie(cookie_dict)
                     except: pass
                 driver.refresh()
        except Exception as e:
            print(f"Warning: Cookie load failed - {e}")

        # Sayfanın yüklenmesini bekle
        time.sleep(8) # Reduced from 12 to 8
        
        # SAFE UI HIDING (CSS Only - No DOM Removal)
        try:
            hide_script = """
            // CSS injection: Hide toolbars by collapsing them (not removing)
            var css = `
                /* Left toolbar - collapse to 0 width */
                .layout__area--left {
                    width: 0 !important;
                    min-width: 0 !important;
                    overflow: hidden !important;
                    opacity: 0 !important;
                }
                
                /* Top header - collapse height */
                .layout__area--top,
                [class*="header-chart"],
                [class*="chart-header"],
                .tv-header,
                .chart-widget__header,
                [data-role="header"] {
                    height: 0 !important;
                    min-height: 0 !important;
                    overflow: hidden !important;
                    opacity: 0 !important;
                    visibility: hidden !important;
                }
                
                /* Bottom bar - collapse */
                .layout__area--bottom,
                [class*="bottom-toolbar"],
                .chart-controls-bar {
                    height: 0 !important;
                    min-height: 0 !important;
                    overflow: hidden !important;
                    opacity: 0 !important;
                }
                
                /* Other floating elements and popups */
                .tv-floating-toolbar,
                .legend,
                [class*="watermark"],
                [class*="drawing-toolbar"],
                .tv-main-panel__toolbar,
                .chart-toolbar,
                [class*="dialog"],
                [class*="popup"],
                [class*="overlap"],
                [class*="toast"],
                [class*="modal"],
                [class*="onboarding"],
                [class*="notification"],
                [class*="promo"],
                [class*="marketing"],
                [class*="deal"],
                .tv-dialog,
                .tv-overlap,
                #overlap-manager-root,
                [data-role="toast-container"] {
                    display: none !important;
                    visibility: hidden !important;
                    opacity: 0 !important;
                    z-index: -1 !important;
                    pointer-events: none !important;
                }
                
                /* Aggressive fixed/absolute overlay nuke (v3.7.2) */
                body > div:not(.layout-with-border-radius):not(.tv-main-panel):not(#overlap-manager-root) {
                    /* Only apply if it looks like a modal (high z-index) */
                }
            `;
            var style = document.createElement('style');
            style.id = 'xideai-hide-ui';
            style.textContent = css;
            document.head.appendChild(style);
            
            // JAVASCRIPT MODAL KILLER (v3.7.2)
            // Try to find and click close buttons on common TV popups
            var closeSelectors = [
                '[data-name="close"]', 
                '[class*="close"]', 
                '[class*="button"] svg', 
                'button[aria-label*="close"]',
                '.tv-dialog__close'
            ];
            
            // Find all fixed elements with high z-index and hide them
            var allElements = document.querySelectorAll('body > div');
            allElements.forEach(function(el) {
                var style = window.getComputedStyle(el);
                if ((style.position === 'fixed' || style.position === 'absolute') && parseInt(style.zIndex) > 100) {
                    // It's a modal or ad
                    console.log('XiDeAI: Hiding high-z element:', el.className);
                    el.style.display = 'none';
                }
            });
            
            console.log('XiDeAI: UI hidden via CSS & JS Cleanup');
            """
            driver.execute_script(hide_script)
            time.sleep(2)
            print("[CSS] UI elementleri CSS ile gizlendi")
        except Exception as e:
            print(f"Warning: CSS injection failed: {e}")

        # Close cookies popup
        try:
            cookie_btn = driver.find_element(By.CSS_SELECTOR, "[data-role='accept-all']")
            cookie_btn.click()
            time.sleep(1)
        except: pass

        chart_ready = wait_for_chart(driver, timeout=35)

        # SKIP MANUAL SYMBOL TYPING (URL param should work if toolbar is present initially)
        
        # SIMPLE SNAPSHOT MODE (Matching Pro_ script style)
        # Replaced all complex Zoom/Pan/Reset logic with a simple wait
        print("[MODE] Simple Snapshot (Zoom/Pan disabled)")
        time.sleep(5) # Wait for chart to settle


        # SCREENSHOT (UPDATED: Always Full Page)
        timestamp = time.strftime("%Y%m%d_%H%M%S")
        safe_symbol = symbol.replace(":", "_").replace("/", "_").replace("\\", "_").replace("*", "_").replace("?", "_").replace("\"", "_").replace("<", "_").replace(">", "_").replace("|", "_")
        filename = f"{safe_symbol}_{period}_{timestamp}.png"
        filepath = os.path.join(output_dir, filename)
        
        # Use full page screenshot to ensure axis readability
        driver.save_screenshot(filepath)
        print(f"Screenshot saved: {filepath}")
        
        if os.path.exists(filepath) and os.path.getsize(filepath) > 0:
            # WATERMARK Logic
            if PILLOW_AVAILABLE:
                try:
                    img = Image.open(filepath)
                    width, height = img.size
                    draw = ImageDraw.Draw(img)
                    
                    text = f"{tv_symbol} - {period_map.get(period, period)}"
                    font_size = int(height * 0.025) 
                    if font_size < 15: font_size = 15
                    
                    try:
                        font = ImageFont.truetype("arial.ttf", font_size)
                    except:
                        try:
                            font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", font_size)
                        except:
                            font = ImageFont.load_default()
                    
                    text_margin = 20
                    try:
                        left, top, right, bottom = draw.textbbox((0, 0), text, font=font)
                        text_w = right - left
                        text_h = bottom - top
                    except AttributeError:
                        text_w, text_h = draw.textsize(text, font=font)

                    pos_x = width - text_w - text_margin
                    pos_y = height - text_h - text_margin
                    
                    draw.text((pos_x, pos_y), text, fill=(180, 180, 180), font=font) 
                    img.save(filepath)
                    print(f"DEBUG: Watermark added to {filepath}")
                except Exception as pil_err:
                    print(f"Warning: Could not add watermark - {pil_err}")

            # SAVE PIVOT DATA (Ensure background thread is done)
            if ohlc_thread.is_alive():
                print("[OHLC] Waiting for background fetch to complete...")
                ohlc_thread.join(timeout=10) # Wait max 10 more seconds
            
            ohlc_data = ohlc_container["data"]
            pivots_data = None
            if ohlc_data:
                pivots_data = calculate_pivots(ohlc_data)
                
                # Save pivots to JSON file for C# to read
                if pivots_data:
                    pivots_filename = os.path.join(output_dir, f"{symbol}_pivots_{datetime.now().strftime('%Y%m%d')}.json")
                    try:
                        data_to_save = {
                            'symbol': symbol,
                            'timestamp': datetime.now().isoformat(),
                            'ohlc': ohlc_data,
                            'pivots': pivots_data
                        }
                        with open(pivots_filename, 'w', encoding='utf-8') as f:
                            json.dump(data_to_save, f, indent=2, ensure_ascii=False)
                        print(f"[PIVOTS] Saved to {pivots_filename}")
                    except Exception as e:
                        print(f"Warning: Could not save pivots JSON - {e}")

            print(f"SUCCESS:{filepath}")
            return filepath
        else:
             print(f"ERROR:Screenshot file creation failed at {filepath}")
             return None

    except Exception as e:
        print(f"ERROR:{str(e)}", file=sys.stderr)
        print(f"ERROR:{str(e)}")
        return None
    finally:
        if driver:
            try:
                driver.quit()
            except:
                pass


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Kullanım: python screenshot.py SEMBOL [PERIYOT] [OUTPUT_DIR] [CHART_ID] [CHROMEDRIVER_PATH]")
        sys.exit(1)
    
    symbol = normalize_symbol(sys.argv[1])
    period = sys.argv[2] if len(sys.argv) > 2 else "60"
    output_dir = sys.argv[3] if len(sys.argv) > 3 else "screenshots"
    chart_id = sys.argv[4] if len(sys.argv) > 4 else "GDHgGCEv"
    chromedriver_path = sys.argv[5] if len(sys.argv) > 5 else None
    
    # Robustness: If C# passes a path that doesn't exist, ignore it and let webdriver-manager handle it
    if chromedriver_path and not os.path.exists(chromedriver_path):
        print(f"Warning: Passed chromedriver path '{chromedriver_path}' does not exist. Switching to auto-manage mode.")
        chromedriver_path = None

    take_screenshot(symbol, period, output_dir, chart_id, chromedriver_path)
