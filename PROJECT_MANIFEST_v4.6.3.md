# XiDeAI Pro v4.4.3 Release Manifest

## 📅 Release Date
21.01.2026

## 🎯 Focus
**Critical Fix: Session Synchronization**

## ⚠️ Critical Changes
- **Cookie Sync Bridge:** Implemented a new mechanism to export active session cookies from the UI (WebView2) to a JSON file (`twitter_cookies.json`), which is then consumed by the backend automation (Selenium/Daemon). This ensures that once you log in via the app, the background bot automatically shares the same session.

## 🛠️ Changes Breakdown

### 1. MainForm (UI)
- **ExportCookiesToJson:** Automatically exports session cookies to `AppData/Local/XiDeAI/twitter_cookies.json` whenever the Twitter WebView completes navigation (e.g., after login).

### 2. Automation Scripts (Python)
- **JSON Cookie Support:** Updated `social_intel.py` and `x_daemon.py` to prioritize loading cookies from the new JSON file.
- **Fail-Safe:** Retains the old `.pkl` format as a backup but prefers the fresh JSON data supplied by the UI.

## 📝 Release Notes
> **v4.4.3 Hotfix**
> - **SOLVED:** "Selenium fail" issue where the background bot couldn't log in despite the UI being logged in.
> - **Added:** Automatic session synchronization between UI and Bot.
> - **Retained:** All fixes from v4.4.2 (Login Loop, Setup Size, Persistent Storage).

## 🧪 Verification
1. Install v4.4.3.
2. Open the app and ensure "Social Media" tab is logged in.
3. The background bot will now automatically pick up this session.



















