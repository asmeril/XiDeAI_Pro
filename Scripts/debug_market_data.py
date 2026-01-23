
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
import time

def setup_driver():
    options = Options()
    options.add_argument("--headless=new")
    options.add_argument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
    driver = webdriver.Chrome(options=options)
    return driver

def test_google_finance():
    driver = setup_driver()
    try:
        url = "https://www.google.com/finance/quote/XU100:INDEXBIST?hl=tr"
        print(f"Visiting {url}...")
        driver.get(url)
        time.sleep(5)
        
        rows = driver.find_elements(By.CSS_SELECTOR, "div[data-symbol], tr[data-symbol]")
        print(f"Found {len(rows)} rows with [data-symbol]")
        
        for i, row in enumerate(rows[:5]):
            sym = row.get_attribute("data-symbol")
            print(f"Row {i}: {sym} - Text: {row.text.replace('\n', ' | ')}")

    except Exception as e:
        print(f"Error: {e}")
    finally:
        driver.quit()

if __name__ == "__main__":
    test_google_finance()
