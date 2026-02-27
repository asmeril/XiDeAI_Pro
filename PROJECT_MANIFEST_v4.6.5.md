# XiDeAI Pro v4.6.4 Release Manifest

## 📅 Release Date
27.02.2026

## 🎯 Focus
**Critical Fix: Influencer Search JSON Parsing & Threading**

## ⚠️ Critical Changes
- **JSON Pollution Fix:** Fixed an issue where `social_intel.py` printed "Log error" to `stdout` due to a hardcoded path, causing JSON parsing failures in C#.
- **Portable Logging:** Debug logs in the Python layer now use `APPDATA_DIR` and redirect unexpected errors to `stderr`.
- **Threading & Sync Fixes:** Retained all fixes from v4.6.4 (Market Close threads, Session sync).

## 🛠️ Changes Breakdown

### 1. Automation Scripts (Python)
- **social_intel.py:** Fixed `log_debug` to use a portable path and `file=sys.stderr`.
- **social_intel.py:** Sanitized all non-JSON `print` statements to use `stderr`.

### 2. MainForm & OperationEngine
- Corrected threading logic for market reports.

## 📝 Release Notes
> **v4.6.5 Hotfix**
> - **SOLVED:** "'L' is an invalid start of a value" error during Influencer search.
> - **IMPROVED:** Python script output reliability and JSON extraction.
> - **FIXED:** Remaining thread and session sync issues.

## 🧪 Verification
1. Run Market Close Summary.
2. Verify that it appears as a single cohesive thread on X.
3. Check daemon logs for "Processing thread part X/Y" messages.





















