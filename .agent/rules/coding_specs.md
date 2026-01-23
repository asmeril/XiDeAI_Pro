# XiDeAI Pro: Coding Specifications

## 1. C# (.NET 8 Windows Forms)
- **UI Framework**: Vanilla Windows Forms. Avoid third-party UI libraries unless specified.
- **Async/Await**: Always use `Task.Run` for background operations to keep the UI responsive.
- **Form Controls**: Use `pnlContent.Controls.Clear()` and `pnlContent.Controls.Add(newControl)` for tab-like navigation.
- **Design**: Maintain the modern/dark theme defined in `MainForm.cs` (use standard `Color` constants).

## 2. Python (Scripts)
- **Library**: Use `selenium` for web automation and `yfinance` for technical data.
- **Browser**: Use Chrome in headless mode unless debugging is required.
- **Data Exchange**: Scripts should output results in JSON format or save to `screenshots/` and `.json` files in the app data directory.
- **Error Logs**: Print errors to `stderr` or use a dedicated `Log` output that the C# wrapper can capture.

## 3. Database & Memory
- Use `System.Text.Json` for all serialization.
- Store persistent data in the `%LOCALAPPDATA%\XiDeAI\` directory.
