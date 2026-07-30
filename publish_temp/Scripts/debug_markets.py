
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
import time

def test_markets():
    options = Options()
    options.add_argument("--headless=new")
    driver = webdriver.Chrome(options=options)
    try:
        url = "https://www.google.com/finance/markets/gainers"
        print(f"Visiting {url}...")
        driver.get(url)
        time.sleep(5)
        
        # Find all <a> tags with quote/ in href
        links = driver.find_elements(By.CSS_SELECTOR, "a[href*='/quote/']")
        print(f"Found {len(links)} quote links.")
        
        for i, link in enumerate(links[:3]):
            print(f"--- Link {i} ---")
            print(f"Text: {link.text}")
            print(f"Href: {link.get_attribute('href')}")
            # print(f"OuterHTML: {link.get_attribute('outerHTML')}") # Too verbose, skip
            
            # Check for parent structure
            parent = link.find_element(By.XPATH, "./..")
            # print(f"Parent Class: {parent.get_attribute('class')}")
            
            # Try to check if we can split text lines
            lines = link.text.split('\n')
            print(f"Lines: {lines}")

    except Exception as e:
        print(f"Error: {e}")
    finally:
        driver.quit()

if __name__ == "__main__":
    test_markets()
