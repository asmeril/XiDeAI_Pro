
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

def inspect_html():
    driver = setup_driver()
    try:
        url = "https://www.google.com/finance/markets/gainers?hl=tr"
        print(f"Visiting {url}...")
        driver.get(url)
        time.sleep(5)
        
        # Target the main list more specifically
        # usually inside a <div role="main"> or similar, but let's look for a known BIST stock 'SASA' link parent
        try:
             # Find any link to a known stock to localize the list
             links = driver.find_elements(By.CSS_SELECTOR, "a[href*=':IST']")
             if links:
                 print(f"Found {len(links)} BIST links.")
                 first_link = links[0]
                 print("--- First Link HTML ---")
                 print(first_link.get_attribute('outerHTML'))
                 print("--- Parent HTML ---")
                 print(first_link.find_element(By.XPATH, "./..").get_attribute('outerHTML'))
                 print("--- Text Content ---")
                 print(f"Link Text: '{first_link.text}'")
                 print(f"Parent Text: '{first_link.find_element(By.XPATH, './..').text}'")
             else:
                 print("No BIST links found!")
                 print(driver.page_source[:2000]) # Print start of page to debug
        except Exception as ex:
             print(f"Error finding specific link: {ex}")

    except Exception as e:
        print(f"General Error: {e}")
    finally:
        driver.quit()

if __name__ == "__main__":
    inspect_html()
