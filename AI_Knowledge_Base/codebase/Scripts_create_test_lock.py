import os
import json
import sys
from datetime import datetime, timezone

LOCK_FILE = r"C:\XHive\locks\x_session.lock"

def create_stale():
    try:
        # Force remove first
        if os.path.exists(LOCK_FILE):
            try:
                os.remove(LOCK_FILE)
            except OSError as e:
                print(f"Delete failed: {e}")
                
        # Create stale
        info = {
            "pid": 999999,
            "process_name": "fake",
            "created_at_utc": "2020-01-01T00:00:00.000000+00:00"
        }
        with open(LOCK_FILE, 'w') as f:
            json.dump(info, f)
        print("Stale lock created.")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    create_stale()
