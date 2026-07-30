
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
import time

def setup_driver():
    options = Options()
    options.add_argument("--headless=new")
    driver = webdriver.Chrome(options=options)
    return driver

def test_sources():
    driver = setup_driver()
    try:
        # 1. Try İş Yatırım - En Çok Kazandıranlar
        # Note: The search result gave general pages. Let's try the suspected specific one or the general one.
        # URL 1: https://www.isyatirim.com.tr/tr-tr/analiz/hisse/Sayfalar/default.aspx
        # URL 2: https://canlidoviz.com/borsa/hisseler/artanlar
        
        urls = [
            "https://www.isyatirim.com.tr/tr-tr/analiz/hisse/Sayfalar/default.aspx",
            "https://canlidoviz.com/borsa/hisseler/artanlar",
            "https://bigpara.hurriyet.com.tr/borsa/en-cok-artan-hisseler/"
        ]
        
        for url in urls:
            print(f"\n--- VISITING {url} ---")
            try:
                driver.get(url)
                time.sleep(5)
                
                # Try to finding table rows
                rows = driver.find_elements(By.TAG_NAME, "tr")
                print(f"Found {len(rows)} table rows.")
                
                for i, row in enumerate(rows[:5]):
                    print(f"Row {i}: {row.text.replace('\n', '|')}")
            except Exception as e:
                print(f"Error visiting {url}: {e}")

    except Exception as e:
        print(f"General Error: {e}")
    finally:
        driver.quit()

if __name__ == "__main__":
    test_sources()
