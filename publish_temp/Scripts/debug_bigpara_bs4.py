
import requests
from bs4 import BeautifulSoup

def test_bigpara_bs4():
    url = "https://bigpara.hurriyet.com.tr/borsa/en-cok-artan-hisseler/"
    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    }
    
    try:
        print(f"Fetching {url}...")
        resp = requests.get(url, headers=headers, timeout=10)
        print(f"Status: {resp.status_code}")
        
        soup = BeautifulSoup(resp.content, "html.parser")
        
        # BigPara usually puts these lists in a table or specific div
        # Let's look for 'en cok artan' container or table rows
        # Common classes in BigPara: .live-stock-item, .table, etc.
        
        # Filter for precise stock detail links
        links = soup.select("a[href^='/borsa/hisse-fiyatlari/'][href*='-detay']")
        # Filter out 'hisselerim-detay' which is a menu
        links = [l for l in links if 'hisselerim-detay' not in l.get('href', '')]
        print(f"Found {len(links)} specific stock links")
        
        for i, link in enumerate(links[:5]):
            print(f"--- Link {i} ---")
            print(f"Text: {link.get_text(strip=True)}")
            print(f"Href: {link.get('href')}")
            
            # Find parent row
            row = link.find_parent("ul") or link.find_parent("tr") or link.find_parent("div", class_="row")
            if row:
                print(f"Parent Tag: {row.name}")
                print(f"Parent Class: {row.get('class')}")
                # Print children text
                print(f"Row content: {row.get_text(strip=True, separator='|')}")

    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    test_bigpara_bs4()
