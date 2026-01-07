"""
TradingView Screenshot Script with OHLC Data Extraction
Kullanım: python screenshot.py SYMBOL PERIOD OUTPUT_DIR CHART_ID [CHROMEDRIVER_PATH]
Uses webdriver-manager for automatic ChromeDriver download
Fetches OHLC data from yfinance for pivot calculation
"""
import sys
import os
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
import time
import json
from datetime import datetime, timedelta

# Try to use webdriver-manager for automatic driver management
try:
    from webdriver_manager.chrome import ChromeDriverManager
    USE_WDM = True
except ImportError:
    USE_WDM = False

# Try to use yfinance for OHLC data
try:
    import yfinance as yf
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


def get_ohlc_data(symbol, days_back=1):
    """
    Get OHLC data from yfinance for pivot calculation
    Returns: dict with High, Low, Close, Open from previous trading day
    """
    if not USE_YFINANCE:
        print("Warning: yfinance not available, skipping OHLC fetch")
        return None
    
    try:
        # Convert symbol format for yfinance
        # Remove exchange prefix (BIST:, BINANCE:, etc.) if present
        ticker = symbol
        if ':' in ticker:
            ticker = ticker.split(':')[1]  # BIST:THYAO -> THYAO
        
        # Convert to yfinance format
        # Turkish stocks: THYAO -> THYAO.IS
        # Crypto: Keep as-is (BTCUSDT)
        # Forex: Keep as-is (EURUSD=X)
        # Index: XU100 -> XU100.IS
        if not any(x in ticker for x in ['.', '^', '=', 'USDT']):
            # Assume Turkish stock or index (BIST)
            ticker = f"{ticker}.IS"
        
        # Fetch last 10 days to be sure we get the previous trading day
        end_date = datetime.now()
        start_date = end_date - timedelta(days=10)
        
        data = yf.download(ticker, start=start_date, end=end_date, progress=False)
        
        if data.empty or len(data) < 2:
            print(f"Warning: No OHLC data found for {ticker}")
            return None
        
        # Get second-to-last row (previous trading day)
        prev_day = data.iloc[-2]
        
        ohlc_data = {
            'date': str(data.index[-2].date()),
            'open': float(prev_day['Open'].iloc[0]) if hasattr(prev_day['Open'], 'iloc') else float(prev_day['Open']),
            'high': float(prev_day['High'].iloc[0]) if hasattr(prev_day['High'], 'iloc') else float(prev_day['High']),
            'low': float(prev_day['Low'].iloc[0]) if hasattr(prev_day['Low'], 'iloc') else float(prev_day['Low']),
            'close': float(prev_day['Close'].iloc[0]) if hasattr(prev_day['Close'], 'iloc') else float(prev_day['Close']),
            'symbol': symbol,
            'source': 'yfinance'
        }
        
        print(f"OHLC Data ({ohlc_data['date']}): O={ohlc_data['open']:.2f} H={ohlc_data['high']:.2f} L={ohlc_data['low']:.2f} C={ohlc_data['close']:.2f}")
        return ohlc_data
        
    except Exception as e:
        print(f"Warning: Could not fetch OHLC data for {symbol} - {e}")
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
        
        pivots = {
            'pivot': round(p, 2),
            's1': round(s1, 2),
            's2': round(s2, 2),
            's3': round(s3, 2),
            'r1': round(r1, 2),
            'r2': round(r2, 2),
            'r3': round(r3, 2),
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

    # STEP 1: Fetch OHLC data and calculate pivots
    print(f"\n[OHLC] Fetching OHLC data for {symbol}...")
    ohlc_data = get_ohlc_data(symbol)
    pivots_data = None
    
    if ohlc_data:
        pivots_data = calculate_pivots(ohlc_data)
        print(f"[DEBUG] pivots_data type: {type(pivots_data)}, value: {pivots_data is not None}")
        
        # Save pivots to JSON file for C# to read
        if pivots_data:
            pivots_filename = os.path.join(output_dir, f"{symbol}_pivots_{datetime.now().strftime('%Y%m%d')}.json")
            print(f"[DEBUG] Attempting to save pivots to: {pivots_filename}")
            try:
                # Ensure directory exists
                os.makedirs(output_dir, exist_ok=True)
                
                data_to_save = {
                    'symbol': symbol,
                    'timestamp': datetime.now().isoformat(),
                    'ohlc': ohlc_data,
                    'pivots': pivots_data
                }
                print(f"[DEBUG] Data prepared: {list(data_to_save.keys())}")
                
                with open(pivots_filename, 'w', encoding='utf-8') as f:
                    json.dump(data_to_save, f, indent=2, ensure_ascii=False)
                print(f"[PIVOTS] Saved to {pivots_filename}")
            except Exception as e:
                import traceback
                print(f"Warning: Could not save pivots JSON to {pivots_filename}")
                print(f"Exception: {e}")
                traceback.print_exc()

    # Chrome options
    chrome_options = Options()
    chrome_options.add_argument("--headless=new")
    chrome_options.add_argument("--window-size=1920,1080") # Optimized for server RAM (1080p)
    chrome_options.add_argument("--disable-gpu")
    chrome_options.add_argument("--no-sandbox")
    chrome_options.add_argument("--disable-dev-shm-usage")
    chrome_options.add_argument("--disable-blink-features=AutomationControlled")
    chrome_options.add_argument("--force-device-scale-factor=2.0") # Increased scaling for crystal clear text labels
    chrome_options.add_experimental_option("excludeSwitches", ["enable-automation"])

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
    # Preserving '/' for relative charts (e.g. THYAO/XU100)
    tv_symbol = symbol.upper()
    
    if ":" in tv_symbol:
        # User already provided a specific exchange/prefix (e.g., BINANCE:BTCUSDT)
        pass
    elif "USDT" in tv_symbol:
        tv_symbol = f"BINANCE:{tv_symbol}"
    elif any(fx in tv_symbol for fx in ["XAUUSD", "XAGUSD", "EURUSD", "GBPUSD", "USDJPY", "USDTRY"]):
        tv_symbol = f"FX:{tv_symbol}"
    elif tv_symbol.endswith("1!"):
        # Futures (e.g. THYAO1!, XU0301!) are usually BIST unless specified
        tv_symbol = f"BIST:{tv_symbol}"
    elif len(tv_symbol) >= 3 and len(tv_symbol) <= 6 and tv_symbol.isalpha():
        # Highly likely a BIST stock (SASA, THYAO, etc.). 
        # Adding BIST: helps avoid ambiguity but we could also leave it blank.
        # Let's keep BIST: for standard short stocks to stay safe on tr.tradingview.com
        tv_symbol = f"BIST:{tv_symbol}"
    else:
        # Fallback to NO PREFIX - This triggers the "All" (Tümü) search behavior on TV
        # TV will automatically pick the most popular match for things like XU100, GOLD, etc.
        pass
    
    url = f"https://tr.tradingview.com/chart/{chart_id}/?symbol={tv_symbol}&interval={tv_period}"

    driver = None
    try:
        # Priority 1: Use provided chromedriver path
        if chromedriver_path and os.path.exists(chromedriver_path):
            service = Service(chromedriver_path)
            driver = webdriver.Chrome(service=service, options=chrome_options)
        # Priority 2: Use webdriver-manager if available
        elif USE_WDM:
            service = Service(ChromeDriverManager().install())
            driver = webdriver.Chrome(service=service, options=chrome_options)
        else:
            # Priority 3: Fallback to system ChromeDriver
            driver = webdriver.Chrome(options=chrome_options)
        
        driver.get(url)
        
        # Load TradingView Cookies if available (to skip popups, see saved chart settings)
        try:
             # Look for tradingview_cookies.json in the same directory as the script or User Data
             # We assume MainForm copies it to local app data or scripts folder
             
             # Check potential locations
             cookie_locations = [
                 os.path.join(os.path.dirname(os.path.abspath(__file__)), "tradingview_cookies.json"),
                 os.path.join(os.environ.get("LOCALAPPDATA", ""), "XiDeAI", "tradingview_cookies.json"), # Add explicit AppData path
                 os.path.join(output_dir, "..", "..", "tradingview_cookies.json"), 
                 "d:\\MEGA\\IdealSmartNotifier\\tradingview_cookies.json", # Hardcode dev path
                 "tradingview_cookies.json"
             ]
             
             cookie_file = next((f for f in cookie_locations if os.path.exists(f)), None)
             
             if cookie_file:
                 with open(cookie_file, 'r', encoding='utf-8') as f:
                     cookies = json.load(f)
                     
                 # Determine domain from URL (usually .tradingview.com)
                 for cookie in cookies:
                     # Selenium requires domain to match somewhat, or at least be valid
                     # If cookie has no domain, or different domain, it might fail.
                     # We try to set relevant fields.
                     cookie_dict = {
                         'name': cookie.get('name'),
                         'value': cookie.get('value'),
                         'domain': cookie.get('domain', '.tradingview.com'),
                         'path': cookie.get('path', '/')
                     }
                     try:
                         driver.add_cookie(cookie_dict)
                     except:
                         pass
                 
                 # Refresh to apply cookies
                 driver.refresh()
        except Exception as e:
            print(f"Warning: Cookie load failed - {e}")

        # Sayfanın yüklenmesini bekle (Increased)
        time.sleep(10) 
        
        # Cookie popup'ı kapat (varsa) - if cookies didn't work
        try:
            cookie_btn = driver.find_element(By.CSS_SELECTOR, "[data-role='accept-all']")
            cookie_btn.click()
            time.sleep(1)
        except:
            pass

        chart_ready = wait_for_chart(driver, timeout=35)

        # Fare imlecini SON BAR'a taşı (End tuşu ile)
        # Bu sayede sol üstteki OHLC değerleri SON kapanışı gösterecek
        try:
            from selenium.webdriver.common.keys import Keys
            from selenium.webdriver.common.action_chains import ActionChains
            
            # Chart alanını bul
            chart_area = driver.find_element(By.CSS_SELECTOR, ".chart-gui-wrapper")
            
            # Chart'a tıkla ve End tuşuna bas (son bar'a git)
            actions = ActionChains(driver)
            actions.move_to_element(chart_area)
            actions.click()
            actions.send_keys(Keys.END)  # Son bar'a git
            actions.perform()
            
            time.sleep(1)  # OHLC değerlerinin güncellenmesini bekle
            
            # Fare imlecini chart'ın sağ tarafına (son bar üzerine) taşı
            chart_width = chart_area.size['width']
            chart_height = chart_area.size['height']
            # Sağdan %5 içeri, ortadan biraz yukarı
            actions = ActionChains(driver)
            actions.move_to_element_with_offset(chart_area, int(chart_width * 0.45), int(chart_height * 0.3))
            actions.perform()
            
            time.sleep(0.5)  # OHLC güncellenmesi için bekle
            print("[MOUSE] Fare imleci son bar üzerine taşındı")
        except Exception as mouse_err:
            print(f"Warning: Fare imleci taşınamadı - {mouse_err}")

        # Ekran görüntüsü al
        timestamp = time.strftime("%Y%m%d_%H%M%S")
        
        # Windows-safe filename: Replace invalid chars with _
        safe_symbol = symbol.replace(":", "_").replace("/", "_").replace("\\", "_").replace("*", "_").replace("?", "_").replace("\"", "_").replace("<", "_").replace(">", "_").replace("|", "_")
        filename = f"{safe_symbol}_{period}_{timestamp}.png"
        filepath = os.path.join(output_dir, filename)
        
        if chart_ready:
            try:
                chart_elem = driver.find_element(By.CSS_SELECTOR, ".chart-gui-wrapper")
                chart_elem.screenshot(filepath)
            except Exception as e:
                print(f"Warning: Chart element screenshot failed, falling back to full page - {e}")
                driver.save_screenshot(filepath)
        else:
            driver.save_screenshot(filepath)
        
        if os.path.exists(filepath) and os.path.getsize(filepath) > 0:
            # WATERMARK: Add symbol and period to the image using Pillow
            if PILLOW_AVAILABLE:
                try:
                    img = Image.open(filepath)
                    draw = ImageDraw.Draw(img)
                    
                    # Big bold text for 4K resolution
                    text = f"{tv_symbol} - {period_map.get(period, period)}"
                    
                    # Try to use a common font, fallback to default
                    try:
                        # Smaller font for 1080p
                        font = ImageFont.truetype("arial.ttf", 40)
                    except:
                        font = ImageFont.load_default()
                    
                    # Position: Top left with slight offset
                    draw.text((50, 50), text, fill=(255, 215, 0), font=font) # Gold color
                    
                    img.save(filepath)
                    print(f"DEBUG: Watermark added to {filepath}")
                except Exception as pil_err:
                    print(f"Warning: Could not add watermark - {pil_err}")

            # 🎯 SAVE PIVOT DATA TO JSON FILE
            try:
                ohlc_data = get_ohlc_data(symbol)
                if ohlc_data:
                    pivots = calculate_pivots(ohlc_data)
                    if pivots:
                        # Create pivot JSON file with same naming convention
                        # Use ORIGINAL symbol for filename (without TV formatting)
                        original_symbol = sys.argv[1].upper() if len(sys.argv) > 1 else symbol
                        today_str = datetime.now().strftime("%Y%m%d")
                        pivot_filename = f"{original_symbol}_pivots_{today_str}.json"
                        pivot_filepath = os.path.join(output_dir, pivot_filename)
                        
                        # Combine OHLC and pivots into single JSON
                        pivot_json = {
                            "symbol": original_symbol,
                            "timestamp": datetime.now().isoformat(),
                            "ohlc": ohlc_data,
                            "pivots": pivots
                        }
                        
                        # Ensure output directory exists
                        os.makedirs(output_dir, exist_ok=True)
                        
                        with open(pivot_filepath, 'w', encoding='utf-8') as pf:
                            json.dump(pivot_json, pf, indent=2, ensure_ascii=False)
                        
                        print(f"✅ Pivot data saved: {pivot_filepath}")
                        print(f"   Calculated from: {pivots['calculated_from_date']}")
                        print(f"   Valid for: {pivots['valid_for_date']}")
            except Exception as pivot_err:
                print(f"Warning: Could not save pivot data - {pivot_err}")

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
    
    symbol = sys.argv[1].upper()
    period = sys.argv[2] if len(sys.argv) > 2 else "60"
    output_dir = sys.argv[3] if len(sys.argv) > 3 else "screenshots"
    chart_id = sys.argv[4] if len(sys.argv) > 4 else "GDHgGCEv"
    chromedriver_path = sys.argv[5] if len(sys.argv) > 5 else None
    
    take_screenshot(symbol, period, output_dir, chart_id, chromedriver_path)

