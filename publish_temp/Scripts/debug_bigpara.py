
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
import time

def test_bigpara():
    options = Options()
    options.add_argument("--headless=new")
    driver = webdriver.Chrome(options=options)
    try:
        url = "https://bigpara.hurriyet.com.tr/borsa/en-cok-artan-hisseler/"
        print(f"Visiting {url}...")
        driver.get(url)
        time.sleep(5)
        
        # Look for the container of the list
        # Based on typical BigPara I suspect a .content or .table class
        # Let's try to find links containing 'hisse-fiyatlari' which are the stock details
        links = driver.find_elements(By.CSS_SELECTOR, "a[href*='/borsa/hisse-fiyatlari/']")
        print(f"Found {len(links)} stock detail links.")
        
        for i, link in enumerate(links[:3]):
             print(f"\n--- Link {i} ---")
             print(f"Text: {link.text}")
             print(f"Href: {link.get_attribute('href')}")
             
             # Check parent - mostly list item
             parent = link.find_element(By.XPATH, "./..")
             grandparent = parent.find_element(By.XPATH, "./..")
             
             print(f"Parent Tag: {parent.tag_name}, Class: {parent.get_attribute('class')}")
             print(f"Grandparent Tag: {grandparent.tag_name}, Class: {grandparent.get_attribute('class')}")
             
             # Try to get the whole row text
             print(f"Grandparent Text (Row?): {grandparent.text.replace('\n', '|')}")

    except Exception as e:
        print(f"Error: {e}")
    finally:
        driver.quit()

if __name__ == "__main__":
    test_bigpara()
