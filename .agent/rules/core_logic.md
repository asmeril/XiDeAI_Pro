# XiDeAI Pro: Core System Rules

These rules apply to all AI agents (Antigravity, Cursor, Claude Code) working on the XiDeAI Pro project.

## 1. Project Overview
XiDeAI Pro is an AI-powered trading assistant designed for iDeal platform. It automates technical analysis, news tracking, and social media posting.

## 2. Technical Stack
- **Language**: C# (.NET 8.0, Windows Forms)
- **Scripting**: Python 3.x (Selenium, yfinance, Pillow)
- **AI**: LM Studio (Yerel Model, Birincil — `EnableMultiModel=true` ile aktif) & Gemini/Perplexity (Yedek/Bulut)
- **Infrastructure**: Service-oriented architecture (orchestrated by OperationManager)

## 3. Communication Standards
- **UI & Logs**: Use Turkish for all user-facing strings, message boxes, and log entries.
- **Code**: Use English for variable names, class names, method names, and comments.

## 4. Coding Standards
- **Logging**: Always use `Logger.Sys(msg)` for system events and `Logger.AI(msg)` for AI-related logs. DO NOT use `Console.WriteLine` in production code.
- **Service Pattern**: Always register new services in `OperationManager.cs` and use dependency injection via the constructor.
- **Error Handling**: Use `try-catch` blocks in all service entry points and log errors gracefully.

## 5. Script Management
- Python scripts are located in the `Scripts/` directory.
- Always use the standalone `chromedriver.exe` located in the application data folder.
