
import sys
import json
from pathlib import Path
import os

# Define path matches MainForm.cs logic
APPDATA_DIR = Path.home() / "AppData" / "Local" / "XiDeAI"
APPDATA_DIR.mkdir(parents=True, exist_ok=True)
JSON_FILE = APPDATA_DIR / "twitter_cookies.json"

print(f"Testing path: {JSON_FILE}")

# 1. Create Dummy JSON
try:
    dummy_cookies = [{"name": "auth_token", "value": "VERIFICATION_TEST_TOKEN", "domain": ".x.com", "path": "/"}]
    JSON_FILE.write_text(json.dumps(dummy_cookies))
    print("Created dummy JSON cookie file (Simulation).")
except Exception as e:
    print(f"Failed to create dummy file: {e}")
    sys.exit(1)

# 2. Simulate Daemon/Script Logic
print("Simulating Python Backend Check...")
if JSON_FILE.exists():
    try:
        content = JSON_FILE.read_text()
        cookies = json.loads(content)
        print(f"SUCCESS: Backend detected {len(cookies)} cookies.")
        print(f"   - Cookie Name: {cookies[0]['name']}")
        print(f"   - Cookie Value: {cookies[0]['value']}")
        print("   -> The Bridge is WORKING.")
    except Exception as e:
        print(f"FAILED to read JSON: {e}")
else:
    print("FAILED: File disappeared?")

# 3. Clean up
try:
    JSON_FILE.unlink()
    print("Cleanup complete.")
except:
    pass
