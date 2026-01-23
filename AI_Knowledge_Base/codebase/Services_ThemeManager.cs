using System.Drawing;
using System.Windows.Forms;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Theme definitions and application logic for XiDeAI Pro.
    /// </summary>
    public static class ThemeManager
    {
        // Dark Theme Colors
        public static class Dark
        {
            public static Color Background = Color.FromArgb(30, 30, 30);
            public static Color SidebarBackground = Color.FromArgb(40, 44, 52);
            public static Color CardBackground = Color.FromArgb(45, 45, 50);
            public static Color TopBarBackground = Color.FromArgb(45, 45, 48);
            public static Color TextPrimary = Color.White;
            public static Color TextSecondary = Color.Silver;
            public static Color TextAccent = Color.Cyan;
            public static Color LogText = Color.Lime;
            public static Color LogBackground = Color.FromArgb(25, 25, 25);
            public static Color ButtonHover = Color.FromArgb(60, 63, 70);
            public static Color ButtonActive = Color.FromArgb(50, 54, 62);
        }

        // Light Theme Colors (Polished for comfort)
        public static class Light
        {
            public static Color Background = Color.FromArgb(232, 235, 238); // Darker, softer light background
            public static Color SidebarBackground = Color.FromArgb(218, 222, 227);
            public static Color CardBackground = Color.FromArgb(245, 245, 248);
            public static Color TopBarBackground = Color.FromArgb(210, 215, 220);
            public static Color TextPrimary = Color.FromArgb(40, 44, 52); // Darker text for readability
            public static Color TextSecondary = Color.FromArgb(100, 105, 115);
            public static Color TextAccent = Color.FromArgb(0, 102, 204);
            public static Color LogText = Color.FromArgb(0, 90, 0);
            public static Color LogBackground = Color.FromArgb(248, 248, 250);
            public static Color ButtonHover = Color.FromArgb(195, 205, 215);
            public static Color ButtonActive = Color.FromArgb(175, 185, 195);
        }

        // Current Theme Reference (for convenience)
        public static bool IsDark => ConfigManager.Current.IsDarkTheme;

        public static Color GetBackground() => IsDark ? Dark.Background : Light.Background;
        public static Color GetSidebarBackground() => IsDark ? Dark.SidebarBackground : Light.SidebarBackground;
        public static Color GetCardBackground() => IsDark ? Dark.CardBackground : Light.CardBackground;
        public static Color GetTopBarBackground() => IsDark ? Dark.TopBarBackground : Light.TopBarBackground;
        public static Color GetTextPrimary() => IsDark ? Dark.TextPrimary : Light.TextPrimary;
        public static Color GetTextSecondary() => IsDark ? Dark.TextSecondary : Light.TextSecondary;
        public static Color GetTextAccent() => IsDark ? Dark.TextAccent : Light.TextAccent;
        public static Color GetLogText() => IsDark ? Dark.LogText : Light.LogText;
        public static Color GetLogBackground() => IsDark ? Dark.LogBackground : Light.LogBackground;
        public static Color GetButtonHover() => IsDark ? Dark.ButtonHover : Light.ButtonHover;
        public static Color GetButtonActive() => IsDark ? Dark.ButtonActive : Light.ButtonActive;

        /// <summary>
        /// Recursively applies theme colors to a control and its children.
        /// </summary>
        public static void ApplyTheme(Control control)
        {
            // CRITICAL: Skip controls that manage their own aesthetics (e.g., Ultra Glow Frames)
            if (control.Tag?.ToString() == "IGNORE_THEME") return;

            // Base colors
            control.BackColor = GetBackground();
            control.ForeColor = GetTextPrimary();

            // Specific control types
            if (control is Panel panel)
            {
                // Detect purpose by name or tag
                if (panel.Name.Contains("Sidebar") || panel.Tag?.ToString() == "sidebar")
                    panel.BackColor = GetSidebarBackground();
                else if (panel.Name.Contains("Card") || panel.Tag?.ToString() == "card")
                    panel.BackColor = GetCardBackground();
                else if (panel.Name.Contains("Top") || panel.Tag?.ToString() == "topbar")
                    panel.BackColor = GetTopBarBackground();
            }
            else if (control is TextBox textBox)
            {
                textBox.BackColor = GetLogBackground();
                textBox.ForeColor = GetLogText();
            }
            else if (control is Button button)
            {
                if (button.FlatStyle == FlatStyle.Flat && button.FlatAppearance.BorderSize == 0)
                {
                    // Navigation-style button
                    button.ForeColor = GetTextSecondary();
                    if (button.Tag?.ToString() == "active")
                    {
                        button.BackColor = GetButtonActive();
                        button.ForeColor = GetTextPrimary();
                    }
                }
                else {
                    // Standard Buttons 
                    button.ForeColor = GetTextPrimary();
                }
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.ForeColor = GetTextPrimary();
            }
            else if (control is Label label)
            {
                // Accent labels (titles, etc.)
                if (label.ForeColor == Color.Cyan || label.Tag?.ToString() == "accent")
                    label.ForeColor = GetTextAccent();
                else
                    label.ForeColor = GetTextPrimary();
            }

            // Recurse
            foreach (Control child in control.Controls)
            {
                ApplyTheme(child);
            }
        }

        /// <summary>
        /// Toggles the theme and saves the preference.
        /// </summary>
        public static void ToggleTheme()
        {
            ConfigManager.Current.IsDarkTheme = !ConfigManager.Current.IsDarkTheme;
            ConfigManager.Save();
        }
    }
}


