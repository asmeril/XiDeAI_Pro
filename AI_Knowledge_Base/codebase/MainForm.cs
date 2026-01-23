using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using XiDeAI_Pro.Config;
using XiDeAI_Pro.Services;
using System.Drawing;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Web.WebView2.WinForms;

namespace XiDeAI_Pro
{
    public partial class MainForm : Form
    {
        private LogFileWatcher _watcher = null!;
        private WebView2 _webViewChart = null!;
        private WebView2 _webViewTwitter = null!;
        private WebView2 _webViewTwitterBg = null!; // Background tasks
        private TabControl tabDashViews = null!;
        
        // CENTRAL MANAGER
        private OperationManager _opManager = null!;

        private DateTime _lastNewsTweetTime = DateTime.MinValue;
        private HashSet<string> _tweetedToday = new HashSet<string>();
        private object _memoryLock = new object();
        
        private System.Windows.Forms.Timer _deepScanTimer = null!; // Fixed: Deep Scan timer
        private System.Windows.Forms.Timer _telegramPollTimer = null!; // Telegram Polling Timer
        private bool _isTelegramProcessing = false; // v3.0.9 Re-entrancy protection
        private System.Windows.Forms.Timer _scheduleTimer = null!; // Main Dashboard/Quota Timer
        private DateTime _lastStatsUpdate = DateTime.MinValue;
        private long _lastProcessedUpdateId = 0;
        private HashSet<string> _signalMemory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Cache for Manual Analysis (Telegram)
        private ManualAnalysisResult? _lastAnalysisResult; 
        private string _lastAnalysisSymbol = ""; 

        // Cache for News Approval (Telegram)
        private readonly ConcurrentDictionary<int, (NewsItem item, string summary)> _pendingNewsDict = new();
        private int _newsIdCounter = 0;

        private readonly ConcurrentDictionary<int, InteractionState> _pendingInteractionDict = new();
        private int _interactionIdCounter = 0;

        private HashSet<string> _knownDMs = new HashSet<string>();

        public class InteractionState
        {
            public string Url { get; set; } = "";
            public string Text { get; set; } = "";
            public DateTime Time { get; set; }
        }
        
        // Analysis debounce (avoid repeated analysis/screenshot for same symbol within short window)
        private readonly object _analysisLock = new object();
        private readonly Dictionary<string, DateTime> _lastAnalysisAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan AnalysisDebounceWindow = TimeSpan.FromSeconds(120);

        // Interaction processing handled by InteractionEngine
        
        // Delayed Queue handled by SignalEngine

        // UI Controls
        // Sidebar UI Controls
        private Panel pnlSidebar = null!;
        private Panel pnlContent = null!;
        private Panel pnlDashboard = null!, pnlSignals = null!, pnlAnalysis = null!, pnlSettings = null!, pnlBot = null!, pnlHistory = null!, pnlInfluencers = null!, pnlNews = null!, pnlGuruCenter = null!;
        private Button btnNavDash = null!, btnNavSignals = null!, btnNavAnalysis = null!, btnNavBot = null!, btnNavSettings = null!, btnNavHistory = null!, btnNavInfluencers = null!, btnNavNews = null!, btnNavGuru = null!;
        private TextBox txtLog = null!, txtTrends = null!;
        private TextBox txtTargetAccounts = null!; // New control
        private CheckBox chkAuto = null!;
        private CheckBox chkPostAsThread = null!;
        private RichTextBox rtbSignalLog = null!; // New Activity Log
        private DataGridView dgvSignals = null!; // New Signal Grid
        // KPI Labels for Signal Center 2.0
        private Label lblDailySignals = null!, lblSuccessRate = null!, lblTopStrategy = null!;

        // Settings Controls
        private TextBox txtApiKey = null!, txtApiSecret = null!, txtAccessToken = null!, txtTokenSecret = null!, txtGeminiKey = null!, txtPerplexityKey = null!, txtTvChartId = null!, txtTvSymbol = null!;
        private TextBox txtTelToken = null!, txtTelChatId = null!;
        
        // Bot Interaction Tab Controls
        private CheckBox chkBotEnabled = null!;
        private TextBox txtBotKeywords = null!;
        private TextBox txtBotMinFollowers = null!;
        private TextBox txtBotMinFavorites = null!;
        private TextBox txtBotMaxAge = null!;
        private RichTextBox rtbBotLog = null!;
        private Label lblBotStatus = null!;
        private Button btnStart = null!, btnSave = null!;
        private Button btnStopWatcher = null!, btnPauseWatcher = null!;
        private Panel pnlDashHeader = null!;
        private ComboBox cmbGeminiModel = null!;
        private FlowLayoutPanel pnlNewsCards = null!; // Published (Existing)
        private FlowLayoutPanel pnlNewsPending = null!; // New: Pending Approval
        private FlowLayoutPanel pnlNewsLive = null!; // New: Live Feed
        private Button btnNewsStart = null!, btnNewsStop = null!;
        private bool _isNewsTrackerRunning = false;
        private bool _newsInitialized = false;
        private bool _guruPanelInitialized = false;
        private List<InfluencerPost> _guruPosts = new List<InfluencerPost>();
        private DataGridView dgvGuru = null!;
        private List<(NewsItem item, string? analysis, string status)> _newsBuffer = new List<(NewsItem item, string? analysis, string status)>();

        
        // Filter Controls
        private CheckBox chkKing = null!, chkBomba = null!, chkTeFo = null!, chkDip = null!, chkZirve = null!, chkAnka = null!, chkMiner = null!;
        private CheckBox chkPer15 = null!, chkPer60 = null!, chkPer240 = null!, chkPerG = null!;
        private CheckBox chkOnlyCommon = null!;
        // Common Scan Checkboxes
        private CheckBox chkComKing = null!, chkComBomba = null!, chkComTeFo = null!, chkComDip = null!, chkComZirve = null!, chkComAnka = null!;
        private NumericUpDown numThresholdKing = null!, numThresholdDip = null!, numThresholdAnka = null!;
        private TextBox txtScanHours = null!;

        // Manual Analysis Controls
        private ComboBox cmbMarket = null!, cmbTimeframe = null!, cmbBasis = null!;
        private TextBox txtAnalysisSymbol = null!;
        private RichTextBox rtbAnalysisResult = null!;
        private PictureBox picScreenshot = null!;
        private Button btnAnalyze = null!, btnTweetAnalysis = null!;
        
        // Quota Stats UI (Removed labels in V5.1 in favor of Circular Panels)
        
        // News Controls (Dashboard)
// private Button btnNewsToggle = null!; // Removed legacy control
        private Label lblFollowers = null!;
        
        // ToolTip Control
        private ToolTip _toolTip = null!;

        // Spam Protection (Per-Module) UI
        private CheckBox chkSpamSignals = null!;
        private CheckBox chkSpamBatches = null!;
        private CheckBox chkSpamManual = null!;
        
        // Apex Ar-Ge UI (Migrated to HIVE Hub)
        private Panel pnlApexContainer = null!; 
        private DataGridView dgvApexPapers = null!;
        private DataGridView dgvApexRepos = null!;
        private RichTextBox rtbApexReport = null!;
        private CheckBox chkSpamNews = null!;
        private CheckBox chkSpamReports = null!;
        private CheckBox chkSpamMotivation = null!;
        
        // Fenerbahçe Fan Modu
        private Panel pnlFenerbahce = null!;
        private Button btnNavFenerbahce = null!;
        private DataGridView dgvFenerbahce = null!;
        private bool _fenerbahceInitialized = false;

        // HIVE Hub (Unified Architecture v3.7.2) - REMOVED
        
        // Engagement Hub (Sentinel Expansion v3.7.5) - REMOVED


        public MainForm()
        {
            InitializeComponent();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = $"X'iDeAI Pro v{version} - STABLE";
            InitializeServices();
            // HIVE Protocol initialization is now handled via InitializeHiveHub on first click
            
            // v3.0 Link Memory to Intelligence
            if (_opManager.SocialIntel != null && _opManager.Memory != null)
            {
                _opManager.SocialIntel.SetMemoryEngine(_opManager.Memory);
            }    
            InitializeDeepScanTimer();
            
            LoadSettings();
            
            // Apply saved theme
            ThemeManager.ApplyTheme(this);
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1100, 800);
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Set application icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xideai_icon.ico");
                if (File.Exists(iconPath))
                    this.Icon = new System.Drawing.Icon(iconPath);
            }
            catch { /* Use default if icon loading fails */ }
            
            // ToolTip Init
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 10000;
            _toolTip.InitialDelay = 500;
            _toolTip.ReshowDelay = 500;
            _toolTip.ShowAlways = true;

            // Sidebar Creation
                pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = Color.FromArgb(40, 44, 52), Padding = new Padding(0, 20, 0, 0) };
            pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30,30,30) };
            this.Controls.Add(pnlContent);
            this.Controls.Add(pnlSidebar);

            // Nav Buttons Helper
            Button CreateNavBtn(string text, string icon) {
                var btn = new Button { 
                    Text = "  " + icon + "  " + text, 
                    Dock = DockStyle.Top, 
                    Height = 45, // Slightly reduced for compact view
                    FlatStyle = FlatStyle.Flat, 
                    FlatAppearance = { BorderSize = 0 }, 
                    BackColor = Color.Transparent, 
                    ForeColor = Color.Silver, 
                    Font = new Font("Segoe UI", 10, FontStyle.Regular), 
                    TextAlign = ContentAlignment.MiddleLeft, 
                    Padding = new Padding(20, 0, 0, 0),
                    Cursor = Cursors.Hand
                };
                btn.MouseEnter += (s,e) => { if (btn.Tag?.ToString() != "active") btn.BackColor = Color.FromArgb(60,63,70); };
                btn.MouseLeave += (s,e) => { if (btn.Tag?.ToString() != "active") btn.BackColor = Color.Transparent; };
                return btn;
            }

            // Section Header Helper
            Label CreateSectionHeader(string text) {
                return new Label {
                    Text = text,
                    Dock = DockStyle.Top,
                    Height = 35,
                    ForeColor = Color.FromArgb(0, 212, 255), // Cyan Accent
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    TextAlign = ContentAlignment.BottomLeft,
                    Padding = new Padding(10, 0, 0, 5),
                    BackColor = Color.Transparent
                };
            }

            // Create Panels (Hidden by default)
            pnlDashboard = new Panel { Dock = DockStyle.Fill, Visible = true, BackColor = Color.FromArgb(30,30,30) };
            pnlSignals = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlAnalysis = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlSettings = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlBot = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlHistory = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlInfluencers = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlNews = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlGuruCenter = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };
            pnlFenerbahce = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(30,30,30) };

            
            pnlContent.Controls.Add(pnlDashboard);
            pnlContent.Controls.Add(pnlSignals);
            pnlContent.Controls.Add(pnlAnalysis);
            pnlContent.Controls.Add(pnlSettings);
            pnlContent.Controls.Add(pnlBot);
            pnlContent.Controls.Add(pnlHistory);

            pnlContent.Controls.Add(pnlInfluencers);
            pnlContent.Controls.Add(pnlNews);
            pnlContent.Controls.Add(pnlGuruCenter);
            pnlContent.Controls.Add(pnlFenerbahce);


            // =================================================================================
            // NEW SIDEBAR NAVIGATION (Consolidated V2.0)
            // Order of Addition: Bottom -> Top (because Dock=Top stacks upwards)
            // =================================================================================

            // 4. THEME TOGGLE (Bottom-most)
            var btnThemeToggle = new Button {
                Text = ConfigManager.Current.IsDarkTheme ? "  ☀️  Açık Tema" : "  🌙  Koyu Tema",
                Dock = DockStyle.Top,
                Height = 45,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnThemeToggle.FlatAppearance.BorderSize = 0;
            btnThemeToggle.Click += (s, e) => {
                ThemeManager.ToggleTheme();
                btnThemeToggle.Text = ConfigManager.Current.IsDarkTheme ? "  ☀️  Açık Tema" : "  🌙  Koyu Tema";
                ThemeManager.ApplyTheme(this);
            };
            pnlSidebar.Controls.Add(btnThemeToggle);

            // 3. SİSTEM (System) GROUP
            btnNavSettings = CreateNavBtn("Ayarlar", "⚙️");
            btnNavSettings.Click += (s,e) => ShowPanel(pnlSettings, btnNavSettings);
            pnlSidebar.Controls.Add(btnNavSettings);

            btnNavHistory = CreateNavBtn("Geçmiş", "📜");
            btnNavHistory.Click += (s,e) => { LoadActivityHistory(); ShowPanel(pnlHistory, btnNavHistory); };
            pnlSidebar.Controls.Add(btnNavHistory);

            btnNavBot = CreateNavBtn("İletişim Merkezi", "💬"); 
            btnNavBot.Click += (s,e) => ShowPanel(pnlBot, btnNavBot);
            pnlSidebar.Controls.Add(btnNavBot);

            pnlSidebar.Controls.Add(CreateSectionHeader("SİSTEM"));

            // 2. ZEKA (HIVE Intelligence) GROUP
            if (!ConfigManager.Current.FanZoneEnabled) { ConfigManager.Current.FanZoneEnabled = true; ConfigManager.Save(); }
            btnNavFenerbahce = CreateNavBtn("Fenerbahçe", "💙");
            btnNavFenerbahce.ForeColor = Color.Yellow;
            btnNavFenerbahce.Click += (s,e) => { InitializeFenerbahcePanel(); ShowPanel(pnlFenerbahce, btnNavFenerbahce); };
            pnlSidebar.Controls.Add(btnNavFenerbahce);




            // Haberler (Restored by User Request)
            btnNavNews = CreateNavBtn("Haberler", "📰");
            btnNavNews.Click += (s, e) => {
                 if (!_newsInitialized) InitializeNewsPanel();
                 ShowPanel(pnlNews, btnNavNews); 
            };
            pnlSidebar.Controls.Add(btnNavNews);

            // [RESURRECTED] Fenomen Veritabanı (Influencers) - v3.8.2
            btnNavInfluencers = CreateNavBtn("Fenomenler", "👥");
            btnNavInfluencers.Click += (s, e) => {
                 if (!_influencerPanelInitialized) InitializeInfluencerPanel();
                 ShowPanel(pnlInfluencers, btnNavInfluencers);
            };
            pnlSidebar.Controls.Add(btnNavInfluencers);

            pnlSidebar.Controls.Add(CreateSectionHeader("İSTİHBARAT"));

            // 1. ANALİZ (Finance) GROUP
            btnNavGuru = CreateNavBtn("Üstat Paneli", "👑");
            btnNavGuru.Click += (s,e) => { InitializeGuruPanel(); ShowPanel(pnlGuruCenter, btnNavGuru); };
            pnlSidebar.Controls.Add(btnNavGuru);

            btnNavAnalysis = CreateNavBtn("Manuel Analiz", "📈");
            btnNavAnalysis.Click += (s,e) => ShowPanel(pnlAnalysis, btnNavAnalysis);
            pnlSidebar.Controls.Add(btnNavAnalysis);

            btnNavSignals = CreateNavBtn("Sinyal Merkezi", "📡");
            btnNavSignals.Click += (s,e) => ShowPanel(pnlSignals, btnNavSignals);
            pnlSidebar.Controls.Add(btnNavSignals);

            btnNavDash = CreateNavBtn("Ana Ekran", "🏠");
            btnNavDash.Click += (s,e) => ShowPanel(pnlDashboard, btnNavDash);
            pnlSidebar.Controls.Add(btnNavDash);

            pnlSidebar.Controls.Add(CreateSectionHeader("ANALİZ (FİNANS)"));

            // Sidebar Header (Logo/Title) - Added LAST so it's at the TOP
            var pnlSideHeader = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Color.Transparent };
            var lblSideTitle = new Label { Text = "X'iDeAI\nPro", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            pnlSideHeader.Controls.Add(lblSideTitle);
            pnlSidebar.Controls.Add(pnlSideHeader);
            
            // Initial Active
            btnNavDash.Tag = "active";
            btnNavDash.BackColor = Color.FromArgb(50, 54, 62);
            btnNavDash.ForeColor = Color.White;
            
            // ========== Tab 1: Dashboard (ULTRA GLOW V5.1) ==========
            pnlDashboard.BackColor = Color.FromArgb(11, 14, 17); // Deep terminal background
            
            // Helper: 2x2 Grid Mini Stat Bloğu
            Panel CreateMiniStat(string title, Color accent) {
                var p = new Panel { Width = 95, Height = 42, BackColor = Color.Transparent, Margin = new Padding(2) };
                p.Paint += (s, ev) => {
                    var cfg = ConfigManager.Current;
                    string valStr = "0";
                    string subStr = ""; // e.g., "/16"
                    
                    if (p.Tag?.ToString() == "api_day") { valStr = cfg.DailyTweetCount.ToString(); subStr = "/" + cfg.TwitterApiDailyLimit; }
                    else if (p.Tag?.ToString() == "api_month") { valStr = cfg.MonthlyTweetCount.ToString(); subStr = "/" + cfg.TwitterApiMonthlyLimit; }
                    else if (p.Tag?.ToString() == "web_day") { valStr = (cfg.DailyTotalTweetCount - cfg.DailyTweetCount).ToString(); }
                    else if (p.Tag?.ToString() == "web_month") { valStr = (cfg.MonthlyTotalTweetCount - cfg.MonthlyTweetCount).ToString(); }

                    ev.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var brushText = ThemeManager.IsDark ? Brushes.White : Brushes.Black;
                    var brushSub = new SolidBrush(Color.FromArgb(120, 120, 130));

                    // Background card
                    using (var brushBg = new SolidBrush(Color.FromArgb(20, accent.R, accent.G, accent.B)))
                        ev.Graphics.FillRectangle(brushBg, 0, 0, p.Width, p.Height);
                    using (var pen = new Pen(Color.FromArgb(80, accent.R, accent.G, accent.B), 1))
                        ev.Graphics.DrawRectangle(pen, 0, 0, p.Width-1, p.Height-1);

                    // Fonts
                    var fVal = new Font("Segoe UI Black", 11);
                    var fSub = new Font("Segoe UI Bold", 8);
                    
                    // Measure
                    var szVal = ev.Graphics.MeasureString(valStr, fVal);
                    var szSub = !string.IsNullOrEmpty(subStr) ? ev.Graphics.MeasureString(subStr, fSub) : SizeF.Empty;
                    float totalW = szVal.Width + (szSub.Width > 0 ? szSub.Width - 4 : 0);
                    float xPos = (p.Width - totalW) / 2;

                    // Draw Value
                    ev.Graphics.DrawString(valStr, fVal, brushText, xPos, 16);
                    
                    // Draw Subtext (if exists)
                    if (!string.IsNullOrEmpty(subStr)) {
                        ev.Graphics.DrawString(subStr, fSub, brushSub, xPos + szVal.Width - 4, 21);
                    }
                };
                return p;
            }

            // 1. Command Center Header (180px - Absolute Headroom Fix)
            pnlDashHeader = new Panel { Dock = DockStyle.Top, Height = 180, BackColor = Color.Transparent, Padding = new Padding(15, 10, 15, 10) };
            var tblHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.Transparent };
            tblHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); // Grid Stats (Expanded)
            tblHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); // Ticker (Reduced)
            tblHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); // Action Buttons
            pnlDashHeader.Controls.Add(tblHeader);

            // A. Dairesel Sayaçlar -> 2x2 Grid (Logic Based)
            var flowContainer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            
            var tblGrid = new TableLayoutPanel { Width = 280, Height = 125, ColumnCount = 3, RowCount = 3, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 0) };
            tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); // Labels (GÜNLÜK, AYLIK)
            tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // API
            tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // WEB
            
            // Header Labels
            var lblAPI = new Label { Text = "X API", ForeColor = Color.FromArgb(0, 212, 255), Font = new Font("Segoe UI Black", 8), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Bottom, Height = 20 };
            var lblWEB = new Label { Text = "X WEB", ForeColor = Color.FromArgb(255, 50, 255), Font = new Font("Segoe UI Black", 8), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Bottom, Height = 20 };
            tblGrid.Controls.Add(lblAPI, 1, 0); tblGrid.Controls.Add(lblWEB, 2, 0);

            // Row 1 Title
            var lblDay = new Label { Text = "GÜNLÜK", ForeColor = Color.Gray, Font = new Font("Segoe UI Bold", 7), TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            tblGrid.Controls.Add(lblDay, 0, 1);
            
            // Row 2 Title
            var lblMonth = new Label { Text = "AYLIK", ForeColor = Color.Gray, Font = new Font("Segoe UI Bold", 7), TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            tblGrid.Controls.Add(lblMonth, 0, 2);

            var pApiDay = CreateMiniStat("", Color.FromArgb(0, 212, 255)); pApiDay.Tag = "api_day";
            var pApiMon = CreateMiniStat("", Color.FromArgb(0, 212, 255)); pApiMon.Tag = "api_month";
            var pWebDay = CreateMiniStat("", Color.FromArgb(255, 50, 255)); pWebDay.Tag = "web_day";
            var pWebMon = CreateMiniStat("", Color.FromArgb(255, 50, 255)); pWebMon.Tag = "web_month";

            tblGrid.Controls.Add(pApiDay, 1, 1); tblGrid.Controls.Add(pWebDay, 2, 1);
            tblGrid.Controls.Add(pApiMon, 1, 2); tblGrid.Controls.Add(pWebMon, 2, 2);

            // Follower Count (Styled separate)
            lblFollowers = new Label { Text = $"{ConfigManager.Current.FollowersCount}\nTAKİPÇİ", ForeColor = Color.White, Font = new Font("Segoe UI Black", 9), TextAlign = ContentAlignment.MiddleCenter, Width = 85, Height = 90, Margin = new Padding(10, 10, 0, 0), Tag = "accent" };
            
            flowContainer.Controls.AddRange(new Control[] { tblGrid, lblFollowers });
            tblHeader.Controls.Add(flowContainer, 0, 0);

            // B. Ticker (Center)
            var pnlTickerBox = new Panel { Dock = DockStyle.Fill, Margin = new Padding(15, 5, 15, 5), Padding = new Padding(0, 5, 0, 5) };
            var lblTickerTitle = new Label { Text = "ANLIK PİYASA TRENDLERİ", Dock = DockStyle.Top, Height = 20, ForeColor = Color.FromArgb(100, 100, 110), Font = new Font("Segoe UI Bold", 8), TextAlign = ContentAlignment.MiddleCenter };
            txtTrends = new TextBox { Dock = DockStyle.Fill, Text = ConfigManager.Current.DailyTrends, Font = new Font("Consolas", 10, FontStyle.Bold), BackColor = Color.FromArgb(15, 16, 20), ForeColor = Color.FromArgb(0, 212, 255), BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center, Multiline = true };
            txtTrends.TextChanged += (s, ev) => { ConfigManager.Current.DailyTrends = txtTrends.Text; };
            pnlTickerBox.Controls.Add(txtTrends); pnlTickerBox.Controls.Add(lblTickerTitle);
            tblHeader.Controls.Add(pnlTickerBox, 1, 0);

            // C. Neon Buttons (Right) - Structured Row System
            var pnlActionContainer = new Panel { Dock = DockStyle.Fill, Tag = "IGNORE_THEME" };
            
            // Row 1: System Controls
            var flowSystem = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 5, 0, 0), WrapContents = false };
            
            Button CreateNeonBtn(string text, Color neon, int w = 110) {
                var btn = new Button { Text = text, Width = w, Height = 45, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = new Font("Segoe UI Black", 8), Cursor = Cursors.Hand, BackColor = Color.FromArgb(20, 21, 25), Margin = new Padding(5) };
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = neon;
                btn.MouseEnter += (s, e) => { btn.BackColor = Color.FromArgb(neon.R/4, neon.G/4, neon.B/4); };
                btn.MouseLeave += (s, e) => { btn.BackColor = Color.FromArgb(20, 21, 25); };
                return btn;
            }

            btnStart = CreateNeonBtn("START", Color.FromArgb(0, 200, 83)); btnStart.Click += BtnStart_Click;
            btnStopWatcher = CreateNeonBtn("STOP", Color.FromArgb(211, 47, 47)); btnStopWatcher.Click += BtnStopWatcher_Click;
            btnPauseWatcher = CreateNeonBtn("PAUSE", Color.FromArgb(255, 160, 0)); btnPauseWatcher.Click += BtnPauseWatcher_Click;
            
            chkAuto = new CheckBox { Text = "OTO AI TWEET", ForeColor = Color.FromArgb(0, 229, 255), Font = new Font("Segoe UI Black", 7), AutoSize = true, Cursor = Cursors.Hand, Margin = new Padding(5, 15, 5, 0) };
            chkAuto.CheckedChanged += (s, e) => { ConfigManager.Current.AutoTweet = chkAuto.Checked; };

            flowSystem.Controls.AddRange(new Control[] { btnStopWatcher, btnPauseWatcher, btnStart, chkAuto });
            pnlActionContainer.Controls.Add(flowSystem);

            // Row 2: Navigation Controls
            var flowNav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 0, 0, 0), WrapContents = false };
            
            var btnGoChart = CreateNeonBtn("GRAFİK", Color.Cyan, 110);
            var btnGoX = CreateNeonBtn("SOSYAL", Color.DimGray, 90);
            
            btnGoX.ForeColor = Color.Silver;

            // Tab Switching Logic
            void SetActiveTab(int index) {
                tabDashViews.SelectedIndex = index;
                btnGoChart.FlatAppearance.BorderColor = index == 0 ? Color.Cyan : Color.DimGray; btnGoChart.ForeColor = index == 0 ? Color.Cyan : Color.Silver;
                btnGoX.FlatAppearance.BorderColor =     index == 1 ? Color.Cyan : Color.DimGray; btnGoX.ForeColor     = index == 1 ? Color.Cyan : Color.Silver;
            }

            btnGoChart.Click += (s, e) => SetActiveTab(0);
            btnGoX.Click += (s, e) => SetActiveTab(1);

            flowNav.Controls.AddRange(new Control[] { btnGoX, btnGoChart });
            pnlActionContainer.Controls.Add(flowNav);
            flowNav.BringToFront(); // Ensure Row 2 is below Row 1

            tblHeader.Controls.Add(pnlActionContainer, 2, 0);

            pnlDashboard.Controls.Add(pnlDashHeader);

            // V6.0 - 3 SCREEN TAB INTERFACE (RED)
            
            void SetupManualLayout(Panel container, Control header, Control content) {
                container.Controls.Add(header);
                container.Controls.Add(content);
                void DoLayout() {
                    if (container.Width == 0 || container.Height == 0) return;
                    int h = 50; 
                    header.SetBounds(0, 0, container.Width, h);
                    content.SetBounds(0, h, container.Width, container.Height - h);
                }
                container.Resize += (s, e) => DoLayout();
                DoLayout();
            }

            // Main Tab Control (Full Dashboard)
            tabDashViews = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons, ItemSize = new Size(0, 1), Padding = new Point(0,0), Margin = new Padding(0) };
            
            // --- TAB 1: CHART ---
            var tpChart = new TabPage { BackColor = Color.FromArgb(11, 14, 17), Margin = new Padding(0) };
            var pnlChartContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 14, 17), Padding = new Padding(2), Tag = "IGNORE_THEME" };
            pnlChartContainer.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = pnlChartContainer.ClientRectangle; rect.Width -= 1; rect.Height -= 1;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath()) {
                    int r = 10;
                    path.AddArc(0, 0, r, r, 180, 90); path.AddArc(rect.Width-r, 0, r, r, 270, 90);
                    path.AddArc(rect.Width-r, rect.Height-r, r, r, 0, 90); path.AddArc(0, rect.Height-r, r, r, 90, 90);
                    path.CloseAllFigures();
                    using (var pen = new Pen(Color.FromArgb(0, 212, 255), 2.0f)) e.Graphics.DrawPath(pen, path);
                }
            };

            var pnlChartHeader = new Panel { BackColor = Color.FromArgb(20, 24, 28), Tag = "IGNORE_THEME" }; 
            pnlChartHeader.Controls.Add(new Label { Text = "📊 PİYASA ANALİZ TERMİNALİ", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(0, 212, 255), Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0) });
            _webViewChart = new WebView2 { BackColor = Color.FromArgb(11, 14, 17), DefaultBackgroundColor = Color.FromArgb(11, 14, 17), Tag = "IGNORE_THEME" };
             SetupManualLayout(pnlChartContainer, pnlChartHeader, _webViewChart);
            tpChart.Controls.Add(pnlChartContainer);

            // --- TAB 2: TWITTER ---
            var tpTwitter = new TabPage { BackColor = Color.FromArgb(11, 14, 17), Margin = new Padding(0) };
            var pnlTwitterContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 14, 17), Padding = new Padding(2), Tag = "IGNORE_THEME" };
            pnlTwitterContainer.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = pnlTwitterContainer.ClientRectangle; rect.Width -= 1; rect.Height -= 1;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath()) {
                    int r = 10;
                    path.AddArc(0, 0, r, r, 180, 90); path.AddArc(rect.Width-r, 0, r, r, 270, 90);
                    path.AddArc(rect.Width-r, rect.Height-r, r, r, 0, 90); path.AddArc(0, rect.Height-r, r, r, 90, 90);
                    path.CloseAllFigures();
                    using (var pen = new Pen(Color.FromArgb(255, 160, 0), 2.0f)) e.Graphics.DrawPath(pen, path);
                }
            };

            var pnlTwitterHeader = new Panel { BackColor = Color.FromArgb(20, 24, 28), Tag = "IGNORE_THEME" };
            pnlTwitterHeader.Controls.Add(new Label { Text = "🐦 SOSYAL MEDYA AKIŞI", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 160, 0), Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0) });
            _webViewTwitter = new WebView2 { BackColor = Color.FromArgb(11, 14, 17), DefaultBackgroundColor = Color.FromArgb(11, 14, 17), Tag = "IGNORE_THEME" };
            SetupManualLayout(pnlTwitterContainer, pnlTwitterHeader, _webViewTwitter);
            tpTwitter.Controls.Add(pnlTwitterContainer);

            // txtLog initialized here but no longer added to a dashboard tab, 
            // it can be hosted in History or HIVE if needed later. 
            // For now, it stays as a field to receive log updates.
            txtLog = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.FromArgb(11, 14, 17), ForeColor = Color.FromArgb(0, 255, 100), Font = new Font("Consolas", 8), BorderStyle = BorderStyle.None, Tag="IGNORE_THEME" };

            // --- APEX AR-GE (Core Container) ---
            // This container is built here and re-used in HIVE Hub tab.
            pnlApexContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 14, 17), Padding = new Padding(10), Tag = "IGNORE_THEME" };
            
            pnlApexContainer.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = pnlApexContainer.ClientRectangle; rect.Width -= 1; rect.Height -= 1;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath()) {
                    int r = 15;
                    path.AddArc(0, 0, r, r, 180, 90); path.AddArc(rect.Width-r, 0, r, r, 270, 90);
                    path.AddArc(rect.Width-r, rect.Height-r, r, r, 0, 90); path.AddArc(0, rect.Height-r, r, r, 90, 90);
                    path.CloseAllFigures();
                    using (var pen = new Pen(Color.FromArgb(186, 104, 200), 2.0f)) e.Graphics.DrawPath(pen, path); 
                }
            };

            var pnlApexHeader = new Panel { BackColor = Color.FromArgb(20, 24, 28), Tag = "IGNORE_THEME", Height = 45, Dock = DockStyle.Top };
            var lblApexTitle = new Label { Text = "📡 HIVE INTEL (v3.7) - Global Intelligence System", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(224, 64, 251), Font = new Font("Segoe UI Black", 11), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0) };
            pnlApexHeader.Controls.Add(lblApexTitle);
            
            var btnRefreshApex = new Button { Text = "🔄 VERİLERİ TAZELE", Dock = DockStyle.Right, Width = 140, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = new Font("Segoe UI Bold", 8), BackColor = Color.FromArgb(30, 34, 40), Cursor = Cursors.Hand };
            btnRefreshApex.FlatAppearance.BorderColor = Color.FromArgb(224, 64, 251);
            btnRefreshApex.Click += (s, e) => { };

            var btnOmni = new Button { Text = "🌍 DÜNYAYI TARA", Dock = DockStyle.Right, Width = 140, FlatStyle = FlatStyle.Flat, ForeColor = Color.Cyan, Font = new Font("Segoe UI Bold", 8), BackColor = Color.FromArgb(30, 34, 40), Cursor = Cursors.Hand };
            btnOmni.FlatAppearance.BorderColor = Color.Cyan;
            btnOmni.Click += (s, e) => { };

            var btnOracle = new Button { Text = "🔮 GELECEĞİ SOR", Dock = DockStyle.Right, Width = 140, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gold, Font = new Font("Segoe UI Bold", 8), BackColor = Color.FromArgb(30, 34, 40), Cursor = Cursors.Hand };
            btnOracle.FlatAppearance.BorderColor = Color.Gold;
            btnOracle.Click += (s, e) => { };
            
            pnlApexHeader.Controls.Add(btnOracle);
            pnlApexHeader.Controls.Add(btnOmni);
            pnlApexHeader.Controls.Add(btnRefreshApex);

            // MAIN LAYOUT
            var tblApexMain = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            tblApexMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            tblApexMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            // LEFT SIDE: GRIDS
            var tblApexGrids = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            tblApexGrids.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tblApexGrids.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            void StyleGrid(DataGridView dgv, string title) {
                dgv.Dock = DockStyle.Fill;
                dgv.BackgroundColor = Color.FromArgb(15, 15, 20);
                dgv.ForeColor = Color.White;
                dgv.BorderStyle = BorderStyle.None;
                dgv.RowHeadersVisible = false;
                dgv.AllowUserToAddRows = false;
                dgv.ReadOnly = true;
                dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dgv.EnableHeadersVisualStyles = false;
                dgv.ColumnHeadersHeight = 35;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 55);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.Cyan;
                dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                dgv.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 25);
                dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(100, 0, 255);
                dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 30);
                dgv.RowTemplate.Height = 45;
                dgv.GridColor = Color.FromArgb(40, 40, 45);
            }

            dgvApexPapers = new DataGridView { AutoGenerateColumns = false };
            StyleGrid(dgvApexPapers, "MAKALE");
            dgvApexPapers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "title", HeaderText = "📑 Makale Başlığı (HuggingFace)", MinimumWidth = 350, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvApexPapers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "upvotes", HeaderText = "🔥 UP", Width = 60 });
            
            dgvApexRepos = new DataGridView { AutoGenerateColumns = false };
            StyleGrid(dgvApexRepos, "REPO");
            dgvApexRepos.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "full_name", HeaderText = "💻 Trend Projeler (GitHub)", Width = 250 });
            dgvApexRepos.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "stars", HeaderText = "⭐ Stars", Width = 80 });
            dgvApexRepos.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "description", HeaderText = "📝 Açıklama", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            tblApexGrids.Controls.Add(dgvApexPapers, 0, 0);
            tblApexGrids.Controls.Add(dgvApexRepos, 0, 1);

            // RIGHT SIDE: REPORT
            var pnlReportArea = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 0, 0), BackColor = Color.Transparent };
            var lblReportHeader = new Label { Text = "🧠 GÜNLÜK STRATEJİK ANALİZ", Dock = DockStyle.Top, Height = 30, ForeColor = Color.Gold, Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            rtbApexReport = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 10, 15), ForeColor = Color.FromArgb(200, 200, 200), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None, ReadOnly = true };
            
            pnlReportArea.Controls.Add(rtbApexReport);
            pnlReportArea.Controls.Add(lblReportHeader);

            tblApexMain.Controls.Add(tblApexGrids, 0, 0);
            tblApexMain.Controls.Add(pnlReportArea, 1, 0);

            pnlApexContainer.Controls.Add(tblApexMain);
            pnlApexContainer.Controls.Add(pnlApexHeader);
            // pnlApexContainer is now complete and will be added to HIVE Hub on-demand.

            tabDashViews.TabPages.Add(tpChart);
            tabDashViews.TabPages.Add(tpTwitter);

            // SPACER FOR "1 CM" GAP (approx 40px)
            var pnlSpacer = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent, Tag="IGNORE_THEME" };
            
            pnlDashboard.Controls.Add(pnlSpacer);
            pnlDashboard.Controls.Add(tabDashViews);
            
            // Z-ORDERING FOR DOCKING
            // Back/Last is laid out FIRST (Top).
            // Front/First is laid out LAST (Fill).
            
            pnlSpacer.SendToBack();       // List: [.., Spacer] -> Docks Top (under Header)
            pnlDashHeader.SendToBack();   // List: [.., Spacer, Header] -> Header is Deepest -> Docks Top (Absolute Top)
            tabDashViews.BringToFront();  // List: [Tabs, .., Spacer, Header] -> Docks Fill (Remaining space)

            InitializeChart();
            InitializeTwitterWebView();


            // ========== Tab 2: Signal Center (SİNYAL MERKEZİ - v2.0) ==========
            var pnlSigMain = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 22, 25), Padding = new Padding(10) };
            
            // 1. KPI HEADER
            var pnlKPI = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, FlowDirection = FlowDirection.LeftToRight };
            
            // Helper for KPI Cards (local)
            Panel CreateKPICard(string title, string value, Color color) {
                var p = new Panel { Width = 200, Height = 80, BackColor = Color.FromArgb(30, 32, 35), Margin = new Padding(0, 0, 10, 0) };
                var lblT = new Label { Text = title, Dock = DockStyle.Top, Height = 25, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8), TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(10,5,0,0) };
                var lblV = new Label { Text = value, Dock = DockStyle.Fill, ForeColor = color, Font = new Font("Segoe UI", 20, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10,0,0,0) };
                p.Controls.Add(lblV); p.Controls.Add(lblT);
                p.Paint += (s, ev) => { using(var pen = new Pen(color, 2)) ev.Graphics.DrawLine(pen, 0, 10, 0, p.Height-10); };
                return p;
            }

            var kpi1 = CreateKPICard("GÜNLÜK SİNYAL", "0", Color.Cyan); lblDailySignals = (Label)kpi1.Controls[0];
            var kpi2 = CreateKPICard("BAŞARI (Tahmini)", "%--", Color.LimeGreen); lblSuccessRate = (Label)kpi2.Controls[0];
            var kpi3 = CreateKPICard("TREND GÖRÜNÜMÜ", "NÖTR", Color.Gold); lblTopStrategy = (Label)kpi3.Controls[0];

            pnlKPI.Controls.AddRange(new Control[] { kpi1, kpi2, kpi3 });
            pnlSigMain.Controls.Add(pnlKPI);

            // 2. FILTER BAR (Chips)
            var pnlFilters = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(0, 5, 0, 5) };
            var flowFilters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = false };

            CheckBox CreateToggle(string text, Color activeColor, bool isChecked) {
                var chk = new CheckBox { 
                    Text = text, Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, 
                    BackColor = Color.FromArgb(40, 40, 45), ForeColor = Color.Silver,
                    TextAlign = ContentAlignment.MiddleCenter, Checked = isChecked, AutoSize = true,
                    Margin = new Padding(0, 0, 5, 0), Cursor = Cursors.Hand
                };
                chk.FlatAppearance.BorderSize = 0;
                chk.FlatAppearance.CheckedBackColor = Color.FromArgb(activeColor.R/2, activeColor.G/2, activeColor.B/2);
                chk.CheckedChanged += (s, e) => { 
                    chk.ForeColor = chk.Checked ? activeColor : Color.Silver; 
                    chk.Font = new Font("Segoe UI", 9, chk.Checked ? FontStyle.Bold : FontStyle.Regular);
                };
                if(isChecked) { chk.ForeColor = activeColor; chk.Font = new Font("Segoe UI", 9, FontStyle.Bold); }
                return chk;
            }

            chkKing = CreateToggle("KING", Color.Gold, true); flowFilters.Controls.Add(chkKing);
            chkBomba = CreateToggle("BOMBA", Color.OrangeRed, true); flowFilters.Controls.Add(chkBomba);
            chkTeFo = CreateToggle("TEFO", Color.DeepSkyBlue, true); flowFilters.Controls.Add(chkTeFo);
            chkDip = CreateToggle("DIP", Color.SpringGreen, true); flowFilters.Controls.Add(chkDip);
            chkZirve = CreateToggle("ZIRVE", Color.Magenta, true); flowFilters.Controls.Add(chkZirve);
            chkAnka = CreateToggle("ANKA", Color.White, true); flowFilters.Controls.Add(chkAnka);
            chkMiner = CreateToggle("MINER", Color.Gold, true); flowFilters.Controls.Add(chkMiner);

            // Period separator
            flowFilters.Controls.Add(new Panel { Width = 20, Height = 20 });

            chkPer15 = CreateToggle("15dk", Color.LightGray, true); flowFilters.Controls.Add(chkPer15);
            chkPer60 = CreateToggle("60dk", Color.LightGray, true); flowFilters.Controls.Add(chkPer60);
            chkPer240 = CreateToggle("4S", Color.LightGray, true); flowFilters.Controls.Add(chkPer240);
            chkPerG = CreateToggle("GÜN", Color.LightGray, true); flowFilters.Controls.Add(chkPerG);
            
            // Advanced Settings Toggle
            var btnAdv = new Button { Text = "⚙️", Width = 40, Height = 30, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray };
            btnAdv.Click += (s, e) => { 
                using(var frm = new Form { Text = "Gelişmiş Filtreler", Size = new Size(400, 300), BackColor = Color.FromArgb(30,30,30), ForeColor = Color.White }) {
                     var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20) };
                     p.Controls.Add(new Label { Text = "Ortak Tarama (AND Logic):", AutoSize = true, ForeColor = Color.Cyan });
                     p.Controls.Add(chkComKing); p.Controls.Add(chkComBomba); p.Controls.Add(chkComTeFo);
                     p.Controls.Add(chkOnlyCommon);
                     p.Controls.Add(new Label { Text = "---", AutoSize = true });
                     p.Controls.Add(new Label { Text = "Eşik Değerler:", AutoSize = true, ForeColor = Color.Cyan });
                     p.Controls.Add(new Label { Text = "King Min:", AutoSize = true }); p.Controls.Add(numThresholdKing);
                     frm.Controls.Add(p);
                     frm.StartPosition = FormStartPosition.CenterParent;
                     frm.ShowDialog();
                }
            };
            flowFilters.Controls.Add(btnAdv);

            pnlFilters.Controls.Add(flowFilters);
            pnlSigMain.Controls.Add(pnlFilters);

            // 3. SMART GRID
            dgvSignals = new DataGridView { 
                Dock = DockStyle.Fill, 
                BackgroundColor = Color.FromArgb(25, 27, 30), 
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(40, 42, 45),
                RowTemplate = { Height = 40 }
            };
            
            dgvSignals.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 40);
            dgvSignals.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gray;
            dgvSignals.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            dgvSignals.ColumnHeadersHeight = 40;
            dgvSignals.DefaultCellStyle.BackColor = Color.FromArgb(25, 27, 30);
            dgvSignals.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvSignals.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 60);
            dgvSignals.DefaultCellStyle.Font = new Font("Segoe UI", 10);

            dgvSignals.Columns.Add("Time", "SAAT");
            dgvSignals.Columns.Add("Source", "STRATEJİ");
            dgvSignals.Columns.Add("Symbol", "SEMBOL");
            dgvSignals.Columns.Add("Period", "PERİYOT");
            dgvSignals.Columns.Add("Price", "FİYAT");
            dgvSignals.Columns.Add("Score", "SKOR");
            dgvSignals.Columns.Add("Status", "DURUM");

            // Custom Painting for Score & Status
            dgvSignals.CellFormatting += (s, e) => {
                if (e.RowIndex < 0) return;
                var col = dgvSignals.Columns[e.ColumnIndex].Name;
                if (col == "Symbol") { e.CellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold); e.CellStyle.ForeColor = Color.Cyan; }
                if (col == "Score") {
                   if (int.TryParse(e.Value?.ToString()?.Split('/')[0], out int sc)) {
                       e.CellStyle.ForeColor = sc >= 25 ? Color.Lime : (sc >= 20 ? Color.Gold : Color.Gray);
                   }
                }
            };

            var pnlGridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
            pnlGridContainer.Controls.Add(dgvSignals);
            pnlSigMain.Controls.Add(pnlGridContainer);

            // 4. COMPACT LOG
            rtbSignalLog = new RichTextBox { Dock = DockStyle.Bottom, Height = 100, BackColor = Color.FromArgb(15, 15, 20), ForeColor = Color.Gray, Font = new Font("Consolas", 8), ReadOnly = true, BorderStyle = BorderStyle.None };
            pnlSigMain.Controls.Add(rtbSignalLog);

            // 5. BOTTOM ACTIONS
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(0, 5, 0, 0) };
            var btnSaveF = new Button { Text = "💾 AYARLARI KAYDET", Dock = DockStyle.Right, Width = 150, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSaveF.Click += BtnSave_Click;
            pnlBottom.Controls.Add(btnSaveF);
            pnlSigMain.Controls.Add(pnlBottom);

            // Initialize hidden legacy controls (so code doesn't break)
            chkComKing = new CheckBox { Text = "King" }; chkComBomba = new CheckBox { Text = "Bomba" }; 
            chkComTeFo = new CheckBox { Text = "TeFo" }; chkComDip = new CheckBox { Text = "Dip" }; 
            chkComZirve = new CheckBox { Text = "Zirve" }; chkComAnka = new CheckBox { Text = "ANKA" };
            chkOnlyCommon = new CheckBox { Text = "Ortak" };
            numThresholdKing = new NumericUpDown(); numThresholdDip = new NumericUpDown(); numThresholdAnka = new NumericUpDown();
            txtScanHours = new TextBox();

            pnlSignals.Controls.Add(pnlSigMain);


            // ========== Tab 3: Settings (AYARLAR - Modern v3.0 with Benchmark) ==========
            var splitSettings = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 180, SplitterWidth = 2, BackColor = Color.FromArgb(30,30,30), FixedPanel = FixedPanel.Panel1 };
            
            // Left Menu
            var lstCategories = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40,40,45), ForeColor = Color.White, Font = new Font("Segoe UI", 11), BorderStyle = BorderStyle.None, ItemHeight = 40, DrawMode = DrawMode.OwnerDrawFixed };
            lstCategories.Items.AddRange(new object[] { "🔑 API & Bağlantılar", "🛡️ Spam & Güvenlik", "🎯 Hedef & Otomasyon" });
            splitSettings.Panel1.Controls.Add(lstCategories);
            
            // Right Content Container
            var pnlSetContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            splitSettings.Panel2.Controls.Add(pnlSetContent);

            // Sub-Panels (Created but only one visible)
            var pnlSetApi = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, Visible = true };
            var pnlSetSpam = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, Visible = false };
            var pnlSetTarget = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, Visible = false };
            
            pnlSetContent.Controls.Add(pnlSetApi);
            pnlSetContent.Controls.Add(pnlSetSpam);
            pnlSetContent.Controls.Add(pnlSetTarget);

            // --- API Panel ---
            void AddGroup(FlowLayoutPanel p, string title) { p.Controls.Add(new Label { Text = title, ForeColor = Color.Cyan, Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Margin = new Padding(0,10,0,10) }); }
            void AddField(FlowLayoutPanel p, string label, Control ctrl) { p.Controls.Add(new Label { Text = label, ForeColor = Color.Silver, AutoSize = true }); p.Controls.Add(ctrl); }

            AddGroup(pnlSetApi, "🐦 Twitter API");
            txtApiKey = new TextBox { Width = 400, BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, BorderStyle=BorderStyle.FixedSingle }; AddField(pnlSetApi, "API Key:", txtApiKey);
            txtApiSecret = new TextBox { Width = 400, PasswordChar='*', BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, BorderStyle=BorderStyle.FixedSingle }; AddField(pnlSetApi, "API Secret:", txtApiSecret);
            txtAccessToken = new TextBox { Width = 400, BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, BorderStyle=BorderStyle.FixedSingle }; AddField(pnlSetApi, "Access Token:", txtAccessToken);
            txtTokenSecret = new TextBox { Width = 400, PasswordChar='*', BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, BorderStyle=BorderStyle.FixedSingle }; AddField(pnlSetApi, "Token Secret:", txtTokenSecret);
            
            AddGroup(pnlSetApi, "🧠 AI & Model Yönetimi");
            
            // Gemini API Key Row (Compact)
            var pnlGeminiRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            pnlGeminiRow.Controls.Add(new Label { Text = "Gemini API Key:", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 8, 10, 0) });
            txtGeminiKey = new TextBox { Width = 320, PasswordChar='*', BackColor = Color.FromArgb(50,50,55), ForeColor = Color.LimeGreen, BorderStyle=BorderStyle.FixedSingle, Font = new Font("Consolas", 10) };
            pnlGeminiRow.Controls.Add(txtGeminiKey);
            
            // API Test Button
            var btnTestApi = new Button { Text = "🧪 Test", Width = 70, Height = 28, BackColor = Color.FromArgb(0, 120, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(10, 5, 0, 0), Cursor = Cursors.Hand };
            btnTestApi.FlatAppearance.BorderSize = 0;
            btnTestApi.Click += async (s, ev) => {
                btnTestApi.Text = "⏳...";
                btnTestApi.Enabled = false;
                try {
                    var benchmark = new ModelBenchmarkService();
                    var result = await benchmark.TestModelAsync(txtGeminiKey.Text, cmbGeminiModel.SelectedItem?.ToString() ?? "gemini-2.0-flash-exp");
                    if (result.Success)
                        MessageBox.Show($"✅ API Bağlantısı Başarılı!\n\nModel: {result.ModelName}\nYanıt Süresi: {result.ResponseTimeMs}ms", "API Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show($"❌ API Hatası\n\n{result.ErrorMessage}", "API Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } catch (Exception ex) {
                    MessageBox.Show($"❌ Hata: {ex.Message}", "API Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } finally {
                    btnTestApi.Text = "🧪 Test";
                    btnTestApi.Enabled = true;
                }
            };
            pnlGeminiRow.Controls.Add(btnTestApi);
            pnlSetApi.Controls.Add(pnlGeminiRow);
            
            // Perplexity API Key Row
            var pnlPerplexityRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 5, 0, 10) };
            pnlPerplexityRow.Controls.Add(new Label { Text = "Perplexity API Key:", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 8, 10, 0) });
            txtPerplexityKey = new TextBox { Width = 320, PasswordChar='*', BackColor = Color.FromArgb(50,50,55), ForeColor = Color.Cyan, BorderStyle=BorderStyle.FixedSingle, Font = new Font("Consolas", 10) };
            pnlPerplexityRow.Controls.Add(txtPerplexityKey);
            pnlSetApi.Controls.Add(pnlPerplexityRow);
            
            // Model Selection Row
            var pnlModelRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            pnlModelRow.Controls.Add(new Label { Text = "Aktif Model:", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 8, 10, 0) });
            cmbGeminiModel = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(50,50,55), ForeColor = Color.Gold, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10) };
            foreach(var m in ModelBenchmarkService.FallbackModels) cmbGeminiModel.Items.Add(m);
            cmbGeminiModel.SelectedIndex = 0;
            pnlModelRow.Controls.Add(cmbGeminiModel);
            
            // Model List Button - Fetches LIVE from API
            var btnListModels = new Button { Text = "📋 Modeller", Width = 90, Height = 28, BackColor = Color.FromArgb(70, 70, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(10, 5, 0, 0), Cursor = Cursors.Hand };
            btnListModels.FlatAppearance.BorderSize = 0;
            btnListModels.Click += async (s, ev) => {
                if (string.IsNullOrEmpty(txtGeminiKey.Text)) {
                    MessageBox.Show("Lütfen önce Gemini API Key girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                btnListModels.Text = "⏳...";
                btnListModels.Enabled = false;
                
                try {
                    var models = await ModelBenchmarkService.FetchAvailableModelsAsync(txtGeminiKey.Text);
                    
                    if (models.Count == 0) {
                        MessageBox.Show("API'den model listesi alınamadı. Fallback modeller kullanılacak.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    } else {
                        var sb = new StringBuilder();
                        sb.AppendLine($"📋 Kullanılabilir Gemini Modelleri ({models.Count} adet):\n");
                        sb.AppendLine("─────────────────────────────────────────────");
                        
                        foreach (var model in models.Take(15)) { // En fazla 15 model göster
                            string tier = model.Name.Contains("flash") ? "⚡ Hızlı" : model.Name.Contains("pro") ? "💎 Pro" : "🧪 Deneysel";
                            sb.AppendLine($"• {model.Name}");
                            sb.AppendLine($"   {model.DisplayName} | {tier}");
                        }
                        
                        if (models.Count > 15)
                            sb.AppendLine($"\n... ve {models.Count - 15} model daha.");
                        
                        sb.AppendLine("\n💡 ComboBox'ı güncellemek için 'Güncelle' butonunu kullanın.");
                        MessageBox.Show(sb.ToString(), "Canlı Model Listesi (API)", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        // Update ComboBox with fetched models
                        var currentSelection = cmbGeminiModel.SelectedItem?.ToString();
                        cmbGeminiModel.Items.Clear();
                        foreach (var model in models.Take(10)) // ComboBox'a ilk 10 model
                            cmbGeminiModel.Items.Add(model.Name);
                        
                        // Try to restore previous selection
                        int idx = cmbGeminiModel.Items.IndexOf(currentSelection);
                        cmbGeminiModel.SelectedIndex = idx >= 0 ? idx : 0;
                    }
                } catch (Exception ex) {
                    MessageBox.Show($"Hata: {ex.Message}", "API Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } finally {
                    btnListModels.Text = "📋 Modeller";
                    btnListModels.Enabled = true;
                }
            };
            pnlModelRow.Controls.Add(btnListModels);
            pnlSetApi.Controls.Add(pnlModelRow);
            
            // Benchmark Section
            var pnlBenchmark = new Panel { Width = 550, Height = 180, BackColor = Color.FromArgb(35, 35, 40), Margin = new Padding(0, 15, 0, 10) };
            var lblBenchTitle = new Label { Text = "🎯 Akıllı Model Seçimi (Benchmark)", Dock = DockStyle.Top, Height = 30, ForeColor = Color.Gold, Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 5, 0, 0) };
            pnlBenchmark.Controls.Add(lblBenchTitle);
            
            // Benchmark Results Grid
            var dgvBenchmark = new DataGridView { 
                Location = new Point(10, 35), Width = 530, Height = 95,
                BackgroundColor = Color.FromArgb(25, 25, 30), ForeColor = Color.White, BorderStyle = BorderStyle.None,
                ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(50, 50, 55), RowTemplate = { Height = 22 }
            };
            dgvBenchmark.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 40, 45);
            dgvBenchmark.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gray;
            dgvBenchmark.DefaultCellStyle.BackColor = Color.FromArgb(25, 25, 30);
            dgvBenchmark.DefaultCellStyle.ForeColor = Color.White;
            dgvBenchmark.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 100, 150);
            dgvBenchmark.Columns.Add("Model", "Model");
            dgvBenchmark.Columns.Add("Tier", "Tier");
            dgvBenchmark.Columns.Add("Time", "Süre (ms)");
            dgvBenchmark.Columns.Add("Status", "Durum");
            pnlBenchmark.Controls.Add(dgvBenchmark);
            
            // Benchmark Buttons
            var btnRunBenchmark = new Button { Text = "🚀 Benchmark Çalıştır", Location = new Point(10, 140), Width = 160, Height = 32, BackColor = Color.FromArgb(0, 100, 180), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnRunBenchmark.FlatAppearance.BorderSize = 0;
            
            var btnAutoSelect = new Button { Text = "✨ Otomatik Seç", Location = new Point(180, 140), Width = 130, Height = 32, BackColor = Color.FromArgb(80, 80, 90), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Enabled = false };
            btnAutoSelect.FlatAppearance.BorderSize = 0;
            
            var lblRecommendation = new Label { Text = "", Location = new Point(320, 145), Width = 220, Height = 25, ForeColor = Color.LimeGreen, Font = new Font("Segoe UI", 9) };
            
            List<ModelBenchmarkService.BenchmarkResult> _lastBenchmarkResults = null;
            
            btnRunBenchmark.Click += async (s, ev) => {
                if (string.IsNullOrEmpty(txtGeminiKey.Text)) { MessageBox.Show("Lütfen önce Gemini API Key girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                btnRunBenchmark.Text = "⏳ Test Ediliyor...";
                btnRunBenchmark.Enabled = false;
                dgvBenchmark.Rows.Clear();
                lblRecommendation.Text = "";
                
                try {
                    var benchmark = new ModelBenchmarkService();
                    var results = await benchmark.RunBenchmarkAsync(txtGeminiKey.Text);
                    _lastBenchmarkResults = results;
                    
                    foreach (var r in results) {
                        string status = r.Success ? "✅ OK" : $"❌ {r.ErrorMessage}";
                        dgvBenchmark.Rows.Add(r.ModelName, r.Tier, r.ResponseTimeMs.ToString(), status);
                    }
                    
                    var recommended = ModelBenchmarkService.GetRecommendedModel(results, "balanced");
                    lblRecommendation.Text = $"💡 Önerilen: {recommended}";
                    btnAutoSelect.Enabled = true;
                } catch (Exception ex) {
                    MessageBox.Show($"Benchmark Hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } finally {
                    btnRunBenchmark.Text = "🚀 Benchmark Çalıştır";
                    btnRunBenchmark.Enabled = true;
                }
            };
            
            btnAutoSelect.Click += (s, ev) => {
                if (_lastBenchmarkResults != null) {
                    var recommended = ModelBenchmarkService.GetRecommendedModel(_lastBenchmarkResults, "balanced");
                    for (int i = 0; i < cmbGeminiModel.Items.Count; i++) {
                        if (cmbGeminiModel.Items[i].ToString() == recommended) {
                            cmbGeminiModel.SelectedIndex = i;
                            MessageBox.Show($"✅ Model '{recommended}' seçildi.\n\nBu model benchmark sonuçlarına göre en uygun seçenektir.", "Otomatik Seçim", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            break;
                        }
                    }
                }
            };
            
            pnlBenchmark.Controls.Add(btnRunBenchmark);
            pnlBenchmark.Controls.Add(btnAutoSelect);
            pnlBenchmark.Controls.Add(lblRecommendation);
            pnlSetApi.Controls.Add(pnlBenchmark);

            // Telegram Section
            AddGroup(pnlSetApi, "📱 Telegram");
            var pnlTelRow1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            pnlTelRow1.Controls.Add(new Label { Text = "Bot Token:", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 8, 10, 0) });
            txtTelToken = new TextBox { Width = 380, PasswordChar='*', BackColor = Color.FromArgb(50,50,55), ForeColor = Color.White, BorderStyle=BorderStyle.FixedSingle, Font = new Font("Consolas", 10) };
            pnlTelRow1.Controls.Add(txtTelToken);
            pnlSetApi.Controls.Add(pnlTelRow1);
            
            var pnlTelRow2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 5, 0, 10) };
            pnlTelRow2.Controls.Add(new Label { Text = "Chat ID:", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 8, 10, 0) });
            txtTelChatId = new TextBox { Width = 200, BackColor = Color.FromArgb(50,50,55), ForeColor = Color.Orange, BorderStyle=BorderStyle.FixedSingle, Font = new Font("Consolas", 10) };
            pnlTelRow2.Controls.Add(txtTelChatId);
            pnlSetApi.Controls.Add(pnlTelRow2);

            // TradingView Section
            AddGroup(pnlSetApi, "📈 TradingView");
            var pnlTvRow1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            pnlTvRow1.Controls.Add(new Label { Text = "Sembol (NASDAQ:AAPL):", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 8, 10, 0) });
            txtTvSymbol = new TextBox { Width = 200, BackColor = Color.FromArgb(50,50,55), ForeColor = Color.Cyan, BorderStyle=BorderStyle.FixedSingle, Font = new Font("Consolas", 10) };
            pnlTvRow1.Controls.Add(txtTvSymbol);
            pnlSetApi.Controls.Add(pnlTvRow1);
            
            var pnlTvRow2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 5, 0, 10) };
            pnlTvRow2.Controls.Add(new Label { Text = "Chart ID:", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 8, 10, 0) });
            txtTvChartId = new TextBox { Width = 150, BackColor = Color.FromArgb(50,50,55), ForeColor = Color.White, BorderStyle=BorderStyle.FixedSingle, Font = new Font("Consolas", 10) };
            pnlTvRow2.Controls.Add(txtTvChartId);
            pnlSetApi.Controls.Add(pnlTvRow2);

            // Cookie Import Button
            var flowCookies = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            var btnImportCookies = new Button { Text = "🐦 X Çerezlerini İçe Aktar", Width = 180, Height = 35, BackColor = Color.FromArgb(60, 60, 70), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnImportCookies.FlatAppearance.BorderSize = 0;
            btnImportCookies.Click += async (s, ev) => {
                 using (var ofd = new OpenFileDialog { Filter = "JSON Files|*.json" }) if (ofd.ShowDialog() == DialogResult.OK) {
                     var res = await _opManager.SocialIntel.ImportCookiesAsync(File.ReadAllText(ofd.FileName)); MessageBox.Show(res.Message);
                 }
            };
            flowCookies.Controls.Add(btnImportCookies);
            pnlSetApi.Controls.Add(flowCookies);

            // --- Spam Panel ---
            AddGroup(pnlSetSpam, "🛡️ Spam Koruması");
            chkSpamSignals = new CheckBox { Text = "Sinyal Tweetleri", ForeColor = Color.White, AutoSize = true }; pnlSetSpam.Controls.Add(chkSpamSignals);
            chkSpamBatches = new CheckBox { Text = "Toplu (Batch) Thread", ForeColor = Color.White, AutoSize = true }; pnlSetSpam.Controls.Add(chkSpamBatches);
            chkSpamManual = new CheckBox { Text = "Manuel Analiz Paylaşımları", ForeColor = Color.White, AutoSize = true }; pnlSetSpam.Controls.Add(chkSpamManual);
            chkSpamNews = new CheckBox { Text = "Haber Paylaşımları", ForeColor = Color.White, AutoSize = true }; pnlSetSpam.Controls.Add(chkSpamNews);
            chkSpamReports = new CheckBox { Text = "Raporlar (Günlük/Haftalık)", ForeColor = Color.White, AutoSize = true }; pnlSetSpam.Controls.Add(chkSpamReports);
            chkSpamMotivation = new CheckBox { Text = "Motivasyon Tweetleri", ForeColor = Color.White, AutoSize = true }; pnlSetSpam.Controls.Add(chkSpamMotivation);

            // --- Target Panel ---
            AddGroup(pnlSetTarget, "🎯 Hedef & Otomasyon");
            txtTargetAccounts = new TextBox { Width = 600, Height = 100, Multiline = true, BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, BorderStyle=BorderStyle.FixedSingle }; 
            AddField(pnlSetTarget, "Hedef Hesaplar (Virgülle ayır):", txtTargetAccounts);
            
            chkAuto = new CheckBox { Text = "OTO AI TWEET (Sinyaller için)", ForeColor = Color.FromArgb(0, 229, 255), Font = new Font("Segoe UI Black", 9), AutoSize = true, Margin = new Padding(0,20,0,0) };
            pnlSetTarget.Controls.Add(chkAuto);

            // Logic for switching panels
            lstCategories.SelectedIndexChanged += (s, e) => {
                pnlSetApi.Visible = lstCategories.SelectedIndex == 0;
                pnlSetSpam.Visible = lstCategories.SelectedIndex == 1;
                pnlSetTarget.Visible = lstCategories.SelectedIndex == 2;
            };
            
            // Custom Draw for ListBox styling
            lstCategories.DrawItem += (s, e) => {
                if (e.Index < 0) return;
                e.DrawBackground();
                bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                if (selected) e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(0, 120, 215)), e.Bounds);
                else e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40,40,45)), e.Bounds);
                
                TextRenderer.DrawText(e.Graphics, lstCategories.Items[e.Index].ToString(), lstCategories.Font, e.Bounds, selected ? Color.White : Color.Silver, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                e.DrawFocusRectangle();
            };
            lstCategories.SelectedIndex = 0; // Default

            pnlSettings.Controls.Add(splitSettings);
            
            // Save Button at Bottom
            btnSave = new Button { Text = "💾 TÜM AYARLARI KAYDET", Dock = DockStyle.Bottom, Height = 60, BackColor = Color.DodgerBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.Click += BtnSave_Click;
            pnlSettings.Controls.Add(btnSave);

            // ========== Tab 4: Manuel Analiz (Renamed from AI Analysis) ==========
            InitializeManualAnalysisTab(pnlAnalysis);

            // ========== Tab 5: Bot Etkileşim ==========
            InitializeBotInteractionTab(pnlBot);
        }

        private void ShowPanel(Panel activePanel, Button activeBtn)
        {
            // Hide all
            pnlDashboard.Visible = false;
            pnlSignals.Visible = false;
            pnlAnalysis.Visible = false;
            pnlSettings.Visible = false;
            pnlBot.Visible = false;
            pnlHistory.Visible = false;
            pnlNews.Visible = false;
            pnlGuruCenter.Visible = false;
            pnlFenerbahce.Visible = false;

            
            // Reset Buttons
            void ResetBtn(Button b) { if (b != null) { b.BackColor = Color.Transparent; b.ForeColor = Color.Silver; b.Tag = null; } }
            ResetBtn(btnNavDash); ResetBtn(btnNavSignals); ResetBtn(btnNavAnalysis); ResetBtn(btnNavSettings); 
            ResetBtn(btnNavBot); ResetBtn(btnNavHistory); ResetBtn(btnNavInfluencers); ResetBtn(btnNavNews); 
            ResetBtn(btnNavFenerbahce); ResetBtn(btnNavGuru);

            // Show Active
            activePanel.Visible = true;
            activePanel.BringToFront();
            
            // Highlight Button
            activeBtn.BackColor = Color.FromArgb(50, 54, 62);
            activeBtn.ForeColor = Color.White;
            activeBtn.Tag = "active";
        }

        private ListView _influencerListView = null!;
        private ComboBox _influencerCategoryFilter = null!;
        private bool _influencerPanelInitialized = false;
        private bool _suppressEvents = false; // Fix: Prevent re-entrancy

        private ListView _kbListView = null!;
        private Label _lblDeepScanStatus = null!;

        private void InitializeInfluencerPanel()
        {
            if (_influencerPanelInitialized) return;
            _influencerPanelInitialized = true;

            var tabInfluencer = new TabControl { Dock = DockStyle.Fill };
            var tpList = new TabPage("👥 Fenomen Listesi") { BackColor = Color.FromArgb(30, 30, 30) };
            var tpKB = new TabPage("🧠 Bilgi Tabanı (Database)") { BackColor = Color.FromArgb(30,30,30) };
            tabInfluencer.TabPages.Add(tpList);
            tabInfluencer.TabPages.Add(tpKB);

            // --- TAB 1: Influencer List ---
            // Top Panel
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(45, 45, 48), Padding = new Padding(15) };
            var lblTitle = new Label { Text = "👥 Fenomen Veritabanı", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(15, 15) };
            topPanel.Controls.Add(lblTitle);

            var lblFilter = new Label { Text = "Kategori:", ForeColor = Color.White, Font = new Font("Segoe UI", 10), Location = new Point(280, 18), AutoSize = true };
            topPanel.Controls.Add(lblFilter);

            _influencerCategoryFilter = new ComboBox { 
                Location = new Point(350, 15), 
                Width = 120, 
                 DropDownStyle = ComboBoxStyle.DropDownList, 
                BackColor = Color.FromArgb(60, 60, 60), 
                ForeColor = Color.White 
            };
            _influencerCategoryFilter.Items.Add("Tümü");
            // Index assignment moved to end
            _influencerCategoryFilter.SelectedIndexChanged += (s, e) => { if (!_suppressEvents) RefreshInfluencerListView(); };
            topPanel.Controls.Add(_influencerCategoryFilter);

            var btnRefresh = new Button { Text = "🔄", Location = new Point(480, 12), Width = 40, Height = 35, BackColor = Color.DimGray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => RefreshInfluencerListView();
            topPanel.Controls.Add(btnRefresh);

            // Deep Scan Controls (Independent)
            var btnStartScan = new Button { Text = "▶ Taramayı Başlat", Location = new Point(540, 12), Width = 140, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btnStartScan.Click += (s, e) => {
                // _deepScanTimer (removed).Start();
                _lblDeepScanStatus.Text = "📡 TARAMA AKTİF (Her 45dk)";
                _lblDeepScanStatus.ForeColor = Color.Lime;
                Log("🧠 Hafıza Taraması (Deep Scan) manuel olarak BAŞLATILDI.", "System");
                // Run once immediately
                Task.Run(() => _opManager.SocialIntel.PerformDeepScanAsync((msg) => Log(msg, "Social")));
            };
            topPanel.Controls.Add(btnStartScan);

            var btnStopScan = new Button { Text = "⏹ Durdur", Location = new Point(690, 12), Width = 80, Height = 35, BackColor = Color.Firebrick, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btnStopScan.Click += (s, e) => {
                // _deepScanTimer (removed).Stop();
                _lblDeepScanStatus.Text = "⏸ TARAMA DURDURULDU";
                _lblDeepScanStatus.ForeColor = Color.Orange;
                Log("🧠 Hafıza Taraması (Deep Scan) DURDURULDU.", "System");
            };
            topPanel.Controls.Add(btnStopScan);

            _lblDeepScanStatus = new Label { Text = "⏸ TARAMA DURDURULDU", Location = new Point(780, 18), AutoSize = true, ForeColor = Color.Orange, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            topPanel.Controls.Add(_lblDeepScanStatus);

            // Bottom Action Panel
            var actionPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(40, 40, 45), Padding = new Padding(15) };

            var txtNewHandle = new TextBox { Location = new Point(15, 15), Width = 150, BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White, PlaceholderText = "@kullanıcı_adı" };
            actionPanel.Controls.Add(txtNewHandle);

            var cmbNewCategory = new ComboBox { Location = new Point(175, 15), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White };
            cmbNewCategory.Items.AddRange(new object[] { "BIST", "CRYPTO", "FOREX", "TECH", "BUSINESS", "PERSONAL", "GLOBAL" });
            cmbNewCategory.SelectedIndex = 0;
            actionPanel.Controls.Add(cmbNewCategory);

            var btnAdd = new Button { Text = "➕ Ekle", Location = new Point(290, 12), Width = 80, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtNewHandle.Text)) return;
                string category = cmbNewCategory.SelectedItem?.ToString() ?? "BIST";
                bool added = _opManager.InfluencerControl.AddInfluencer(category, txtNewHandle.Text);
                if (added) { txtNewHandle.Clear(); }
                else MessageBox.Show("Bu fenomen zaten mevcut.");
            };
            actionPanel.Controls.Add(btnAdd);

            var btnDelete = new Button { Text = "🗑️ Sil", Location = new Point(380, 12), Width = 80, Height = 35, BackColor = Color.Firebrick, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += (s, e) => {
                if (_influencerListView.SelectedItems.Count == 0) return;
                var item = _influencerListView.SelectedItems[0];
                string category = item.SubItems[0].Text;
                string handle = item.SubItems[1].Text;
                var list = _opManager.InfluencerControl.GetInfluencers(category);
                var inf = list.FirstOrDefault(i => i.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase));
                if (inf != null) { _opManager.InfluencerControl.DeleteInfluencer(category, inf.Handle); }
            };
            actionPanel.Controls.Add(btnDelete);

            var btnReset = new Button { Text = "🔁 Varsayılana Dön", Location = new Point(480, 12), Width = 140, Height = 35, BackColor = Color.DarkGoldenrod, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += (s, e) => { 
                if (MessageBox.Show("Tüm fenomenler silinip varsayılan liste yüklenecek. Emin misiniz?", "Onay", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { _opManager.InfluencerControl.ResetDatabase(); RefreshInfluencerListView(); }
            };
            actionPanel.Controls.Add(btnReset);

            // ListView
            _influencerListView = new ListView { 
                Dock = DockStyle.Fill, 
                View = View.Details, 
                FullRowSelect = true, 
                BackColor = Color.FromArgb(30, 30, 30), 
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            _influencerListView.Columns.Add("Kategori", 100);
            _influencerListView.Columns.Add("Kullanıcı", 200);
            _influencerListView.Columns.Add("Skor", 80);
            _influencerListView.Columns.Add("Eklenme Tarihi", 150);

            tpList.Controls.Add(_influencerListView);
            tpList.Controls.Add(actionPanel);
            tpList.Controls.Add(topPanel);

            // --- TAB 2: Knowledge Base ---
            var pnlKBAction = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(45, 45, 48), Padding = new Padding(10) };
            var btnRefreshKB = new Button { Text = "🔄 Veritabanını Yenile", Dock = DockStyle.Left, Width = 180, BackColor = Color.DimGray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnRefreshKB.Click += (s, e) => RefreshKnowledgeBaseListView();
            pnlKBAction.Controls.Add(btnRefreshKB);

            _kbListView = new ListView { 
                Dock = DockStyle.Fill, 
                View = View.Details, 
                FullRowSelect = true, 
                BackColor = Color.FromArgb(20, 20, 20), 
                ForeColor = Color.Lime, 
                Font = new Font("Consolas", 9) 
            };
            _kbListView.Columns.Add("Tarih", 130);
            _kbListView.Columns.Add("Yazar", 120);
            _kbListView.Columns.Add("İçerik", 450);
            _kbListView.Columns.Add("İlgili Semboller", 150);
            _kbListView.Columns.Add("Skor", 50);

            tpKB.Controls.Add(_kbListView);
            tpKB.Controls.Add(pnlKBAction);


            pnlInfluencers.Controls.Add(tabInfluencer);

            RefreshInfluencerListView();
            RefreshKnowledgeBaseListView();
        }

        private void RefreshKnowledgeBaseListView()
        {
            if (_kbListView == null || _opManager.Memory == null) return;
            if (_kbListView.InvokeRequired) { this.Invoke(new Action(RefreshKnowledgeBaseListView)); return; }

            _kbListView.Items.Clear();
            var items = _opManager.Memory.GetKnowledgeBase();

            foreach (var item in items.OrderByDescending(x => x.PostDate))
            {
                var lvi = new ListViewItem(item.PostDate.ToString("dd.MM HH:mm"));
                lvi.SubItems.Add(item.Author);
                lvi.SubItems.Add(item.Content.Replace("\n", " "));
                lvi.SubItems.Add(string.Join(", ", item.RelatedSymbols));
                lvi.SubItems.Add(item.RelevanceScore.ToString());
                _kbListView.Items.Add(lvi);
            }
        }

        private void RefreshInfluencerListView()
        {
            if (_influencerListView == null || _opManager?.InfluencerControl == null) return;
            if (_influencerListView.InvokeRequired) { this.Invoke(new Action(RefreshInfluencerListView)); return; }

            try
            {
                _suppressEvents = true; // Block event handler
                
                // v3.5.3: Sync Categories
                string currentFilter = _influencerCategoryFilter?.SelectedItem?.ToString() ?? "Tümü";
                var dbCategories = _opManager.InfluencerControl.GetCategories();
                
                _influencerCategoryFilter.BeginUpdate();
                _influencerCategoryFilter.Items.Clear();
                _influencerCategoryFilter.Items.Add("Tümü");
                foreach(var c in dbCategories) _influencerCategoryFilter.Items.Add(c);
                
                // Restore selection
                int idx = _influencerCategoryFilter.FindStringExact(currentFilter);
                _influencerCategoryFilter.SelectedIndex = idx >= 0 ? idx : 0;
            }
            finally
            {
                _influencerCategoryFilter.EndUpdate();
                _suppressEvents = false; // Restore events
            }

            _influencerListView.Items.Clear();
            List<Influencer> list;

            if (_influencerCategoryFilter.SelectedIndex == 0 || _influencerCategoryFilter.SelectedIndex == -1) // Tümü or None
            {
                list = _opManager.InfluencerControl.GetAllInfluencers();
            }
            else
            {
                var selected = _influencerCategoryFilter.SelectedItem;
                if (selected == null) return; // Guard against null
                list = _opManager.InfluencerControl.GetInfluencers(selected.ToString());
            }

            foreach (var inf in list)
            {
                var item = new ListViewItem(inf.Category);
                item.SubItems.Add(inf.Handle);
                item.SubItems.Add(inf.Score.ToString());
                item.SubItems.Add(inf.AddedDate.ToString("dd.MM.yyyy"));
                
                // Highlight newer entries
                if (inf.AddedDate > DateTime.Now.AddDays(-1)) 
                    item.ForeColor = Color.Lime;

                _influencerListView.Items.Add(item);
            }
        }

        private RichTextBox _historyLogView = null!;
        private ComboBox _historyModuleFilter = null!;
        private bool _historyInitialized = false;

        private void InitializeHistoryPanel()
        {
            if (_historyInitialized) return;
            _historyInitialized = true;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(45, 45, 48), Padding = new Padding(15) };
            var lblTitle = new Label { Text = "📜 Aktivite Geçmişi", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(15, 15) };
            topPanel.Controls.Add(lblTitle);

            var lblFilter = new Label { Text = "Modül:", ForeColor = Color.White, Font = new Font("Segoe UI", 10), Location = new Point(250, 18), AutoSize = true };
            topPanel.Controls.Add(lblFilter);

            _historyModuleFilter = new ComboBox { 
                Location = new Point(310, 15), 
                Width = 150, 
                DropDownStyle = ComboBoxStyle.DropDownList, 
                BackColor = Color.FromArgb(60, 60, 60), 
                ForeColor = Color.White 
            };
            _historyModuleFilter.Items.AddRange(new object[] { "Tümü", "System", "Twitter", "Social", "AI", "Telegram", "News", "Signal" });
            _historyModuleFilter.SelectedIndex = 0;
            _historyModuleFilter.SelectedIndexChanged += (s, e) => LoadActivityHistory();
            topPanel.Controls.Add(_historyModuleFilter);

            var btnRefresh = new Button { 
                Text = "🔄 Yenile", 
                Location = new Point(480, 12), 
                Width = 100, 
                Height = 35, 
                BackColor = Color.DimGray, 
                ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat 
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadActivityHistory();
            topPanel.Controls.Add(btnRefresh);

            _historyLogView = new RichTextBox { 
                Dock = DockStyle.Fill, 
                BackColor = Color.FromArgb(25, 25, 25), 
                ForeColor = Color.White, 
                Font = new Font("Consolas", 10), 
                ReadOnly = true, 
                BorderStyle = BorderStyle.None 
            };

            pnlHistory.Controls.Add(_historyLogView);
            pnlHistory.Controls.Add(topPanel);
        }

        private void InitializeNewsPanel()
        {
            if (_newsInitialized) return;
            _newsInitialized = true;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(40, 44, 52), Padding = new Padding(15) };
            var lblTitle = new Label { Text = "📰 Haber Editörü", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(15, 15) };
            topPanel.Controls.Add(lblTitle);

            btnNewsStart = new Button { Text = "▶ TAKİBİ BAŞLAT", Location = new Point(220, 12), Width = 140, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
            btnNewsStart.FlatAppearance.BorderSize = 0;
            btnNewsStart.Click += (s, e) => { 
                _isNewsTrackerRunning = true; 
                btnNewsStart.Enabled = false; 
                btnNewsStop.Enabled = true; 
                
                // EVENT WIRING FIX (v3.8.3)
                // Prevent double-subscription
                _opManager.NewsTracker.OnNewsDetected -= OnNewsReceived;
                _opManager.NewsTracker.OnNewsDetected += OnNewsReceived;
                
                _opManager.NewsTracker.Start(); // Fix: Actually start the service
                Log("📰 Haber takibi aktif.", "News"); 
            };
            topPanel.Controls.Add(btnNewsStart);

            btnNewsStop = new Button { Text = "⏹ DURDUR", Location = new Point(370, 12), Width = 100, Height = 35, BackColor = Color.Firebrick, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold), Enabled = false };
            btnNewsStop.FlatAppearance.BorderSize = 0;
            btnNewsStop.Click += (s, e) => { 
                _isNewsTrackerRunning = false; 
                btnNewsStart.Enabled = true; 
                btnNewsStop.Enabled = false; 
                _opManager.NewsTracker.Stop(); // Fix: Actually stop the service
                Log("🛑 Haber takibi durduruldu.", "News"); 
            };
            topPanel.Controls.Add(btnNewsStop);

            pnlNews.Controls.Add(topPanel);

            // TABS
            var tabNews = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal }; // Better visibility for debug
            tabNews.SizeMode = TabSizeMode.Normal;

            // Tab 1: PENDING
            var pgPending = new TabPage("⚠️ Onay Bekleyenler");
            pgPending.BackColor = Color.FromArgb(30,30,30);
            pnlNewsPending = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10), BackColor = Color.FromArgb(35,35,40) };
            pnlNewsPending.Controls.Add(new Label { Text = "⌛ Onay bekleyen haber bulunmuyor.", ForeColor = Color.Gray, AutoSize = true });
            pgPending.Controls.Add(pnlNewsPending);
            tabNews.TabPages.Add(pgPending);

            // Tab 2: PUBLISHED
            var pgPublished = new TabPage("✅ Yayınlananlar");
            pgPublished.BackColor = Color.FromArgb(30,30,30);
            pnlNewsCards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10), BackColor = Color.FromArgb(30,30,30) };
            pnlNewsCards.Controls.Add(new Label { Text = "✅ Yayınlanan haber bulunmuyor.", ForeColor = Color.Gray, AutoSize = true });
            pgPublished.Controls.Add(pnlNewsCards);
            tabNews.TabPages.Add(pgPublished);

            // Tab 3: LIVE FEED
            var pgLive = new TabPage("📡 Canlı Akış");
            pgLive.BackColor = Color.FromArgb(30,30,30);
            pnlNewsLive = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10), BackColor = Color.Black };
            pgLive.Controls.Add(pnlNewsLive);
            tabNews.TabPages.Add(pgLive);

            pnlNews.Controls.Clear(); // Ensure it's empty before adding
            pnlNews.Controls.Add(tabNews);
            pnlNews.Controls.Add(topPanel);
            
            // WinForms Docking Rule: controls are docked from front to back in Z-order.
            // Controls docked Fill should be sent to BACK to allow Top/Bottom docked controls to take their space.
            topPanel.SendToBack(); // Docks at the absolute top
            tabNews.BringToFront(); // Takes the remaining Fill space
            tabNews.Dock = DockStyle.Fill;
            topPanel.Dock = DockStyle.Top;
            
            // Drain Buffer
            if (_newsBuffer.Count > 0)
            {
                var copy = _newsBuffer.ToList();
                _newsBuffer.Clear();
                foreach(var b in copy) AddNewsCard(b.item, b.analysis, b.status);
            }

            if (_isNewsTrackerRunning) { btnNewsStart.Enabled = false; btnNewsStop.Enabled = true; }
        }

        private void AddNewsCard(NewsItem news, string? analysis = null, string status = "PUBLISHED")
        {
            if (this.InvokeRequired) { this.Invoke(() => AddNewsCard(news, analysis, status)); return; }

            if (!_newsInitialized)
            {
                _newsBuffer.Add((news, analysis, status));
                return;
            }

            // Target Panel Selection
            FlowLayoutPanel targetPanel = status == "PENDING" ? pnlNewsPending : (status == "LIVE" ? pnlNewsLive : pnlNewsCards);
            Color cardColor = status == "PENDING" ? Color.FromArgb(60, 50, 20) : (status == "LIVE" ? Color.FromArgb(20, 20, 25) : Color.FromArgb(45, 45, 50));

            var card = new Panel { Width = Math.Max(300, targetPanel.Width - 40), Height = status == "PENDING" ? 140 : 100, BackColor = cardColor, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(10) };
            
            var lblHdr = new Label { Text = $"[{news.Source}] {news.Title}", Dock = DockStyle.Top, Height = 40, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoEllipsis = true };
            card.Controls.Add(lblHdr);

            if (!string.IsNullOrEmpty(analysis))
            {
                var lblAn = new Label { Text = "AI: " + (analysis.Length > 100 ? analysis.Substring(0, 100) + "..." : analysis), Dock = DockStyle.Fill, ForeColor = Color.LimeGreen, Font = new Font("Segoe UI", 8, FontStyle.Italic), AutoEllipsis = true };
                card.Controls.Add(lblAn);
            }
            
            // Action Buttons for PENDING
            if (status == "PENDING")
            {
                var pnlBtns = new Panel { Dock = DockStyle.Bottom, Height = 35 };
                
                var btnApprove = new Button { Text = "✅ ONAYLA", Dock = DockStyle.Right, Width = 80, BackColor = Color.Green, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7, FontStyle.Bold) };
                btnApprove.FlatAppearance.BorderSize = 0;
                btnApprove.Click += async (s, e) => {
                    targetPanel.Controls.Remove(card);
                    await _opManager.NewsEng.ForcePostNews(news, analysis ?? "");
                    // Moved to published automatically by NewsEngine event, but we can double check
                };
                
                var btnReject = new Button { Text = "🗑️ REDDET", Dock = DockStyle.Right, Width = 80, BackColor = Color.Red, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7, FontStyle.Bold), Margin = new Padding(0,0,5,0) };
                btnReject.FlatAppearance.BorderSize = 0;
                btnReject.Click += (s, e) => {
                    targetPanel.Controls.Remove(card);
                    Log($"Haber kullanıcı tarafından reddedildi: {news.Title}", "News");
                };

                pnlBtns.Controls.Add(btnApprove);
                pnlBtns.Controls.Add(btnReject);
                card.Controls.Add(pnlBtns);
            }

            var lblTime = new Label { Text = DateTime.Now.ToString("HH:mm:ss"), Dock = DockStyle.Bottom, Height = 20, ForeColor = Color.Gray, Font = new Font("Segoe UI", 7), TextAlign = ContentAlignment.BottomRight };
            card.Controls.Add(lblTime);

            targetPanel.Controls.Add(card);
            targetPanel.Controls.SetChildIndex(card, 0); // Newest on top
            
            // Limit cards
            if (targetPanel.Controls.Count > 50) targetPanel.Controls.RemoveAt(targetPanel.Controls.Count - 1);
        }

        private async void LoadActivityHistory()
        {
            // v3.0.2 FIX: Initialize panel FIRST to ensure controls exist before access
            InitializeHistoryPanel();

            if (_historyLogView == null) return;

            // Clear immediately to show feedback
            _historyLogView.Clear();
            _historyLogView.Text = "Loglar yükleniyor, lütfen bekleyin...";

            // Capture filter value on UI Thread BEFORE running background task
            string filter = _historyModuleFilter?.SelectedItem?.ToString() ?? "Tümü";

            try
            {
                // Run I/O in background
                var historyText = await Task.Run(() => 
                {
                    string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI", "Logs");
                    if (!Directory.Exists(logDir)) return "Henüz log kaydı bulunamadı.";

                    var allLines = new List<(DateTime time, string module, string line)>();

                    foreach (var file in Directory.GetFiles(logDir, "Log_*.txt"))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        var parts = fileName.Split('_');
                        if (parts.Length < 3) continue;

                        string moduleName = parts[parts.Length - 1]; // e.g. "System" from "Log_2024-01-01_System"
                        
                        // Apply filter
                        if (filter != "Tümü" && !string.Equals(moduleName, filter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        try
                        {
                            // Use FileStream for non-locking read
                            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var sr = new StreamReader(fs, Encoding.UTF8))
                            {
                                string? logLine;
                                while ((logLine = sr.ReadLine()) != null)
                                {
                                    if (string.IsNullOrWhiteSpace(logLine)) continue;
                                    
                                    // Parse timestamp: [09.01 14:30:15]
                                    var timePart = logLine.Split(']', 2);
                                    if (timePart.Length > 1)
                                    {
                                        string tsStr = timePart[0].TrimStart('[');
                                        // Attempt to parse, but fallback to file date if fail
                                        if (DateTime.TryParseExact(tsStr, "dd.MM HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dt))
                                        {
                                            allLines.Add((dt, moduleName, logLine));
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Skip corrupted files */ }
                    }

                    if (allLines.Count == 0) return "Seçili modül için log kaydı bulunamadı.";

                    // Sort: Newest first
                    var sb = new StringBuilder();
                    foreach (var log in allLines.OrderByDescending(x => x.time).Take(1000))
                    {
                        sb.AppendLine(log.line);
                    }

                    return sb.ToString();
                });

                // Update UI
                if (_historyLogView != null && !this.IsDisposed)
                {
                    _historyLogView.Clear();
                    _historyLogView.Text = historyText;
                }
            }
            catch (Exception ex)
            {
                if (_historyLogView != null && !this.IsDisposed)
                    _historyLogView.Text = "Hata: " + ex.Message;
            }
        }

        private async void SyncTvSessionButton_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.Enabled = false;
            
            bool success = await SaveTradingViewCookiesAsync();
            if (success)
            {
                Log("✅ TradingView oturumu senkronize edildi", "System");
                MessageBox.Show("TradingView çerezleri başarıyla kaydedildi.\nGrafikleriniz artık bu oturumla açılacak.");
            }
            else
            {
                Log("❌ TradingView oturumu kaydedilemedi", "Error");
            }
            
            if (btn != null) btn.Enabled = true;
        }

        private async Task<bool> SaveTradingViewCookiesAsync()
        {
            try
            {
                var cookieManager = _webViewChart.CoreWebView2.CookieManager;
                var cookies = await cookieManager.GetCookiesAsync("https://www.tradingview.com");
                
                var cookieList = new List<Dictionary<string, object>>();
                foreach (var c in cookies)
                {
                    cookieList.Add(new Dictionary<string, object>
                    {
                        { "name", c.Name },
                        { "value", c.Value },
                        { "domain", c.Domain },
                        { "path", c.Path },
                        { "expires", c.Expires },
                        { "secure", c.IsSecure },
                        { "httpOnly", c.IsHttpOnly }
                    });
                }

                string json = JsonSerializer.Serialize(cookieList);
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
                if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
                
                string destPath = Path.Combine(appDataDir, "tradingview_cookies.json");
                File.WriteAllText(destPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveTvCookies Error: " + ex.Message);
                return false;
            }
        }

        private async void InitializeChart()
        {
            try
            {
                await _webViewChart.EnsureCoreWebView2Async();

                // TradingView Çerez Enjeksiyonu
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
                string cookieJsonPath = Path.Combine(appDataDir, "tradingview_cookies.json");

                if (File.Exists(cookieJsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(cookieJsonPath);
                        var cookies = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

                        if (cookies != null)
                        {
                            var cookieManager = _webViewChart.CoreWebView2.CookieManager;
                            foreach (var c in cookies)
                            {
                                string name = c.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                                string value = c.TryGetValue("value", out var v) ? v?.ToString() ?? "" : "";
                                string domain = c.TryGetValue("domain", out var d) ? d?.ToString() ?? ".tradingview.com" : ".tradingview.com";
                                string path = c.TryGetValue("path", out var p) ? p?.ToString() ?? "/" : "/";

                                if (!string.IsNullOrEmpty(name))
                                {
                                    var cookie = cookieManager.CreateCookie(name, value, domain, path);
                                    cookieManager.AddOrUpdateCookie(cookie);
                                }
                            }
                            Log("✅ TradingView oturum çerezleri enjekte edildi", "System");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TV Cookie Injection Failed: " + ex.Message);
                    }
                }

                _webViewChart.Source = new Uri("https://www.tradingview.com/chart/?theme=dark");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Chart Init Failed: " + ex.Message);
            }
        }

        private async void SyncSessionButton_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.Enabled = false;
            
            bool success = await SaveTwitterCookiesAsync();
            if (success)
            {
                Log("✅ X Oturumu senkronize edildi (WebView2 -> Pickle)", "System");
                MessageBox.Show("X oturum çerezleri başarıyla kaydedildi.\nSinyal botu artık bu yeni oturumu kullanacak.");
            }
            else
            {
                Log("❌ X Oturumu senkronize edilemedi", "Error");
            }
            
            if (btn != null) btn.Enabled = true;
        }

        private async Task<bool> SaveTwitterCookiesAsync()
        {
            try
            {
                var cookieManager = _webViewTwitter.CoreWebView2.CookieManager;
                var cookies = await cookieManager.GetCookiesAsync("https://x.com");
                
                var cookieList = new List<Dictionary<string, object>>();
                foreach (var c in cookies)
                {
                    cookieList.Add(new Dictionary<string, object>
                    {
                        { "name", c.Name },
                        { "value", c.Value },
                        { "domain", c.Domain },
                        { "path", c.Path },
                        { "expires", c.Expires },
                        { "secure", c.IsSecure },
                        { "httpOnly", c.IsHttpOnly }
                    });
                }

                string json = JsonSerializer.Serialize(cookieList);
                string tempFile = Path.Combine(Path.GetTempPath(), "twitter_sync.json");
                File.WriteAllText(tempFile, json);

                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "social_intel.py");
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" SetCookiesFromJson --file \"{tempFile}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string result = await process.StandardOutput.ReadToEndAsync();
                    File.Delete(tempFile);
                    return result.Contains("success");
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveTwitterCookies Error: " + ex.Message);
                return false;
            }
        }

        private async Task InjectTwitterCookiesAsync(Microsoft.Web.WebView2.Core.CoreWebView2 cookieManager)
        {
            try
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
                string cookiePkl = Path.Combine(appDataDir, "twitter_cookies.pkl");

                if (!File.Exists(cookiePkl)) return;

                string scriptPath = _opManager.DependencyManager.GetScriptPath("social_intel.py");
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" GetCookiesJson",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string json = await process.StandardOutput.ReadToEndAsync();
                    var cookies = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

                    if (cookies != null)
                    {
                        var manager = cookieManager.CookieManager;
                        foreach (var c in cookies)
                        {
                            string name = c.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                            string value = c.TryGetValue("value", out var v) ? v?.ToString() ?? "" : "";
                            string domain = c.TryGetValue("domain", out var d) ? d?.ToString() ?? ".x.com" : ".x.com";
                            string path = c.TryGetValue("path", out var p) ? p?.ToString() ?? "/" : "/";

                            if (!string.IsNullOrEmpty(name))
                            {
                                var cookie = manager.CreateCookie(name, value, domain, path);
                                manager.AddOrUpdateCookie(cookie);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Twitter Cookie Injection Failed: " + ex.Message);
            }
        }

        private async void InitializeTwitterWebView()
        {
            try
            {
                await _webViewTwitter.EnsureCoreWebView2Async();
                await InjectTwitterCookiesAsync(_webViewTwitter.CoreWebView2);
                
                Log("✅ X (Twitter) oturum çerezleri enjekte edildi", "System");
                _webViewTwitter.Source = new Uri("https://x.com/home");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Twitter WebView Init Failed: " + ex.Message);
            }
        }

        private async void InitializeServices()
        {
            try
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
                Directory.CreateDirectory(appDataDir);

                _opManager = new OperationManager();
                _opManager.OnLogAI += (msg) => this.Invoke((MethodInvoker)(() => LogAI(msg)));
                
                // v3.5.2: UI Auto-Refresh for Influencers
                _opManager.InfluencerControl.OnDatabaseChanged += () => {
                    if (!this.IsDisposed) {
                        this.Invoke(new Action(() => RefreshInfluencerListView()));
                    }
                };

                await _opManager.InitializeAllAsync(appDataDir, (msg, src) => Log(msg, src));


                // v3.8.5: Auto Model Benchmark (Startup + Daily at 03:00)
                Log("🤖 Otomatik model benchmark başlatılıyor...", "AI");
                _ = Task.Run(async () => {
                    await Task.Delay(5000); // 5 sn bekle, uygulama tam açılsın
                    await _opManager.RunAutoModelBenchmarkAsync();
                });
                _opManager.InitializeDailyBenchmarkTimer();

                _opManager.SignalEng.OnStatusUpdate += (status) => UpdateBotStatus(status);
                _opManager.SignalEng.OnLog += (msg, src) => LogSignal($"[{src}] {msg}"); // v3.9.3: Fixed signature match
                _opManager.SignalEng.OnSignalProcessed += (sig) => this.Invoke((MethodInvoker)(() => {
                    AddSignalToGrid(sig);
                    UpdateKPICards(); // Update stats live
                }));

                // Load Initial Data
                LoadSignalHistory();
                UpdateKPICards();

                _opManager.NewsEng.OnNewsProcessed += (news, summary, importance) => {
                    UpdateBotStatus($"📰 Haber Yayınlandı: {news.Title}");
                    if (_newsInitialized) AddNewsCard(news, summary, "PUBLISHED");
                };

                _opManager.NewsEng.OnNewsPendingApproval += (news, summary, score, reasoning) => {
                    int id = Interlocked.Increment(ref _newsIdCounter);
                    _pendingNewsDict[id] = (news, summary);
                    UpdateBotStatus($"⚠️ Onay Bekliyor [ID: {id}]: {news.Title}");
                    if (_newsInitialized) AddNewsCard(news, summary, "PENDING");
                    
                    string msg = $"⚠️ *ONAY BEKLİYOR [ID: {id}] ({score}/5)*\n\n" +
                                 $"📰 *{news.Title}*\n" +
                                 $"🔎 *Analiz:* {reasoning}\n" +
                                 $"📝 *Özet:* {summary}\n\n" +
                                 $"✅ Onay: `/ONAYHABER {id}`\n" +
                                 $"❌ Red: `/REDHABER {id}`";
                    _ = _opManager.Telegram.SendMessageAsync(msg);
                };

                _opManager.NewsTracker.OnNewsDetected += (news) => {
                    if (_newsInitialized) AddNewsCard(news, null, "LIVE"); 
                };

                // v3.0.7 FB FanZone UI Binding (v3.1.1: Fixed with Status column)
                _opManager.FanZone.OnNewFanContent += (fanTweet) => {
                    this.Invoke((MethodInvoker)(() => {
                        if (dgvFenerbahce != null && !this.IsDisposed)
                        {
                            // v3.1.1: Insert 5 values to match columns: Time, Source, Tweet, Reaction, Status
                            string status = "✅ Tamam";
                            if (string.IsNullOrEmpty(fanTweet.AIReaction)) status = "⚠️ Atlandı";

                            dgvFenerbahce.Rows.Insert(0, 
                                fanTweet.DetectedAt.ToString("HH:mm"), 
                                fanTweet.Handle + (fanTweet.Source == "Gündem" ? " (Gündem)" : ""), 
                                fanTweet.Text, 
                                fanTweet.AIReaction,
                                status
                            );
                            
                            if (dgvFenerbahce.Rows.Count > 100) dgvFenerbahce.Rows.RemoveAt(100);
                        }
                    }));
                };

                // Initialize UI Watchers
                _watcher = new LogFileWatcher();
                _watcher.OnLog += LogSys;
                _watcher.OnSignalDetected += OnSignalReceived;

                _opManager.Twitter.RegisterWebView(_webViewTwitter);

                // Start Operations
                _opManager.StartOperations();
                
                // Init Telegram Polling
                _telegramPollTimer = new System.Windows.Forms.Timer();
                _telegramPollTimer.Interval = 3000; // 3 sec polling
                _telegramPollTimer.Tick += async (s, e) => await ProcessTelegramCommands();
                
                // v3.1 FIX: Better startup offset handling. Instead of skipping all, fetch 20 and process them if they are new.
                Task.Run(async () => {
                    try
                    {
                        var lastUpdates = await _opManager.Telegram.GetUpdatesAsync(0); // Fetch pending
                        if (lastUpdates != null && lastUpdates.Count > 0)
                        {
                            // We don't want to process old commands from hours ago on every restart, 
                            // so we still set the offset to the latest, but we log the catch-up.
                            _lastProcessedUpdateId = lastUpdates.Max(u => u.UpdateId);
                            Logger.Telegram($"📡 Telegram polling başlatılıyor. Bekleyen {lastUpdates.Count} mesaj sisteme tanıtıldı. Son ID: {_lastProcessedUpdateId}");
                        }
                        else
                        {
                            Logger.Telegram("📡 Telegram polling başlatılıyor (Bekleyen mesaj yok).");
                        }
                    }
                    catch (Exception tex)
                    {
                        Logger.Telegram($"⚠️ Telegram başlangıç offset alma hatası: {tex.Message}");
                    }
                    finally
                    {
                        this.Invoke((MethodInvoker)(() => {
                            _telegramPollTimer.Start();
                            Logger.Telegram("✅ Telegram polling timer aktif (UI Thread).");
                        }));
                    }
                });

                // Init Main Schedule Timer (Dashboard Stats)
                _scheduleTimer = new System.Windows.Forms.Timer();
                _scheduleTimer.Interval = 1000 * 60; // Every 1 minute
                _scheduleTimer.Tick += (s, e) => CheckSchedule();
                _scheduleTimer.Start();

                // Initial Profile Stats Catch-up
                _ = UpdateProfileStats();

                Log("🚀 XiDeAI Pro: Tüm sistemler nominal. (OperationManager Active)", "System");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Servis başlatma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void LoadSettings()
        {
            // Load permanently commented tweets (not reset daily)
            LoadCommentedTweets();
            
            var cfg = ConfigManager.Current;
            txtApiKey.Text = cfg.TwitterApiKey ?? "";
            txtApiSecret.Text = cfg.TwitterApiSecret ?? "";
            txtAccessToken.Text = cfg.TwitterAccessToken ?? "";
            txtTokenSecret.Text = cfg.TwitterTokenSecret ?? "";
            
            // Load Gemini Key with logging for debugging
            string geminiKey = cfg.GeminiApiKey ?? "";
            txtGeminiKey.Text = geminiKey;
            if (!string.IsNullOrEmpty(geminiKey))
            {
                Log($"✅ Gemini API Key yüklendi (uzunluk: {geminiKey.Length} karakter)", "System");
            }
            else
            {
                Log("⚠️ Gemini API Key bulunamadı - lütfen Ayarlar'dan giriniz", "System");
            }
            
            // Load Perplexity Key (v3.1+)
            txtGeminiKey.Text = cfg.GeminiApiKey;
            txtPerplexityKey.Text = cfg.PerplexityApiKey;
            
            // v3.1.1: Force Update Telegram Token in Service after loading
            _opManager.Telegram.UpdateConfig();
            Logger.Telegram("🔄 Telegram Ayarları Senkronize Edildi.");
            if (!string.IsNullOrEmpty(cfg.PerplexityApiKey))
            {
                Log($"✅ Perplexity API Key yüklendi (uzunluk: {cfg.PerplexityApiKey.Length} karakter)", "System");
            }
            
            // Load Gemini Model
            if (!string.IsNullOrEmpty(cfg.GeminiModel))
            {
                if (!cmbGeminiModel.Items.Contains(cfg.GeminiModel))
                    cmbGeminiModel.Items.Add(cfg.GeminiModel);
                cmbGeminiModel.SelectedItem = cfg.GeminiModel;
            }
            txtTvChartId.Text = cfg.TradingViewChartId;
            txtTvSymbol.Text = cfg.TradingViewSymbol;

            chkAuto.Checked = cfg.AutoTweet;
            
            // Filter settings
            chkKing.Checked = cfg.EnableKing;
            chkBomba.Checked = cfg.EnableBomba;
            chkTeFo.Checked = cfg.EnableTeFo;
            chkDip.Checked = cfg.EnableDip;
            chkZirve.Checked = cfg.EnableZirve;
            chkAnka.Checked = cfg.EnableAnka;
            chkPer15.Checked = cfg.Period15;
            chkPer60.Checked = cfg.Period60;
            chkPer240.Checked = cfg.Period240;
            chkPerG.Checked = cfg.PeriodDaily;
            chkOnlyCommon.Checked = cfg.OnlyCommonSignals;
            
            // Load Common Strategies
            cfg.CommonStrategies ??= new List<string>();
            var com = cfg.CommonStrategies;
            chkComKing.Checked = com.Contains("KING");
            chkComBomba.Checked = com.Contains("BOMBA");
            chkComTeFo.Checked = com.Contains("TEFO");
            chkComDip.Checked = com.Contains("DIP");
            chkComZirve.Checked = com.Contains("ZIRVE");
            if (chkComAnka.Checked && !cfg.CommonStrategies.Contains("ANKA")) cfg.CommonStrategies.Add("ANKA");
            
            // chkRespectSchedule.Checked = cfg.RespectSchedule;
            numThresholdKing.Value = cfg.MinScoreKing;
            numThresholdDip.Value = cfg.MinScoreDip;
            numThresholdAnka.Value = cfg.MinScoreAnka;
            // Miner has fixed threshold in robot code (60), no UI needed
            chkMiner.Checked = cfg.EnableMiner;
            
            txtTelToken.Text = cfg.TelegramBotToken;
            txtTelChatId.Text = cfg.TelegramChatId;

            // numDelay.Value = cfg.TweetDelayMinutes;
            
            txtScanHours.Text = string.Join(", ", cfg.ScanHours);

            UpdateQuotaDisplay();

            // Load Spam Protection per-module
            chkSpamSignals.Checked = cfg.SpamProtectSignals;
            chkSpamBatches.Checked = cfg.SpamProtectBatches;
            chkSpamManual.Checked = cfg.SpamProtectManual;
            chkSpamNews.Checked = cfg.SpamProtectNews;
            chkSpamReports.Checked = cfg.SpamProtectReports;
            chkSpamMotivation.Checked = cfg.SpamProtectMotivation;
            
            // Load Bot Interaction Settings
            chkBotEnabled.Checked = cfg.BotInteractionEnabled;
            txtBotKeywords.Text = cfg.BotTopicKeywords;
            if (txtBotMinFollowers != null) txtBotMinFollowers.Text = cfg.BotMinFollowers.ToString();
            if (txtBotMinFavorites != null) txtBotMinFavorites.Text = cfg.BotMinFavorites.ToString();
            if (txtBotMaxAge != null) txtBotMaxAge.Text = cfg.BotMaxTweetAgeHours.ToString();

            // v3.7.2: Sync providers with loaded keys
            try 
            {
                if (_opManager != null) _opManager.SyncGeminiProviders();
            }
            catch (Exception ex) { Debug.WriteLine($"SyncGeminiProviders Error: {ex.Message}"); }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var cfg = ConfigManager.Current;
            cfg.TwitterApiKey = txtApiKey.Text.Trim();
            cfg.TwitterApiSecret = txtApiSecret.Text.Trim();
            cfg.TwitterAccessToken = txtAccessToken.Text.Trim();
            cfg.TwitterTokenSecret = txtTokenSecret.Text.Trim();
            
            // Gemini API Key - Trim whitespace and validate
            string newGeminiKey = txtGeminiKey.Text.Trim();
            if (!string.IsNullOrEmpty(newGeminiKey) && newGeminiKey != cfg.GeminiApiKey)
            {
                Log($"🔑 Gemini API Key güncelleniyor... (uzunluk: {newGeminiKey.Length} karakter)", "System");
            }
            cfg.GeminiApiKey = newGeminiKey;
            
            // Perplexity API Key (v3.1+)
            string newPerplexityKey = txtPerplexityKey.Text.Trim();
            if (!string.IsNullOrEmpty(newPerplexityKey) && newPerplexityKey != cfg.PerplexityApiKey)
            {
                Log($"🔑 Perplexity API Key güncelleniyor... (uzunluk: {newPerplexityKey.Length} karakter)", "System");
            }
            cfg.PerplexityApiKey = newPerplexityKey;
            
            cfg.GeminiModel = cmbGeminiModel.SelectedItem?.ToString() ?? "gemini-2.0-flash-exp";
            cfg.TelegramBotToken = txtTelToken.Text.Trim();
            cfg.TelegramChatId = txtTelChatId.Text.Trim();
            cfg.TradingViewChartId = txtTvChartId.Text.Trim();
            cfg.TradingViewSymbol = txtTvSymbol.Text.Trim();

            cfg.AutoTweet = chkAuto.Checked;
            
            // Filters
            cfg.EnableKing = chkKing.Checked;
            cfg.EnableBomba = chkBomba.Checked;
            cfg.EnableTeFo = chkTeFo.Checked;
            cfg.EnableDip = chkDip.Checked;
            cfg.EnableZirve = chkZirve.Checked;
            cfg.EnableAnka = chkAnka.Checked;
            cfg.EnableMiner = chkMiner.Checked;
            cfg.Period15 = chkPer15.Checked;
            cfg.Period60 = chkPer60.Checked;
            cfg.Period240 = chkPer240.Checked;
            cfg.PeriodDaily = chkPerG.Checked;
            cfg.OnlyCommonSignals = chkOnlyCommon.Checked;
            
            // Save Common Strategies
            cfg.CommonStrategies = new List<string>();
            if (chkComKing.Checked) cfg.CommonStrategies.Add("KING");
            if (chkComBomba.Checked) cfg.CommonStrategies.Add("BOMBA");
            if (chkComTeFo.Checked) cfg.CommonStrategies.Add("TEFO");
            if (chkComDip.Checked) cfg.CommonStrategies.Add("DIP");
            if (chkComZirve.Checked) cfg.CommonStrategies.Add("ZIRVE");
            if (chkComAnka.Checked) cfg.CommonStrategies.Add("ANKA");
            
            // cfg.RespectSchedule = chkRespectSchedule.Checked;
            cfg.MinScoreKing = (int)numThresholdKing.Value;
            cfg.MinScoreDip = (int)numThresholdDip.Value;
            cfg.MinScoreAnka = (int)numThresholdAnka.Value;
            // cfg.TweetDelayMinutes = (int)numDelay.Value;
            
            // Scan Hours Parsing
            var hours = txtScanHours.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(h => h.Trim())
                                         .Where(h => !string.IsNullOrEmpty(h))
                                         .ToList();
            cfg.ScanHours = hours;

            // Save Spam Protection per-module
            cfg.SpamProtectSignals = chkSpamSignals.Checked;
            cfg.SpamProtectBatches = chkSpamBatches.Checked;
            cfg.SpamProtectManual = chkSpamManual.Checked;
            cfg.SpamProtectNews = chkSpamNews.Checked;
            cfg.SpamProtectReports = chkSpamReports.Checked;
            cfg.SpamProtectMotivation = chkSpamMotivation.Checked;
            
            // Save Target Accounts
            cfg.TargetAccounts = txtTargetAccounts?.Text?.Trim() ?? "";

            ConfigManager.Save();
            Log("✅ Ayarlar kaydedildi.", "System");
            
            // v3.7.2: Update AI providers with new keys immediately
            _opManager?.SyncGeminiProviders();

            // Show confirmation message box
            MessageBox.Show("✅ Ayarlar başarıyla kaydedildi!\n\nGemini API Key aktif edildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            var cfg = ConfigManager.Current;
            _watcher.Start(new[] { cfg.WatchFolderKing, cfg.WatchFolderDip, cfg.WatchFolderAnka, cfg.WatchFolderMiner });
            // _scheduleTimer (removed).Start();
            
            // v3.0 Deep Scan - MOVED TO INFLUENCER PANEL
            /*
            if (cfg.AutoTweet) 
            {
               // _deepScanTimer (removed).Start();
               Log("🧠 Hafıza Taraması (Deep Scan) Aktif.", "System");
            }
            */

            Log("🚀 Watcher Service Started...");
            Log($"📁 Watching: {cfg.WatchFolderKing}, {cfg.WatchFolderDip}, {cfg.WatchFolderAnka}, {cfg.WatchFolderMiner}");
            btnStart.Enabled = false;
            btnStart.Text = "⏳ Running...";
            btnStart.BackColor = Color.Gray;
            // Enable Stop and Pause buttons
            btnStopWatcher.Enabled = true;
            btnPauseWatcher.Enabled = true;
            btnPauseWatcher.Text = "⏸️ Pause";
            UpdateStatus("Watching...");
        }

        private void BtnStopWatcher_Click(object? sender, EventArgs e)
        {
            _watcher.Stop();
            // _scheduleTimer (removed).Stop();
            // // _deepScanTimer (removed).Stop(); // MOVED TO INFLUENCER PANEL
            Log("⏹️ Watcher Service Stopped.");
            btnStart.Enabled = true;
            btnStart.Text = "▶ Start";
            btnStart.BackColor = Color.FromArgb(0, 120, 212);
            btnStopWatcher.Enabled = false;
            btnPauseWatcher.Enabled = false;
            UpdateStatus("Stopped");
        }

        private void BtnPauseWatcher_Click(object? sender, EventArgs e)
        {
            if (_watcher.IsPaused)
            {
                _watcher.Resume();
                Log("▶️ Watcher Service Resumed.");
                btnPauseWatcher.Text = "⏸️ Pause";
                UpdateStatus("Watching...");
            }
            else
            {
                _watcher.Pause();
                Log("⏸️ Watcher Service Paused.");
                btnPauseWatcher.Text = "▶️ Resume";
                UpdateStatus("Paused");
            }
        }



        private async Task PostMarketCloseSummary()
        {
            if (ConfigManager.Current.SpamProtectReports && !_opManager.Spam.CanPostGeneral(out string reason))
            {
                Log($"🛡️ Spam protection (Kapanış Özeti): {reason}", "Twitter");
                return;
            }
            Log("🌆 Posting Market Close Summary with Tables...", "Twitter");
            
            try
            {
                // 1. Fetch Indices Data
                var financials = await _opManager.SocialIntel.GetFinancialSummaryAsync();
                string indicesData = "";
                
                if (financials != null && financials.Count > 0)
                {
                    var xu100 = financials.GetValueOrDefault("XU100", "?");
                    var gold = financials.GetValueOrDefault("GramAltin", "?");
                    var usd = financials.GetValueOrDefault("USD", "?");
                    
                    indicesData = $"XU100: {xu100}, ALTIN: {gold}, USD: {usd}";
                    Log($"📊 Endeksler çekildi: {indicesData}", "Twitter");
                }
                else
                {
                    indicesData = "Sembol | Fiyat\n-------|------\nBIST100| 9.850\nALTIN  | 2.850\nUSD    | 34.50";
                }

                // 2. Fetch Winners/Losers
                var gainers = await _opManager.SocialIntel.GetTopGainersAsync();
                var losers = await _opManager.SocialIntel.GetTopLosersAsync();
                var topVolume = await _opManager.SocialIntel.GetTopVolumeAsync();

                string topGainersData = BuildStockTable(gainers, "Top Gainers");
                string topLosersData = BuildStockTable(losers, "Top Losers");
                string topVolumeData = BuildStockTable(topVolume, "Top Volume");

                // 3. AI Generation
                Log("🤖 AI Piyasa Kapanış Özeti Hazırlanıyor...", "System");
                string? tweetSet = await _opManager.Gemini.GenerateMarketCloseTableTweet(indicesData, topGainersData, topLosersData, topVolumeData);

                if (string.IsNullOrEmpty(tweetSet)) return;

                var tweets = tweetSet.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                int tweetCount = 0;
                foreach (var t in tweets)
                {
                    tweetCount++;
                    string cleanedTweet = t.Trim();
                    
                    if (ConfigManager.Current.AutoTweet)
                    {
                        var webResult = await _opManager.SocialIntel.PostTweet(cleanedTweet);
                        if (webResult.status == "success")
                        {
                            Log($"✅ Market Close Tweet {tweetCount} sent (WebView)!", "Twitter");
                            _opManager.Spam.RecordTweet("REPORT", "CLOSE");
                        }
                    }
                    else
                    {
                        var res = await _opManager.Twitter.SendTweetAsync(cleanedTweet);
                        if (!string.IsNullOrEmpty(res))
                        {
                            Log($"✅ Market Close Tweet {tweetCount} sent (API)!", "Twitter");
                            _opManager.Spam.RecordTweet("REPORT", "CLOSE"); 
                        }
                        else
                        {
                            Log($"❌ Market Close Tweet {tweetCount} failed: {_opManager.Twitter.LastError}", "Twitter"); 
                        }
                    }
                    await Task.Delay(3000);
                }

                Log($"✅ Market Close Summary completed ({tweetCount} tweets sent)!", "Twitter");
            }
            catch (Exception ex)
            {
                Log($"❌ Market Close Summary Error: {ex.Message}", "Twitter");
            }
        }

        /// <summary>
        /// Helper method to build stock data table for tweet
        /// </summary>
        private string BuildStockTable(List<StockData> stocks, string title)
        {
            if (stocks == null || stocks.Count == 0)
            {
                return $"{title}:\nVeri yok";
            }

            var sb = new StringBuilder();
            
            // Determine column widths (compact format, no wrapping)
            // Format: | Symbol | Fiyat   | +/- % |  (total ~35 chars max per line)
            
            sb.AppendLine(title);
            sb.AppendLine("| Hisse | Fiyat | %   |");
            sb.AppendLine("|-------|-------|-----|");
            
            // Take top 5 for compact display (fit in tweet)
            int count = Math.Min(stocks.Count, 5);
            for (int i = 0; i < count; i++)
            {
                var stock = stocks[i];
                
                // Format: Symbol (max 6 chars), Price (compact), Change (max 5 chars)
                string symbol = stock.Symbol.Length > 6 ? stock.Symbol.Substring(0, 6) : stock.Symbol;
                string price = stock.Close >= 1000 ? (stock.Close / 1000).ToString("F1") + "K" : stock.Close.ToString("F2");
                string changeStr = stock.ChangePercent >= 0 
                    ? $"+{stock.ChangePercent:F1}" 
                    : $"{stock.ChangePercent:F1}";
                
                sb.AppendLine($"|{symbol,7}|{price,7}|{changeStr,5}|");
            }

            return sb.ToString();
        }

        private async Task PostMorningMotivation()
        {
            try 
            {
                // 1. Spam/Quota Check
                if (ConfigManager.Current.SpamProtectMotivation && !_opManager.Spam.CanPostGeneral(out string reason))
                {
                    Log($"🛡️ Spam protection (Motivation): {reason}", "Twitter");
                    return;
                }

                // 2. Prevent Double Posting (Using a transient flag for session + checking last post time if persisted)
                // Since this runs in a range (09:00-10:00), we must ensure we strictly run ONCE per day.
                // Best simple way: Check if we already tweeted 'MOTIVATION' today via SpamService cache or local set.
                // For robustness, we use the local set _tweetedToday which is reset at midnight.
                if (_tweetedToday.Contains("MOTIVATION")) return;

                Log("☀️ Posting Morning Motivation...", "Twitter");
                UpdateBotStatus("📊 Günlük Motivasyon Paylaşılıyor...");
                
                string tweet = await _opManager.Gemini.GenerateMotivationTweet();
                
                // Fallback if AI fails (Safety Net)
                if (string.IsNullOrEmpty(tweet))
                {
                    tweet = "☀️ Günaydın!\n\n\"Başarı, son değil; başarısızlık, ölümcül değil. Önemli olan devam etme cesareti.\"\n- Winston Churchill\n\n💡 Her düşüş yeni bir yükseliş fırsatıdır. Vazgeçmeyin!\n\n#Motivasyon #Finans #Bist100";
                }

                // 3. Post (WebView or API)
                if (ConfigManager.Current.AutoTweet)
                {
                    var webResult = await _opManager.SocialIntel.PostTweet(tweet);
                    if (webResult.status == "success")
                    {
                        Log("✅ Motivation tweet sent (WebView)!", "Twitter");
                        _opManager.Spam.RecordTweet("MOTIVATION", "MOTIVATION");
                        _tweetedToday.Add("MOTIVATION"); // Mark as done checking
                        if (ConfigManager.Current.TelegramBotToken != "") _ = _opManager.Telegram.SendMessageAsync($"☀️ *GÜNAYDIN!* Motivasyon tweeti atıldı.");
                    }
                }
                else
                {
                    // API Fallback
                    string sentLink = await _opManager.Twitter.SendTweetAsync(tweet);
                    if (!string.IsNullOrEmpty(sentLink)) 
                    { 
                        Log("✅ Motivation tweet sent (API)!", "Twitter"); 
                        _opManager.Spam.RecordTweet("MOTIVATION", "MOTIVATION"); 
                        _tweetedToday.Add("MOTIVATION");

                         if (ConfigManager.Current.TelegramBotToken != "") _ = _opManager.Telegram.SendMessageAsync($"☀️ *GÜNAYDIN!* Motivasyon tweeti atıldı.");
                    }
                    else Log($"❌ Motivation tweet failed: {_opManager.Twitter.LastError}", "Twitter");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Motivation Error: {ex.Message}", "System");
            }
            finally
            {
                UpdateBotStatus("IDLE");
            }
        }

        private void CheckSchedule()
        {
            var now = DateTime.Now;
            
            // 1. Reset check (Midnight)
            if (now.Hour == 0 && now.Minute == 0)
            {
                ConfigManager.Current.CheckReset();
                _tweetedToday.Clear();
                lock(_memoryLock) { _signalMemory.Clear(); }
                Log("🔄 Daily tweet/news log cleared.");
            }

            ConfigManager.Current.CheckReset(); // Double check for safety
            UpdateQuotaDisplay();
            _opManager.Screenshot.CleanupOldScreenshots();

            // Periodic Stats Update (Every 1 hour)
            if ((now - _lastStatsUpdate).TotalHours >= 1)
            {
                _lastStatsUpdate = now;
                 _ = UpdateProfileStats();
            }

            // --- SCHEDULED EVENTS ---
            
            // A. Morning Motivation (09:00 - 10:00 range)
            // Fix: Check range instead of exact minute to avoid skipping if app was busy/restarting
            if (now.Hour == 9 && !_tweetedToday.Contains("MOTIVATION"))
            {
                // Allow a random delay within the hour or just fire immediately if caught up
                // Here we fire immediately if we are in the 9th hour and haven't posted yet.
                 _ = PostMorningMotivation();
            }

            // B. Market Close Summary (18:15 - 18:45 range)
            // BIST closes at 18:10. We check after 18:15.
            if (now.Hour == 18 && now.Minute >= 15 && !_tweetedToday.Contains("CLOSE_REPORT"))
            {
                 _tweetedToday.Add("CLOSE_REPORT"); // Mark immediately to prevent double fire

                 
                 // CRITICAL FIX: Update Signal Closing Prices before Report
                 Task.Run(async () => 
                 {
                     try 
                     {
                         var signals = _opManager.Performance.GetDailyReport(DateTime.Now).Signals;
                         if (signals.Count > 0)
                         {
                             Log($"🔍 Closing Signals: {signals.Count} signals tracked for PnL...", "System");
                             var symbols = signals.Select(s => s.Symbol).Distinct();
                             
                             var tasks = symbols.Select(async sym => 
                             {
                                 string mType = "BIST";
                                 if (sym.EndsWith("USDT") || sym.Length > 5) mType = "Kripto";
                                 var pInfo = await _opManager.PriceFetch.GetPriceAsync(sym, mType);
                                 return (Symbol: sym, Price: pInfo?.Price ?? 0);
                             });

                             var results = await Task.WhenAll(tasks);
                             var prices = results.Where(r => r.Price > 0).ToDictionary(r => r.Symbol, r => r.Price);
                             
                             _opManager.Performance.UpdateClosingPrices(prices);
                             Log($"✅ PnL Updated: {prices.Count} prices captured.", "System");
                         }
                     }
                     catch (Exception ex) { Log($"⚠️ PnL Update Failed: {ex.Message}", "System"); }

                     // Proceed to Summary
                     _ = PostMarketCloseSummary();
                 });
            }
        }

        private async Task UpdateProfileStats()
        {
            UpdateBotStatus("📊 Profil istatistikleri güncelleniyor...");
            try 
            {
                var stats = await _opManager.SocialIntel.GetProfileStatsAsync();
                if (stats != null)
                {
                    int fCount = 0;
                    string raw = stats.followers.ToUpper().Replace(",", "").Replace(".", "").Trim();
                    
                    if (raw.EndsWith("K")) {
                        string val = raw.Replace("K", "").Trim();
                        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d)) 
                            fCount = (int)(d * 1000);
                    }
                    else if (raw.EndsWith("M")) {
                        string val = raw.Replace("M", "").Trim();
                        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d)) 
                            fCount = (int)(d * 1000000);
                    }
                    else {
                        int.TryParse(raw, out fCount);
                    }

                    if (fCount > 0)
                    {
                        ConfigManager.Current.FollowersCount = fCount;
                        ConfigManager.Save();
                        this.Invoke(new Action(UpdateQuotaDisplay));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Twitter($"⚠️ Profil istatistik güncelleme hatası: {ex.Message}");
            }
            UpdateBotStatus("IDLE");
        }
        
        private async Task RefreshTrendsAsync()
        {
            Log("🔍 Sinyal havuzundan ve X'ten Trendler oluşturuluyor...");
            
            var activeTags = new HashSet<string>();
            
            // 0. Get Real X Trends (Filtered)
            var realTrends = await _opManager.SocialIntel.GetTrendsAsync();
            var financeKeywords = new[] { "BIST", "BORSA", "XU100", "HISSE", "DOLAR", "ALTIN", "EKONOMI", "FED", "TCMB", "FAIZ", "ENFLASYON", "VIOP", "BANKA" };
            
            foreach(var t in realTrends) 
            {
                // Simple filter: Check if trend contains any finance keyword or looks like a ticker (e.g. #THYAO)
                string upperT = t.ToUpper();
                bool isFinance = financeKeywords.Any(k => upperT.Contains(k));
                // Stricter ticker check: Starts with # and followed by 3-6 uppercase letters only
                bool isTicker = t.StartsWith("#") && System.Text.RegularExpressions.Regex.IsMatch(t.Substring(1), "^[A-Z0-9]{3,7}$");
                
                if (isFinance || isTicker)
                {
                    activeTags.Add(t);
                }
            }

            // 1. Get Live Signals
            lock(_memoryLock)
            {
                foreach(var symbol in _signalMemory)
                {
                    activeTags.Add($"#{symbol}");
                }
            }

            // 2. Add General Market Tags (Defaults)
            var defaults = new[] { "#BIST100", "#Borsa", "#XU100", "#Hisse", "#Altın", "#Dolar" };
            foreach(var d in defaults) activeTags.Add(d);
            
            // 3. Select subset
            var finalList = activeTags.Take(8).ToList(); 

            // Update UI
            txtTrends.Text = string.Join(" ", finalList);
            ConfigManager.Current.DailyTrends = txtTrends.Text;
            Log($"✅ Trendler güncellendi: {txtTrends.Text}");

            await Task.CompletedTask; 
        }

        #region Dahili X Otomasyon Köprüsü (WebView2 Bridge)

        private void InitializeInternalXBridge()
        {
            if (_opManager.SocialIntel == null) return;
            
            _opManager.SocialIntel.OnPostTweetRequested += (text, media) => PerformInternalPostAsync(text, media);
            _opManager.SocialIntel.OnPostThreadRequested += (tweets, media) => PerformInternalThreadAsync(tweets, media);
            _opManager.SocialIntel.OnSearchRequested += (symbol, query, market, handles) => PerformInternalSearchAsync(symbol, query, market, handles);
            _opManager.SocialIntel.OnGetStatsRequested += () => FetchInternalProfileStatsAsync();
            _opManager.SocialIntel.OnGetTrendsRequested += () => FetchInternalTrendsAsync();
            _opManager.SocialIntel.OnReplyRequested += (url, text) => PerformInternalReplyAsync(url, text);
            _opManager.SocialIntel.OnInteractTargetsRequested += (users) => PerformInternalInteractionAsync(users);
            
            Log("🔗 Internal Automation Bridge initialized (WebView2 Helper).", "System");
        }

        private async Task InitializeBackgroundWebView()
        {
            try
            {
                await _webViewTwitterBg.EnsureCoreWebView2Async();
                
                // Arkaplan WebView için de çerezleri enjekte et
                await InjectTwitterCookiesAsync(_webViewTwitterBg.CoreWebView2);
                
                // CoreWebView2 will use default profile usually, but injection ensures it's fresh.
                _webViewTwitterBg.Source = new Uri("https://x.com/");
                Log("🌐 Arkaplan X servisi hazırlandı.", "System");
            }
            catch (Exception ex)
            {
                Log($"⚠️ Background WebView Init Error: {ex.Message}", "System");
            }
        }

        private async Task<SocialIntelResult> PerformInternalPostAsync(string text, string? mediaPath)
        {
            try
            {
                LogAI("🐦 Dahili X ekranı üzerinden paylaşım başlatılıyor...");
                
                // Bring Twitter tab to front
                if (tabDashViews != null)
                {
                    this.Invoke(new Action(() => {
                         foreach(TabPage tp in tabDashViews.TabPages)
                         {
                             if (tp.Controls.Contains(_webViewTwitter)) {
                                 tabDashViews.SelectedTab = tp;
                                 break;
                             }
                         }
                    }));
                }

                // 1. Media handling (External to JS)
                bool hasMedia = !string.IsNullOrEmpty(mediaPath) && File.Exists(mediaPath);
                if (hasMedia)
                {
                    this.Invoke(new Action(() => {
                        try {
                            using var img = Image.FromFile(mediaPath!);
                            Clipboard.SetImage(img);
                        } catch(Exception ex) { LogAI($"⚠️ Medya panoya kopyalanamadı: {ex.Message}"); }
                    }));
                }

                // 2. JS Automation for opening and text
                string js = $@"
                    (async () => {{
                        const wait = (ms) => new Promise(r => setTimeout(r, ms));
                        
                        // Open Composer
                        let btn = document.querySelector('[data-testid=""SideNav_NewTweet_Button""]') || 
                                  document.querySelector('[aria-label=""Post""]') ||
                                  document.querySelector('[aria-label=""Gönder""]');
                        if (btn) btn.click();
                        await wait(1500);

                        // Set Text
                        let textarea = document.querySelector('[data-testid=""tweetTextarea_0""]') || 
                                       document.querySelector('.public-DraftEditor-content');
                        
                        if (textarea) {{
                            textarea.focus();
                            document.execCommand('insertText', false, `{text.Replace("`", "\\`")}`);
                            await wait(500);
                            return {{ status: 'success' }};
                        }}
                        return {{ status: 'error', message: 'Tweet button not found' }};
                    }})();
                ";

                var resultJsonTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(js)));
                var resultJson = await resultJsonTask;
                using var doc = JsonDocument.Parse(resultJson);
                if (doc.RootElement.GetProperty("status").GetString() != "success")
                {
                    return new SocialIntelResult { status = "error", message = "JS Text Error" };
                }

                // 3. Paste Media if exists
                if (hasMedia)
                {
                    await Task.Delay(500);
                    this.Invoke(new Action(() => {
                        _webViewTwitter.Focus();
                        SendKeys.SendWait("^v");
                    }));
                    await Task.Delay(3000); // Wait for upload
                }

                // 4. Send
                string sendJs = @"
                    (() => {
                        let postBtn = document.querySelector('[data-testid=""tweetButton""]') || 
                                     document.querySelector('[data-testid=""tweetButtonInline""]');
                        if (postBtn && !postBtn.disabled) {
                            postBtn.click();
                            return true;
                        }
                        return false;
                    })();
                ";
                var sentTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(sendJs)));
                var sent = await sentTask;
                
                if (sent == "true")
                    return new SocialIntelResult { status = "success", message = "Dahili olarak paylaşıldı." };
                else
                    return new SocialIntelResult { status = "error", message = "Gönder butonu bulunamadı veya pasif." };
            }
            catch (Exception ex)
            {
                return new SocialIntelResult { status = "error", message = $"JS Bridge Error: {ex.Message}" };
            }
        }

        private async Task<SocialIntelResult> PerformInternalThreadAsync(List<string> tweets, string? mediaPath)
        {
            try
            {
                LogAI($"🧵 Dahili X ekranı üzerinden {tweets.Count} parçalık zincir başlatılıyor (v2 Engine)...");

                bool hasMedia = !string.IsNullOrEmpty(mediaPath) && File.Exists(mediaPath);
                
                // 0. FIRST: Dismiss any existing draft/confirmation dialogs
                LogAI("   > Varsa eski taslak dialogları kapatılıyor...");
                var dismissTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                    (function() {
                        // Look for 'Vazgeç' (Cancel) or 'Discard' buttons in confirmation dialogs
                        var buttons = Array.from(document.querySelectorAll('button, [role=""button""]'));
                        var dismissBtn = buttons.find(b => {
                            var text = (b.innerText || b.textContent || '').toLowerCase().trim();
                            return text === 'vazgeç' || text === 'discard' || text === 'iptal' || 
                                   text === 'cancel' || text === 'kapat';
                        });
                        if (dismissBtn) {
                            dismissBtn.click();
                            return 'dismissed';
                        }
                        
                        // Also check for X close buttons on modals
                        var closeBtn = document.querySelector('[data-testid=""app-bar-close""]') ||
                                       document.querySelector('[aria-label=""Kapat""]') ||
                                       document.querySelector('[aria-label=""Close""]');
                        if (closeBtn) {
                            closeBtn.click();
                            return 'closed_modal';
                        }
                        
                        return 'no_dialog';
                    })();
                ")));
                var dismissRes = await dismissTask;
                LogAI($"   > Dialog durumu: {dismissRes}");
                
                if (dismissRes != null && (dismissRes.Contains("dismissed") || dismissRes.Contains("closed")))
                {
                    await Task.Delay(1000); // Wait for dialog to close
                    
                    // Check again for nested dialogs
                    var dismiss2Task = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                        (function() {
                            var buttons = Array.from(document.querySelectorAll('button, [role=""button""]'));
                            var dismissBtn = buttons.find(b => {
                                var text = (b.innerText || b.textContent || '').toLowerCase().trim();
                                return text === 'vazgeç' || text === 'discard';
                            });
                            if (dismissBtn) { dismissBtn.click(); return 'dismissed_again'; }
                            return 'clear';
                        })();
                    ")));
                    await dismiss2Task;
                    await Task.Delay(500);
                }
                
                // 1. Navigate to X home and open composer
                LogAI("   > X ana sayfasına gidiliyor ve composer açılıyor...");
                var navTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                    (async function() {
                        const wait = (ms) => new Promise(r => setTimeout(r, ms));
                        
                        // Ensure we're on x.com
                        if (!window.location.href.includes('x.com')) {
                            window.location.href = 'https://x.com/home';
                            return 'navigating';
                        }
                        
                        // Click compose button
                        var btn = document.querySelector('[data-testid=""SideNav_NewTweet_Button""]') || 
                                  document.querySelector('[aria-label=""Post""]') ||
                                  document.querySelector('[aria-label=""Gönder""]') ||
                                  document.querySelector('a[href=""/compose/tweet""]');
                        if (btn) {
                            btn.click();
                            return 'opened';
                        }
                        return 'btn_not_found';
                    })();
                ")));
                var navRes = await navTask;
                LogAI($"   > Composer durumu: {navRes}");
                
                if (navRes != null && navRes.Contains("navigating"))
                {
                    await Task.Delay(3000);
                    // Retry composer open
                    var retryTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                        (function() {
                            var btn = document.querySelector('[data-testid=""SideNav_NewTweet_Button""]');
                            if (btn) { btn.click(); return 'opened'; }
                            return 'btn_not_found';
                        })();
                    ")));
                    await retryTask;
                }
                
                // Wait for modal to fully load
                await Task.Delay(3000);
                
                // 2. Wait for tweet box to appear (with retry)
                LogAI("   > Tweet kutusu bekleniyor...");
                bool boxFound = false;
                for (int wait = 0; wait < 10; wait++)
                {
                    var checkTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                        (function() {
                            var modal = document.querySelector('[role=""dialog""]');
                            if (!modal) return 'no_modal';
                            var box = modal.querySelector('[data-testid=""tweetTextarea_0""]') ||
                                      modal.querySelector('[role=""textbox""]') ||
                                      modal.querySelector('.public-DraftEditor-content') ||
                                      modal.querySelector('[contenteditable=""true""]');
                            return box ? 'found' : 'no_box';
                        })();
                    ")));
                    var checkRes = await checkTask;
                    LogAI($"   > Kutu kontrolü ({wait+1}/10): {checkRes}");
                    if (checkRes != null && checkRes.Contains("found"))
                    {
                        boxFound = true;
                        break;
                    }
                    await Task.Delay(500);
                }
                
                if (!boxFound)
                {
                    return new SocialIntelResult { status = "error", message = "Tweet box not found after 10 attempts" };
                }

                // 3. Handle media first (if exists)
                if (hasMedia)
                {
                    LogAI("   > Görsel panoya alınıp yapıştırılıyor...");
                    this.Invoke(new Action(() => {
                        try {
                            using var img = Image.FromFile(mediaPath!);
                            Clipboard.SetImage(img);
                        } catch(Exception ex) { LogAI($"   ⚠️ Clipboard hatası: {ex.Message}"); }
                    }));
                    await Task.Delay(1000);
                    
                    // Focus first box and paste image
                    var focusImgTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                        (function() {
                            var modal = document.querySelector('[role=""dialog""]');
                            var box = modal ? (modal.querySelector('[data-testid=""tweetTextarea_0""]') || 
                                              modal.querySelector('[role=""textbox""]')) : null;
                            if (box) { box.focus(); return 'focused'; }
                            return 'not_found';
                        })();
                    ")));
                    await focusImgTask;
                    await Task.Delay(300);
                    
                    this.Invoke(new Action(() => {
                        _webViewTwitter.Focus();
                        SendKeys.SendWait("^v");
                    }));
                    await Task.Delay(4000); // Wait for image upload
                }

                // 4. Type each tweet - SELENIUM-STYLE with execCommand insertText
                for (int i = 0; i < tweets.Count; i++)
                {
                    LogAI($"   > Tweet {i + 1}/{tweets.Count} yazılıyor ({tweets[i].Length} karakter)...");
                    
                    bool typeSuccess = false;
                    for (int attempt = 1; attempt <= 5; attempt++)
                    {
                        // Escape the tweet text for JavaScript
                        string escapedText = tweets[i]
                            .Replace("\\", "\\\\")
                            .Replace("\"", "\\\"")
                            .Replace("\n", "\\n")
                            .Replace("\r", "")
                            .Replace("`", "\\`");
                        
                        // SELENIUM-STYLE: Use execCommand insertText (not clipboard!)
                        string typeJs = $@"
                            (async function() {{
                                const wait = (ms) => new Promise(r => setTimeout(r, ms));
                                var modal = document.querySelector('[role=""dialog""]');
                                if (!modal) return 'no_modal';
                                
                                // Get all visible textboxes
                                var boxes = Array.from(modal.querySelectorAll(
                                    '[data-testid^=""tweetTextarea_""], [role=""textbox""], .public-DraftEditor-content'
                                )).filter(el => el.offsetParent !== null && el.offsetHeight > 0);
                                
                                if (boxes.length <= {i}) return 'box_' + boxes.length + '_need_' + {i};
                                
                                var target = boxes[{i}];
                                target.scrollIntoView({{behavior: 'instant', block: 'center'}});
                                await wait(200);
                                
                                // ATOMIC CLEAR (like Selenium)
                                target.focus();
                                target.innerText = '';
                                target.textContent = '';
                                document.execCommand('selectAll', false, null);
                                document.execCommand('delete', false, null);
                                await wait(100);
                                
                                // SELENIUM-STYLE INSERT using insertText
                                var text = ""{escapedText}"";
                                document.execCommand('insertText', false, text);
                                
                                // Dispatch events to trigger React state update
                                target.dispatchEvent(new Event('focus', {{ bubbles: true }}));
                                target.dispatchEvent(new Event('beforeinput', {{ bubbles: true }}));
                                target.dispatchEvent(new Event('input', {{ bubbles: true }}));
                                target.dispatchEvent(new Event('change', {{ bubbles: true }}));
                                target.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true }}));
                                target.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true }}));
                                target.dispatchEvent(new Event('blur', {{ bubbles: true }}));
                                
                                await wait(300);
                                
                                // VERIFY: Check content length
                                var finalLen = (target.innerText || target.textContent || '').trim().length;
                                
                                // Check if tweet button is enabled
                                var btn = document.querySelector('[data-testid=""tweetButton""]');
                                var btnOk = btn && !btn.disabled && btn.getAttribute('aria-disabled') !== 'true';
                                
                                return finalLen >= 5 ? ('ok_' + finalLen + (btnOk ? '_btn' : '')) : ('empty_' + finalLen);
                            }})();
                        ";
                        
                        var typeTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(typeJs)));
                        var typeRes = await typeTask;
                        LogAI($"   > Yazım sonucu (deneme {attempt}): {typeRes}");
                        
                        if (typeRes != null && typeRes.Contains("ok"))
                        {
                            typeSuccess = true;
                            break;
                        }
                        
                        // If insertText failed, try clipboard paste as fallback
                        if (attempt == 3)
                        {
                            LogAI("   > insertText başarısız, clipboard fallback deneniyor...");
                            this.Invoke(new Action(() => {
                                try { Clipboard.SetText(tweets[i]); } catch { }
                            }));
                            await Task.Delay(200);
                            
                            var focusPasteJs = $@"
                                (function() {{
                                    var modal = document.querySelector('[role=""dialog""]');
                                    var boxes = Array.from(modal.querySelectorAll('[role=""textbox""]')).filter(el => el.offsetParent !== null);
                                    if (boxes.length <= {i}) return 'no_box';
                                    boxes[{i}].focus();
                                    return 'focused';
                                }})();
                            ";
                            var focTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(focusPasteJs)));
                            await focTask;
                            
                            this.Invoke(new Action(() => {
                                _webViewTwitter.Focus();
                                SendKeys.SendWait("^v");
                            }));
                            await Task.Delay(500);
                        }
                        
                        LogAI($"   ⚠️ Tweet {i + 1} deneme {attempt} başarısız, tekrar deneniyor...");
                        await Task.Delay(1500);
                    }
                    
                    if (!typeSuccess)
                    {
                        return new SocialIntelResult { status = "error", message = $"Failed to type tweet {i+1}" };
                    }

                    // 5. Add new tweet box if not last
                    if (i < tweets.Count - 1)
                    {
                        LogAI("   > Yeni tweet kutusu ekleniyor...");
                        var addTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                            (async function() {
                                const wait = (ms) => new Promise(r => setTimeout(r, ms));
                                var modal = document.querySelector('[role=""dialog""]');
                                if (!modal) return 'no_modal';
                                
                                // Scroll to bottom of modal
                                var scrollable = modal.querySelector('[data-testid=""ScrollContainer""]') || modal;
                                scrollable.scrollTop = scrollable.scrollHeight;
                                await wait(300);
                                
                                // Find add button - multiple selectors
                                var btn = modal.querySelector('[data-testid=""addButton""]') ||
                                          Array.from(modal.querySelectorAll('[role=""button""], button')).find(b => {
                                              var label = (b.getAttribute('aria-label') || '').toLowerCase();
                                              var svg = b.querySelector('svg');
                                              return (label.includes('add') || label.includes('ekle') || 
                                                     (svg && b.offsetParent !== null && b.offsetWidth < 60));
                                          });
                                
                                if (btn) {
                                    btn.scrollIntoView({behavior: 'instant', block: 'center'});
                                    await wait(200);
                                    btn.click();
                                    await wait(500);
                                    return 'added';
                                }
                                return 'btn_not_found';
                            })();
                        ")));
                        var addRes = await addTask;
                        LogAI($"   > Ekleme sonucu: {addRes}");
                        
                        if (addRes == null || !addRes.Contains("added"))
                        {
                            // Try keyboard shortcut as fallback
                            LogAI("   > Klavye kısayolu deneniyor (Ctrl+Enter)...");
                            this.Invoke(new Action(() => {
                                _webViewTwitter.Focus();
                                SendKeys.SendWait("^{ENTER}");
                            }));
                            await Task.Delay(1000);
                        }
                        else
                        {
                            await Task.Delay(1500);
                        }
                    }
                }

                // 6. Click Post button
                LogAI("   > Tüm tweetler hazır. Paylaşılıyor...");
                var postTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                    (function() {
                        var btn = document.querySelector('[data-testid=""tweetButton""]') || 
                                  document.querySelector('[data-testid=""tweetButtonInline""]');
                        if (btn) {
                            btn.scrollIntoView({behavior: 'instant', block: 'center'});
                            btn.click();
                            return 'clicked';
                        }
                        return 'btn_not_found';
                    })();
                ")));
                var postRes = await postTask;
                LogAI($"   > Gönder butonu: {postRes}");

                // 7. Verify success (modal closes)
                await Task.Delay(2000);
                bool success = false;
                for (int m = 0; m < 10; m++)
                {
                    var checkTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitter.ExecuteScriptAsync(@"
                        (function() { 
                            var modal = document.querySelector('[role=""dialog""]');
                            return modal ? 'open' : 'closed';
                        })()
                    ")));
                    var checkRes = await checkTask;
                    if (checkRes != null && checkRes.Contains("closed"))
                    {
                        success = true;
                        break;
                    }
                    await Task.Delay(1000);
                }

                if (success)
                {
                    LogAI("   ✅ Thread başarıyla yayınlandı!");
                    return new SocialIntelResult { status = "success", message = "Thread posted via WebView2" };
                }
                else
                {
                    LogAI("   ⚠️ Modal kapanmadı ama gönderim denenmiş olabilir.");
                    return new SocialIntelResult { status = "success", message = "Thread sent (unverified)" };
                }
            }
            catch (Exception ex)
            {
                LogAI($"❌ Dahili Thread Hatası: {ex.Message}");
                return new SocialIntelResult { status = "error", message = ex.Message };
            }
        }

        private async Task<List<InfluencerPost>> PerformInternalSearchAsync(string symbol, string query, string market, List<string>? handles)
        {
            try
            {
                LogSocial($"🔍 Dahili arama (Background): {query} (Filtre: {symbol})");

                // Check memory for historical analysis
                if (_opManager.Memory != null && !string.IsNullOrWhiteSpace(symbol))
                {
                    var memories = _opManager.Memory.Recall(symbol, maxAgeHours: 4);
                    if (memories.Any())
                    {
                        Log($"🧠 Memory: {symbol} için son 4 saatte paylaşım yapıldı. Atlanıyor.", "Signal");
                        return new List<InfluencerPost>();
                    }
                }
                
                var collected = new List<InfluencerPost>();
                string filterToken = string.IsNullOrWhiteSpace(symbol) ? string.Empty : symbol.Trim().ToUpperInvariant();
                string filterJson = JsonSerializer.Serialize(filterToken);

                // 1) VIP Handle Tarama (timeline)
                if (handles != null && handles.Count > 0)
                {
                    int scannedCount = 0;
                    // INCREASED LIMIT v2.8.9: Scan up to 50 VIP handles (was 15) to ensure full coverage
                    foreach (var rawHandle in handles.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct(StringComparer.OrdinalIgnoreCase).Take(50))
                    {
                        string cleanHandle = rawHandle.Trim().TrimStart('@');
                        if (string.IsNullOrEmpty(cleanHandle)) continue;

                        if (_webViewTwitterBg.InvokeRequired)
                        {
                            _webViewTwitterBg.Invoke(new Action(() => 
                            {
                                if (!_webViewTwitterBg.Visible) _webViewTwitterBg.Visible = true;
                                _webViewTwitterBg.Source = new Uri($"https://x.com/{cleanHandle}");
                            }));
                        }
                        else
                        {
                            if (!_webViewTwitterBg.Visible) _webViewTwitterBg.Visible = true;
                            _webViewTwitterBg.Source = new Uri($"https://x.com/{cleanHandle}");
                        }
                        await Task.Delay(5000); 
                        
                        scannedCount++;

                        var timelineBuilder = new StringBuilder();
                        timelineBuilder.AppendLine("(() => {");
                        timelineBuilder.AppendLine($"    const filter = {filterJson};");
                        timelineBuilder.AppendLine("    const blacklist = ['RAHMET', 'TAZIYE', 'MEKANI CENNET', 'NUR ICINDE', 'BASSAGLIGI', 'VEFAT', 'KANDIL', 'BAYRAM', 'FUTBOL', 'GOL', 'MAC SONUCU', 'AYET', 'SURE', 'BAKARA', 'ALLAH', 'AMIN', 'SIYASET', 'BAKAN', 'PARTI', 'CHP', 'AKP', 'MHP', 'BELEDIYE', 'BASKAN', 'ZIYARET', 'HAYIRLI', 'DUGUN', 'NISAN', 'ACILIS', 'TESKILAT', 'MUHTAR'];");
                        timelineBuilder.AppendLine("    const technicals = ['GRAFIK', 'ANALIZ', 'TEKNIK', 'RSI', 'MACD', 'DIRENC', 'DESTEK', 'KIRILIM', 'HEDEF', 'MUM', 'BORSA'];");
                        timelineBuilder.AppendLine("    const posts = [];");
                        timelineBuilder.AppendLine("    const seen = new Set();");
                        timelineBuilder.AppendLine("    const articles = document.querySelectorAll('article[data-testid=\"tweet\"]');");
                        timelineBuilder.AppendLine("    articles.forEach(art => {");
                        timelineBuilder.AppendLine("        try {");
                        timelineBuilder.AppendLine("            const contentEl = art.querySelector('[data-testid=\"tweetText\"]');");
                        timelineBuilder.AppendLine("            const urlEl = art.querySelector('a[href*=\"/status/\"]');");
                        timelineBuilder.AppendLine("            const text = contentEl.innerText || '';");
                        timelineBuilder.AppendLine("            const textUpper = text.toUpperCase();");
                        timelineBuilder.AppendLine("            ");
                        timelineBuilder.AppendLine("            // 1. Blacklist check");
                        timelineBuilder.AppendLine("            if (blacklist.some(kw => textUpper.includes(kw))) return;");
                        timelineBuilder.AppendLine("            ");
                        timelineBuilder.AppendLine("            const filterUpper = filter ? filter.toUpperCase() : '';");
                        timelineBuilder.AppendLine("            const cleanFilter = filterUpper.replace('#', '').replace('$', '');");
                        timelineBuilder.AppendLine("            ");
                        timelineBuilder.AppendLine("            // 2. SCORING");
                        timelineBuilder.AppendLine("            let score = 0;");
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            timelineBuilder.AppendLine("            if (filterUpper && (textUpper.includes(filterUpper) || textUpper.includes('$' + cleanFilter) || textUpper.includes('#' + cleanFilter))) score += 50;");
                        }
                        timelineBuilder.AppendLine("            technicals.forEach(kw => { if (textUpper.includes(kw)) score += 10; });");
                        timelineBuilder.AppendLine("            ");
                        timelineBuilder.AppendLine("            // RELAXED THRESHOLD: 10 points");
                        timelineBuilder.AppendLine("            if (score < 10) return;");
                        timelineBuilder.AppendLine("            ");
                        timelineBuilder.AppendLine("            const url = urlEl.href;");
                        timelineBuilder.AppendLine("            if (seen.has(url)) return;");
                        timelineBuilder.AppendLine("            seen.add(url);");
                        timelineBuilder.AppendLine("            const timeEl = art.querySelector('time');");
                        timelineBuilder.AppendLine("            const imgEls = art.querySelectorAll('img[src*=\"/media/\"]');");
                        timelineBuilder.AppendLine("            const imageUrls = Array.from(imgEls).map(img => img.src).join(',');");
                        timelineBuilder.AppendLine("            posts.push({");
                        timelineBuilder.AppendLine($"                handle: '@{cleanHandle}',");
                        timelineBuilder.AppendLine("                content: text,");
                        timelineBuilder.AppendLine("                url: url,");
                        timelineBuilder.AppendLine("                imageUrl: imageUrls,");
                        timelineBuilder.AppendLine("                engagement: 0,");
                        timelineBuilder.AppendLine("                relevance_score: score,");
                        timelineBuilder.AppendLine("                postDate: timeEl ? timeEl.getAttribute('datetime') : new Date().toISOString()");
                        timelineBuilder.AppendLine("            });");
                        timelineBuilder.AppendLine("        } catch(e) {}");
                        timelineBuilder.AppendLine("    });");
                        timelineBuilder.AppendLine("    return JSON.stringify(posts);");
                        timelineBuilder.AppendLine("})();");

                        var rawTimelineTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync(timelineBuilder.ToString())));
                        var rawTimeline = await rawTimelineTask;
                        if (!string.IsNullOrWhiteSpace(rawTimeline) && rawTimeline != "null")
                        {
                            try
                            {
                                // WebView2 may return double-wrapped JSON string or direct JSON
                                string jsonData = rawTimeline.Trim().StartsWith("\"") 
                                    ? JsonSerializer.Deserialize<string>(rawTimeline) ?? ""
                                    : rawTimeline;
                                
                                if (!string.IsNullOrWhiteSpace(jsonData) && jsonData != "null")
                                {
                                    var parsed = JsonSerializer.Deserialize<List<InfluencerPost>>(jsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<InfluencerPost>();
                                    if (parsed.Count > 0)
                                    {
                                        collected.AddRange(parsed);
                                        LogSocial($"📊 @{cleanHandle}: {parsed.Count} post bulundu (Toplam: {collected.Count})");
                                    }
                                }
                            }
                            catch (JsonException jex)
                            {
                                LogSocial($"⚠️ Timeline JSON parse hatası: {jex.Message}");
                            }
                        }

                        // INCREASED LIMIT v2.8.9: 100 items (was 30) for better deep analysis
                        if (collected.Count >= 100) 
                        {
                            LogSocial($"✅ Yeterli analiz bulundu ({collected.Count} post), {scannedCount}/{handles.Count} fenomen tarandı.");
                            break;
                        }
                    }

                    if (collected.Count > 0)
                    {
                        LogSocial($"📊 VIP aramasında {collected.Count} post bulundu, global arama ile harmanlanacak.");
                    }
                }

                // 2) Global arama (latest feed) - Relaxed Query (No min_faves or filter:safe)
                string encodedQuery = Uri.EscapeDataString(query);
                string searchUrl = $"https://x.com/search?q={encodedQuery}&src=typed_query&f=live";
                _webViewTwitterBg.Invoke(new Action(() => _webViewTwitterBg.Source = new Uri(searchUrl)));
                await Task.Delay(6000); // Arama sonuçlarının yüklenmesi için daha fazla süre (4s yetersiz kalabiliyor)

                // 1. Initial Scroll (C# side)
                var scroll1Task = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync("window.scrollBy(0, 800)")));
                await scroll1Task;
                await Task.Delay(1500);
                var scroll2Task = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync("window.scrollBy(0, 800)")));
                await scroll2Task;
                await Task.Delay(1500);

                var searchBuilder = new StringBuilder();
                searchBuilder.AppendLine("(function() {");
                searchBuilder.AppendLine("    const posts = [];");
                searchBuilder.AppendLine("    const seenUrls = new Set();");
                searchBuilder.AppendLine($"    const filter = {filterJson};");
                searchBuilder.AppendLine("    const blacklist = ['RAHMET', 'TAZIYE', 'MEKANI CENNET', 'NUR ICINDE', 'BASSAGLIGI', 'VEFAT', 'KANDIL', 'BAYRAM', 'FUTBOL', 'GOL', 'MAC SONUCU', 'AYET', 'SURE', 'BAKARA', 'ALLAH', 'AMIN', 'SIYASET', 'BAKAN', 'PARTI', 'CHP', 'AKP', 'MHP', 'BELEDIYE', 'BASKAN', 'ZIYARET', 'HAYIRLI', 'DUGUN', 'NISAN', 'ACILIS', 'TESKILAT', 'MUHTAR'];");
                searchBuilder.AppendLine("    const technicals = ['GRAFIK', 'ANALIZ', 'TEKNIK', 'RSI', 'MACD', 'DIRENC', 'DESTEK', 'KIRILIM', 'HEDEF', 'MUM', 'BORSA'];");
                searchBuilder.AppendLine("    const articles = document.querySelectorAll('article[data-testid=\"tweet\"], article');");
                searchBuilder.AppendLine("    articles.forEach(art => {");
                searchBuilder.AppendLine("        try {");
                searchBuilder.AppendLine("            // RELAXED SELECTORS: Try multiple ways to get content");
                searchBuilder.AppendLine("            let contentEl = art.querySelector('[data-testid=\"tweetText\"]');");
                searchBuilder.AppendLine("            let text = contentEl ? (contentEl.innerText || '') : '';");
                searchBuilder.AppendLine("            // FALLBACK: If tweetText not found, use article's visible text");
                searchBuilder.AppendLine("            if (!text || text.length < 10) {");
                searchBuilder.AppendLine("                text = art.innerText || '';");
                searchBuilder.AppendLine("                // Clean up metadata noise (likes, retweets, etc)");
                searchBuilder.AppendLine("                text = text.split('\\n').filter(line => line.length > 20).slice(0, 5).join(' ');");
                searchBuilder.AppendLine("            }");
                searchBuilder.AppendLine("            if (!text || text.length < 10) return;");
                searchBuilder.AppendLine("            const handleEl = art.querySelector('[data-testid=\"User-Names\"]');");
                searchBuilder.AppendLine("            const urlEl = art.querySelector('a[href*=\"/status/\"]');");
                searchBuilder.AppendLine("            // URL is optional now");
                searchBuilder.AppendLine("            const textUpper = text.toUpperCase();");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            // 1. Blacklist check");
                searchBuilder.AppendLine("            if (blacklist.some(kw => textUpper.includes(kw))) return;");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            const fuel = filter ? filter.toUpperCase() : '';");
                searchBuilder.AppendLine("            const clean = fuel.replace('$', '').replace('#', '');");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            // 2. SCORING");
                searchBuilder.AppendLine("            let score = 0;");
                if (!string.IsNullOrEmpty(symbol))
                {
                    searchBuilder.AppendLine("            if (fuel && (textUpper.includes(fuel) || textUpper.includes('$' + clean) || textUpper.includes('#' + clean))) score += 50;");
                }
                searchBuilder.AppendLine("            technicals.forEach(kw => { if (text.toUpperCase().includes(kw)) score += 10; });");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            // 3. TICKER STUFFING PENALTY");
                searchBuilder.AppendLine("            const tickers = text.match(/[$#][A-Z]{2,10}/g) || [];");
                searchBuilder.AppendLine("            const uniqueTickers = [...new Set(tickers)];");
                searchBuilder.AppendLine("            if (uniqueTickers.length > 5) score -= 80;");
                searchBuilder.AppendLine("            else if (uniqueTickers.length > 3) score -= 30;");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            // RELAXED THRESHOLD: 10 points");
                searchBuilder.AppendLine("            if (score < 10) return; ");
                searchBuilder.AppendLine("            // URL is optional now");
                searchBuilder.AppendLine("            let url = urlEl ? urlEl.href : '';");
                searchBuilder.AppendLine("            if (url && seenUrls.has(url)) return;");
                searchBuilder.AppendLine("            if (url) seenUrls.add(url);");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            let handleText = handleEl ? handleEl.innerText : ''; ");
                searchBuilder.AppendLine("            let safeHandle = handleText.includes('@') ? '@' + handleText.split('@')[1].split('\\n')[0].split(' ')[0] : '';");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            // FALLBACK HANDLE: If standard User-Names fails");
                searchBuilder.AppendLine("            if (!safeHandle || safeHandle === 'X-User') {");
                searchBuilder.AppendLine("                const links = Array.from(art.querySelectorAll('a[role=\"link\"]'));");
                searchBuilder.AppendLine("                for (let l of links) {");
                searchBuilder.AppendLine("                    if (l.href && !l.href.includes('/status/') && l.href.includes('x.com/')) {");
                searchBuilder.AppendLine("                        safeHandle = '@' + l.href.split('x.com/')[1].split('/')[0].split('?')[0];");
                searchBuilder.AppendLine("                        break;");
                searchBuilder.AppendLine("                    }");
                searchBuilder.AppendLine("                }");
                searchBuilder.AppendLine("            }");
                searchBuilder.AppendLine("            if (!safeHandle) safeHandle = handleText.trim() || 'X-User';");
                searchBuilder.AppendLine("            ");
                searchBuilder.AppendLine("            let likes = 0;");
                searchBuilder.AppendLine("            const group = art.querySelector('[role=\"group\"]');");
                searchBuilder.AppendLine("            if (group) {");
                searchBuilder.AppendLine("                const likeBtn = group.querySelector('[data-testid=\"like\"]');");
                searchBuilder.AppendLine("                if (likeBtn && likeBtn.innerText) {");
                searchBuilder.AppendLine("                    const digits = likeBtn.innerText.replace(/[^0-9]/g, '');");
                searchBuilder.AppendLine("                    likes = digits ? parseInt(digits) : 0;");
                searchBuilder.AppendLine("                }");
                searchBuilder.AppendLine("            }");
                searchBuilder.AppendLine("            const timeEl = art.querySelector('time');");
                searchBuilder.AppendLine("            const imgEls = art.querySelectorAll('img[src*=\"/media/\"]');");
                searchBuilder.AppendLine("            const imageUrls = Array.from(imgEls).map(img => img.src).join(',');");
                searchBuilder.AppendLine("            posts.push({");
                searchBuilder.AppendLine("                handle: safeHandle,");
                searchBuilder.AppendLine("                content: text,");
                searchBuilder.AppendLine("                url: url || 'https://x.com',");
                searchBuilder.AppendLine("                imageUrl: imageUrls,");
                searchBuilder.AppendLine("                engagement: likes,");
                searchBuilder.AppendLine("                relevance_score: score,");
                searchBuilder.AppendLine("                postDate: timeEl ? timeEl.getAttribute('datetime') : new Date().toISOString()");
                searchBuilder.AppendLine("            });");
                searchBuilder.AppendLine("        } catch(e) {}");
                searchBuilder.AppendLine("    });");
                searchBuilder.AppendLine("    return JSON.stringify(posts);");
                searchBuilder.AppendLine("})();");

                var jsonTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync(searchBuilder.ToString())));
                var json = await jsonTask;
                
                // Track article count for debugging
                string countJs = "document.querySelectorAll('article[data-testid=\"tweet\"]').length";
                var artCountTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync(countJs)));
                var artCount = await artCountTask;
                LogSocial($"🔍 Sayfada {artCount} makale (article) tespit edildi.");

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    LogSocial("⚠️ Arama sonucu boş.");
                    return new List<InfluencerPost>();
                }

                try
                {
                    // WebView2 sometimes returns the string literal with escaped quotes or null as a string "null"
                    if (string.IsNullOrWhiteSpace(json) || json == "null" || json == "{}")
                    {
                        LogSocial("⚠️ Arama sonucu boş veya geçersiz format (Raw: " + (json ?? "null") + ")");
                        return new List<InfluencerPost>();
                    }

                    string jsonData = json.Trim();
                    if (jsonData.StartsWith("\"") && jsonData.EndsWith("\"") && jsonData.Length > 2)
                    {
                         try { jsonData = JsonSerializer.Deserialize<string>(json) ?? json; } catch { }
                    }
                    
                    if (string.IsNullOrWhiteSpace(jsonData) || jsonData == "null" || jsonData == "[]" || jsonData == "{}")
                    {
                        LogSocial("⚠️ Global arama iç JSON boş.");
                        var memory = _opManager?.Memory;
                        if (memory != null)
                        {
                            foreach (var p in collected.Where(x => x != null)) memory.Learn(p!);
                        }
                        return collected; // VIP sonuçlarını koru
                    }

                    var globalParsed = JsonSerializer.Deserialize<List<InfluencerPost>>(jsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<InfluencerPost>();
                    if (globalParsed.Count > 0)
                    {
                        collected.AddRange(globalParsed);
                        LogSocial($"✅ Global arama tamamlandı: {globalParsed.Count} sonuç eklendi.");
                    }
                }
                catch (JsonException jex)
                {
                    LogSocial($"⚠️ Global arama JSON parse hatası: {jex.Message}");
                    var memory = _opManager?.Memory;
                    if (memory != null)
                    {
                        foreach (var p in collected.Where(x => x != null)) memory.Learn(p!);
                    }
                    return collected; // VIP sonuçlarını koru
                }
                var memCache = _opManager?.Memory;
                if (memCache != null)
                {
                    foreach (var p in collected.Where(x => x != null)) memCache.Learn(p!);
                }
                
                // Strict Telegram Filtering (Final Layer)
                collected = collected
                    .Where(p => !string.IsNullOrEmpty(p?.Content))
                    .Where(p => {
                        string contentUpper = p!.Content!.ToUpperInvariant();
                        return !contentUpper.Contains("T.ME") && 
                               !contentUpper.Contains("TELEGRAM") && 
                               !contentUpper.Contains("TG:") &&
                               !contentUpper.Contains("T. ME");
                    })
                    .ToList();

                return collected;
            }
            catch (Exception ex)
            {
                LogSocial($"⚠️ Dahili arama hatası: {ex.Message}");
                return new List<InfluencerPost>();
            }
        }

        private async Task<ProfileStats?> FetchInternalProfileStatsAsync()
        {
            try
            {
                string js = @"
                    (() => {
                        let following = document.querySelector('a[href$=""/following""] span')?.innerText || '0';
                        let followers = document.querySelector('a[href$=""/verified_followers""] span, a[href$=""/followers""] span')?.innerText || '0';
                        return JSON.stringify({ following, followers });
                    })();
                ";
                var jsonTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync(js)));
                var json = await jsonTask;
                var innerJson = JsonSerializer.Deserialize<string>(json);
                if (string.IsNullOrEmpty(innerJson)) return null;
                return JsonSerializer.Deserialize<ProfileStats>(innerJson);
            }
            catch { return null; }
        }

        private async Task<string[]> FetchInternalTrendsAsync()
        {
            try
            {
                string js = @"
                    (() => {
                        let trends = [];
                        // Check sidebar trends
                        document.querySelectorAll('[data-testid=""trend""]').forEach(el => {
                            let spans = el.querySelectorAll('span');
                            let text = '';
                            if (spans.length >= 3) text = spans[1].innerText; // Usually the second or third span is the name
                            else text = el.innerText.split('\n')[1] || el.innerText.split('\n')[0];
                            
                            if (text && text.length > 2 && text.length < 50) trends.push(text);
                        });
                        return JSON.stringify(trends);
                    })();
                ";
                var jsonTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync(js)));
                var json = await jsonTask;
                var innerJson = JsonSerializer.Deserialize<string>(json);
                if (string.IsNullOrEmpty(innerJson)) return Array.Empty<string>();
                var trends = JsonSerializer.Deserialize<string[]>(innerJson) ?? Array.Empty<string>();
                return trends.Distinct().ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        private async Task<SocialIntelResult> PerformInternalReplyAsync(string url, string text)
        {
            // Same logic as Post but navigate to URL first
            try
            {
                _webViewTwitterBg.Invoke(new Action(() => _webViewTwitterBg.Source = new Uri(url)));
                await Task.Delay(3000);
                
                string js = $@"
                    (async () => {{
                        const wait = (ms) => new Promise(r => setTimeout(r, ms));
                        let replyBox = document.querySelector('[data-testid=""tweetTextarea_0""]') || document.querySelector('.public-DraftEditor-content');
                        if (replyBox) {{
                            replyBox.focus();
                            document.execCommand('insertText', false, `{text.Replace("`", "\\`")}`);
                            await wait(500);
                            let btn = document.querySelector('[data-testid=""tweetButtonInline""]');
                            if (btn) btn.click();
                            return {{ status: 'success' }};
                        }}
                        return {{ status: 'error', message: 'Reply box not found' }};
                    }})();
                ";
                var resTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync(js)));
                var res = await resTask;
                return new SocialIntelResult { status = "success" }; // Simplified
            }
            catch { return new SocialIntelResult { status = "error" }; }
        }

        private async Task<SocialIntelInteractResult> PerformInternalInteractionAsync(string[] users)
        {
            try
            {
                LogSocial($"🤝 Dahili etkileşim başlatılıyor ({users.Length} hesap)...");
                int followed = 0;

                foreach (var user in users)
                {
                    string handle = user.Trim().TrimStart('@');
                    string profileUrl = $"https://x.com/{handle}";
                    
                    _webViewTwitterBg.Invoke(new Action(() => _webViewTwitterBg.Source = new Uri(profileUrl)));
                    await Task.Delay(4000); // Wait for profile load

                    string js = @"
                        (async () => {
                            let btn = document.querySelector('[data-testid$=""-follow""]') || 
                                      document.querySelector('[aria-label^=""Follow @""]') ||
                                      document.querySelector('[aria-label^=""Takip et @""]');
                            
                            if (btn && (btn.innerText.includes('Follow') || btn.innerText.includes('Takip et'))) {
                                btn.click();
                                return true;
                            }
                            return false;
                        })();
                    ";
                    var resultTask = (Task<string>)this.Invoke(new Func<Task<string>>(() => _webViewTwitterBg.ExecuteScriptAsync(js)));
                    var result = await resultTask;
                    if (result == "true") followed++;
                    
                    await Task.Delay(2000); // Rate limit protection
                }

                return new SocialIntelInteractResult { 
                    status = "success", 
                    message = $"{followed} hesap başarıyla takip edildi." 
                };
            }
            catch (Exception ex)
            {
                return new SocialIntelInteractResult { status = "error", message = ex.Message };
            }
        }

        #endregion



// Removed legacy BtnNewsToggle_Click

        // News queue for delayed processing
        private Queue<(NewsItem news, string analysis)> _newsQueue = new Queue<(NewsItem, string)>();
        private bool _isNewsQueueProcessing = false;
        
        // News deduplication (title similarity)
        private readonly HashSet<string> _postedNewsToday = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _newsLock = new object();
        
        private async void OnNewsReceived(NewsItem news)
        {
            await _opManager.NewsEng.ProcessNews(news);
        }    
        // v2.3: NewsEngine'den gelen sonuçları kuyruğa atıp Twitter'a gönderen handler
        private void OnNewsEngineProcessed(NewsItem news, string analysis, int importance)
        {
            // v3.7.2 FIX: Prevent Double-Posting
            // If importance is 5, NewsEngine has already posted it (or tried to).
            // We should NOT enqueue it again in the MainForm's news queue.
            if (importance >= 5)
            {
                LogNews($"ℹ️ Haber zaten NewsEngine tarafından işlendi/paylaşıldı: {news.Title}");
                return;
            }

            // v2.8: Twitter Dispatch Threshold artık ayarlardan okunuyor
            if (importance >= ConfigManager.Current.MinNewsImportance && ConfigManager.Current.AutoPostBreakingNews)
            {
                lock (_newsLock)
                {
                    LogNews($"⏳ Haber X kuyruğuna eklendi: {news.Title} (Önem: {importance}/5)");
                    _newsQueue.Enqueue((news, analysis));
                }

                if (!_isNewsQueueProcessing)
                {
                    _ = ProcessNewsQueue();
                }
            }
        }
        
        private async Task ProcessNewsQueue()
        {
            _isNewsQueueProcessing = true;
            LogNews("📰 Haber kuyruğu işleniyor...");
            bool isPremium = ConfigManager.Current?.IsVerifiedAccount ?? false;
            
            while (_newsQueue.Count > 0)
            {
                var (news, analysis) = _newsQueue.Dequeue();
                LogNews($"📰 Sending: {news.Title}");
                
                // v3.0: GELİŞMİŞ İÇERİK KALİTE KONTROLÜ - Bozuk Gemini yanıtlarını engelle
                // 1. Boş veya çok kısa içerik kontrolü
                if (string.IsNullOrWhiteSpace(analysis) || analysis.Length < 50)
                {
                    LogNews($"⚠️ İçerik çok kısa veya boş, atlanıyor: {news.Title}");
                    continue;
                }
                
                // 2. Hashtag oranı kontrolü
                int hashtagCount = System.Text.RegularExpressions.Regex.Matches(analysis, @"#\w+").Count;
                var words = analysis.Split(new[] { ' ', '\n', '.', ',', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
                int wordCount = words.Length;
                if (wordCount > 0 && (double)hashtagCount / wordCount > 0.5)
                {
                    LogNews($"⚠️ İçerik çoğunlukla hashtaglerden oluşuyor (bozuk Gemini yanıtı), atlanıyor: {news.Title}");
                    continue;
                }
                
                // 3. LOOP/TEKRAR TESPİTİ - Aynı kelime veya ifade sürekli tekrarlanıyorsa SPAM
                bool isLoopContent = false;
                var wordFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var word in words)
                {
                    if (word.Length < 3) continue; // Çok kısa kelimeleri atla
                    if (wordFrequency.ContainsKey(word))
                        wordFrequency[word]++;
                    else
                        wordFrequency[word] = 1;
                }
                
                // Bir kelime 5+ kez tekrarlanıyorsa SPAM
                var topRepeated = wordFrequency.OrderByDescending(kv => kv.Value).FirstOrDefault();
                if (topRepeated.Value >= 5 && wordCount > 10)
                {
                    isLoopContent = true;
                    LogNews($"⚠️ LOOP TESPİT: '{topRepeated.Key}' kelimesi {topRepeated.Value} kez tekrarlanıyor. İçerik atlanıyor: {news.Title}");
                }
                
                // Benzersiz kelime oranı %30'un altındaysa SPAM (çok fazla tekrar var)
                if (!isLoopContent && wordCount > 20)
                {
                    double uniqueRatio = (double)wordFrequency.Count / wordCount;
                    if (uniqueRatio < 0.30)
                    {
                        isLoopContent = true;
                        LogNews($"⚠️ LOOP TESPİT: Benzersiz kelime oranı çok düşük ({uniqueRatio:P0}). İçerik atlanıyor: {news.Title}");
                    }
                }
                
                // 4. Ardışık tekrar tespiti (aynı cümle/parça art arda tekrarlanıyor mu?)
                if (!isLoopContent)
                {
                    // 20 karakterlik parçaları kontrol et
                    var chunks = new List<string>();
                    for (int i = 0; i < analysis.Length - 20; i += 10)
                    {
                        chunks.Add(analysis.Substring(i, 20));
                    }
                    var chunkFreq = chunks.GroupBy(c => c).Where(g => g.Count() >= 3).FirstOrDefault();
                    if (chunkFreq != null)
                    {
                        isLoopContent = true;
                        LogNews($"⚠️ LOOP TESPİT: '{chunkFreq.Key.Substring(0, Math.Min(15, chunkFreq.Key.Length))}...' parçası {chunkFreq.Count()} kez tekrarlanıyor. İçerik atlanıyor: {news.Title}");
                    }
                }
                
                if (isLoopContent)
                {
                    continue;
                }
                
                SocialIntelResult result;

                // Modül: Haber (per-module spam check)
                if (ConfigManager.Current?.SpamProtectNews ?? false)
                {
                    if (!_opManager.Spam.CanPostGeneral(out string reason))
                    {
                        LogNews($"🛡️ Spam protection (Haber): {reason}");
                        continue;
                    }
                }
                
                if (isPremium && analysis.Contains("|||"))
                {
                    // PREMIUM ACCOUNT: 2-Tweet Format
                    LogNews("💙 Premium hesap: 2-tweet formatı kullanılıyor...");
                    
                    var parts = analysis.Split(new[] { "|||" }, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        var tweets = new List<string> { parts[0], parts[1] };
                        
                        result = await _opManager.SocialIntel.PostThreadAsync(tweets);
                        if (result.status == "success")
                        {
                            LogNews("✅ Premium News Thread (2-tweet) sent!");
                            _opManager.Spam.RecordTweet("NEWS", "NEWS");

                        }
                        else
                        {
                            LogNews($"❌ Thread failed: {result.ErrorMessage}");
                        }
                    }
                    else
                    {
                        LogNews("⚠️ Premium format parse failed, fallback to single tweet");
                        result = await _opManager.SocialIntel.PostTweet(analysis);
                        if (result.status == "success")
                        {
                            LogNews("✅ News Tweet sent!");
                            _opManager.Spam.RecordTweet("NEWS", "NEWS");

                        }
                    }
                }
                else if (analysis.Length > 260 && !isPremium)
                {
                    // NON-PREMIUM: Long content = Thread Format
                    LogNews("🧵 Content long, converting to News Thread...");
                    var tweets = new List<string>();
                    
                    // Tweet 1: Hook / Title
                    tweets.Add($"🚨 SON DAKİKA: {news.Title}\n\nInvesting kaynaklı haberi analiz ettim. Önemi yüksek! 🔥\n🔗 Kaynak: {news.Link}\n\n#BIST100 #Haber #Borsa\n👇 Detaylar aşağıda...");
                    
                    // Tweet 2+: Split Analysis
                    var newsWords = analysis.Split(' ');
                    string currentTweet = "";
                    foreach(var word in newsWords)
                    {
                        if ((currentTweet + word).Length > 250)
                        {
                            if (!string.IsNullOrWhiteSpace(currentTweet))
                                tweets.Add(currentTweet.Trim());
                            currentTweet = word + " ";
                        }
                        else
                        {
                            currentTweet += word + " ";
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(currentTweet)) 
                        tweets.Add(currentTweet.Trim());
                    
                    result = await _opManager.SocialIntel.PostThreadAsync(tweets);
                    if (result.status == "success")
                    {
                        LogNews("✅ News Thread sent!");
                        _opManager.Spam.RecordTweet("NEWS", "NEWS");
                    }
                    else
                    {
                        LogNews($"❌ Thread failed: {result.ErrorMessage}");
                    }
                }
                else
                {
                    // Single Tweet (fits in 280 chars)
                    string content = analysis;
                    if (content.Length > 280)
                        content = content.Substring(0, 277) + "...";
                    
                    result = await _opManager.SocialIntel.PostTweet(content);
                    if (result.status == "success")
                    {
                        LogNews("✅ News Tweet sent!");
                        _opManager.Spam.RecordTweet("NEWS", "NEWS");
                    }
                    else
                    {
                        LogNews($"❌ Tweet failed: {result.ErrorMessage}");
                    }
                }
                
                // ADAPTIVE QUEUE CONTROL (v2.8)
                if (_newsQueue.Count > 0)
                {
                    // 1. Queue Cleanup: Eğer kuyruk çok şişmişse (Örn: 50+), en eski haberleri temizle (güncelliği korumak için)
                    if (_newsQueue.Count > 50)
                    {
                        LogNews($"🧹 Kuyruk çok yoğun ({_newsQueue.Count}), en eski 10 haber temizleniyor...");
                        lock (_newsLock)
                        {
                            for (int i = 0; i < 10 && _newsQueue.Count > 0; i++) 
                                _newsQueue.Dequeue();
                        }
                    }

                    // 2. Delay: Kullanıcı isteği üzerine her zaman 3 dakika bekle (Hesap güvenliği için)
                    int delayMinutes = 3;
                    
                    LogNews($"⏳ Kuyrukta {_newsQueue.Count} haber kaldı. {delayMinutes} dk bekleniyor...");
                    await Task.Delay(TimeSpan.FromMinutes(delayMinutes));
                }
            }
            
            _isNewsQueueProcessing = false;
            Log("✅ Haber kuyruğu tamamlandı.");
        }

        private async Task UpdateSocialStats()
        {
            try
            {
                Log("🔍 Sosyal medya etkileşim verileri güncelleniyor...", "Stats");
                var engagementData = await _opManager.SocialIntel.GetRecentEngagementAsync();
                
                foreach (var eng in engagementData)
                {
                    // Try to guess symbol from text if possible, else use "GENERAL"
                    string symbol = "GENERAL";
                    if (eng.text.Contains("$"))
                    {
                        var parts = eng.text.Split('$');
                        if (parts.Length > 1)
                        {
                            symbol = parts[1].Split(' ', ',', '\n')[0].ToUpper();
                        }
                    }
                    
                    // _opManager.Stats.RecordSocialEngagement(symbol, eng.likes, eng.retweets, eng.replies, eng.text);
                }
                
                Log($"✅ {engagementData.Count} post için etkileşim verileri güncellendi.", "Stats");
            }
            catch (Exception ex)
            {
                Log($"❌ Etkileşim verisi güncellenirken hata: {ex.Message}", "Stats");
            }
        }


        private void OnSignalReceived(string content, string source)
        {
            _ = _opManager.SignalEng.ProcessRawSignal(content, source);
        }

        // Legacy batching and queue handlers removed. Handled by SignalEngine.

        private List<SignalData> ApplyFilters(List<SignalData> signals)
        {
            var cfg = ConfigManager.Current;
            var filtered = signals.Where(s => {
                // Strategy filter
                if (s.Strategy.Contains("K") && !cfg.EnableKing) return false;
                if (s.Strategy.Contains("B") && !cfg.EnableBomba) return false;
                if (s.Strategy.Contains("T") && !cfg.EnableTeFo) return false;
                if (s.Strategy == "DIP" && !cfg.EnableDip) return false;
                if (s.Strategy == "ZIRVE" && !cfg.EnableZirve) return false;
                if (s.Strategy == "ANKA" && !cfg.EnableAnka) return false;

                // Period filter
                if (s.Period == "15" && !cfg.Period15) return false;
                if (s.Period == "60" && !cfg.Period60) return false;
                if (s.Period == "240" && !cfg.Period240) return false;
                if (s.Period == "G" && !cfg.PeriodDaily) return false;

                // Score threshold
                int threshold = s.Source == "KING" ? cfg.MinScoreKing : 
                               s.Source == "DIP" ? cfg.MinScoreDip : cfg.MinScoreAnka;
                if (s.Score < threshold) return false;

                return true;
            }).ToList();

            return filtered;
        }

        private async void ProcessSignal(SignalData sig)
        {
            // Per-symbol debounce: skip if analyzed very recently
            var nowUtc = DateTime.UtcNow;
            lock (_analysisLock)
            {
                if (_lastAnalysisAt.TryGetValue(sig.Symbol, out var last) && (nowUtc - last) < AnalysisDebounceWindow)
                {
                    Log($"⏭️ Skipped (debounce): Recent analysis exists for {sig.Symbol}");
                    return;
                }
                _lastAnalysisAt[sig.Symbol] = nowUtc;
            }

            string uniqueKey = $"{DateTime.Now:yyyy-MM-dd}|{sig.Symbol}|{sig.Strategy}|{sig.Period}";
            if (_tweetedToday.Contains(uniqueKey))
            {
                Log($"⏭️ Skipped (already tweeted today): {sig.Symbol}");
                return;
            }

            Log($"📢 SIGNAL: {sig.Symbol} ({sig.Strategy}) Period:{sig.Period} Score:{sig.Score}/{sig.MaxScore}");
            
            // Performans takibine kaydet
            _opManager.Performance.RecordSignal(sig);

            if (ConfigManager.Current.AutoTweet)
            {
                // Modül: Sinyal Tweetleri (per-module toggle)
                if (ConfigManager.Current.SpamProtectSignals)
                {
                    if (!_opManager.Spam.CanTweet(sig.Symbol, sig.Strategy, out string reason))
                    {
                        Log($"🛡️ Spam protection (Sinyal): {reason}");
                        LogActivity($"🚫 RED ({sig.Symbol}): Spam Koruması - {reason}");
                        return;
                    }
                }
                
                LogActivity($"✅ ONAY ({sig.Symbol}): Spam kontrolü geçildi. İşlem başlıyor...");

                
                // Zamanlama/Gecikme KALDIRILDI (User Request: Gereksiz)

                // Symbol Mapping for TradingView (VIP/Futures)
        // User Request: VIP-THYAO -> THYAO1!, VIP-X030T -> XU030D1
        string tvSymbol = sig.Symbol;
        if (tvSymbol.StartsWith("VIP-"))
        {
             string core = tvSymbol.Replace("VIP-", "");
             if (core == "X030T") tvSymbol = "XU030D1"; 
             else tvSymbol = core + "1!";
        }
        
        // SCREENSHOT (opsiyonel) 
        string? screenshotPath = null;
        string chartId = ConfigManager.Current.TradingViewChartId;
        try
        {
            Log($"📸 Capturing chart screenshot ({tvSymbol})...");
            // Pass the mapped TV symbol to ScreenshotService
            screenshotPath = await _opManager.Screenshot.CaptureChart("BIST:" + tvSymbol, sig.Period, chartId);
        }
        catch { }

        string trends = ConfigManager.Current.DailyTrends;
        string symbolTag = _opManager.TrendService.GetSymbolHashtag(sig.Symbol);
        
        // TradingView link with Chart ID and interval
        string interval = sig.Period == "G" ? "D" : sig.Period;
        string finalTvLinkSymbol = "BIST:" + tvSymbol; // Ensure BIST prefix for URL
        
        string tvLink = string.IsNullOrEmpty(chartId) 
            ? $"https://tr.tradingview.com/chart/?symbol={finalTvLinkSymbol}&interval={interval}"
            : $"https://tr.tradingview.com/chart/{chartId}/?symbol={finalTvLinkSymbol}&interval={interval}";

                // Yüksek skorlu sinyaller için Thread
                int highScoreThreshold = sig.Source == "KING" ? 24 : sig.Source == "DIP" ? 13 : 28;
                if (sig.Score >= highScoreThreshold && !string.IsNullOrEmpty(screenshotPath))
                {
                    Log($"🧵 High score signal - posting thread...");
                    var (threadOk, _) = await _opManager.ThreadSvc.PostSignalThread(sig, screenshotPath, tvLink, trends);
                    if (threadOk)
                    {
                        _tweetedToday.Add(uniqueKey);
                        _opManager.Spam.RecordTweet("MANUAL", "MANUAL");
                        _opManager.Stats?.RecordTweet("ManualAnalysis", 4, tvLink, sig.Symbol);
                        Log("✅ Thread posted successfully!");
                        LogActivity($"🚀 YAYINLANDI ({sig.Symbol}): Thread olarak paylaşıldı (Skor: {sig.Score})");
                        // Web usage update is handled inside ThreadService or here? 
                        // It should be handled. If not, we will fix it in the next step.
                        return;
                    }
                }

                // Standart tweet
                LogActivity($"📝 AI İçerik ({sig.Symbol}): Gemini tweet yazıyor...");
                string? aiText = await _opManager.Gemini.GenerateTweetContent(sig.Symbol, sig.Price.ToString("0.00"), $"{sig.Score}/{sig.MaxScore}", sig.Strategy, trends);
                
                string finalTweet = aiText != null ? aiText.Replace("[LINK]", tvLink) :
                    $"🚀 {symbolTag} Sinyali!\n📊 {sig.Strategy} | {sig.Period}dk\n💰 Fiyat: {sig.Price:0.00}\n🔥 Skor: {sig.Score}/{sig.MaxScore}\n📈 {tvLink}\n\n{trends}";
                
                // YASAL UYARI EKLE
                finalTweet += "\n\n⚠️ Yatırım tavsiyesi değildir.";

                LogSignal($"🐦 Tweeting: {finalTweet.Substring(0, Math.Min(50, finalTweet.Length))}...");
                
                // Web First Approach for Standard Tweet
                bool sent = false;
                var webResult = await _opManager.SocialIntel.PostTweet(finalTweet);
                
                if (webResult.status == "success")
                {
                    sent = true;
                    LogSignal("✅ Tweet sent successfully (Web)!");
                }
                else
                {
                    LogSignal($"⚠️ Web tweet failed ({webResult.ErrorMessage}), trying API fallback...");
                    var res = await _opManager.Twitter.SendTweetAsync(finalTweet);
                    if (!string.IsNullOrEmpty(res))
                    {
                        sent = true;
                        // v3.7.5: Register signal with Sentinel

                    }
                }
                
                if (sent)
                {
                    _tweetedToday.Add(uniqueKey);
                    _opManager.Spam.RecordTweet(sig.Symbol, sig.Strategy);
                    if (!webResult.status.Equals("success")) LogSignal("✅ Tweet sent successfully (API Fallback)!");
                    
                    var stats = _opManager.Spam.GetStats();
                    LogSignal($"📊 Stats: {stats.hourly}/hr, {stats.daily}/day, {stats.monthly}/month");
                }
                else
                {
                    LogSignal($"❌ Tweet failed: {_opManager.Twitter.LastError}");
                }
            }
        }

        private void UpdateStatus(string status)
        {
            var lbl = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
            if (lbl != null) lbl.Text = $"Status: {status}";
        }




        private async Task PerformManualAnalysis()
        {
            string symbol = txtAnalysisSymbol.Text;
            
            if (string.IsNullOrWhiteSpace(symbol))
            {
                MessageBox.Show("Lütfen bir sembol giriniz.");
                return;
            }

            string market = cmbMarket.SelectedItem?.ToString() ?? "BIST";
            string period = cmbTimeframe.SelectedItem?.ToString() ?? "60";

            btnAnalyze.Text = "⏳ Analiz Ediliyor...";
            btnAnalyze.Enabled = false;
            rtbAnalysisResult.Text = "AI veri topluyor ve analiz ediyor (Period: " + period + ")... Lütfen bekleyin.";

            try
            {
                string periodDisplay = (period == "G" || period == "Daily") ? "Günlük" : $"{period}dk";
                LogAI($"🔍 Manuel Analiz Başlatıldı: {symbol} ({market}, {periodDisplay})");

                // Run in background to avoid freezing UI
                // Read directly from TextBox to ensure latest value is used even if not saved
                string chartId = txtTvChartId.Text.Trim(); 
                string basis = cmbBasis.SelectedItem?.ToString() ?? "TL";
                LogAI("🚀 Manuel Analiz BAŞLATILDI...");
                _lastAnalysisResult = await _opManager.ManualAnalysis.PerformManualAnalysis(symbol, market, period, chartId, basis);
                LogAI("✅ Manuel Analiz TAMAMLANDI.");
                
                if (_lastAnalysisResult != null)
                {
                    rtbAnalysisResult.Text = _lastAnalysisResult.ReportText;
                    // Auto-enable thread mode if success
                    chkPostAsThread.Checked = _lastAnalysisResult.Success;
                    
                    // Load screenshot into PictureBox if available
                    if (!string.IsNullOrEmpty(_lastAnalysisResult.ScreenshotPath) && 
                        File.Exists(_lastAnalysisResult.ScreenshotPath))
                    {
                        try
                        {
                            // Use FileStream to avoid file locking issues
                            using (var stream = new FileStream(_lastAnalysisResult.ScreenshotPath, FileMode.Open, FileAccess.Read))
                            {
                                picScreenshot.Image = Image.FromStream(stream);
                            }
                            LogAI("📷 Grafik görsel analizi yapıldı (Multimodal AI)");
                        }
                        catch (Exception imgEx)
                        {
                            LogAI($"⚠️ Görüntü yüklenemedi: {imgEx.Message}");
                            picScreenshot.Image = null;
                        }
                    }
                    else
                    {
                        picScreenshot.Image = null;
                        LogAI("ℹ️ Grafik görüntüsü alınamadı, sadece metin analizi yapıldı.");
                    }
                }
                else
                {
                    rtbAnalysisResult.Text = "Analiz başarısız (Null).";
                    picScreenshot.Image = null;
                }
                
                btnTweetAnalysis.Enabled = true;
            }
            catch (Exception ex)
            {
                rtbAnalysisResult.Text = "Hata: " + ex.Message;
            }
            finally
            {
                btnAnalyze.Text = "🤖 ANALİZ ET";
                btnAnalyze.Enabled = true;
            }
        }

        private void UpdateQuotaDisplay()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateQuotaDisplay));
                return;
            }

            // In V5.1, quotas are circular panels that need Invalidation to repaint with new values
            var cfg = ConfigManager.Current;
            
            // Update Follower Label
            if (lblFollowers != null)
                lblFollowers.Text = $"{cfg.FollowersCount}\nTAKİPÇİ";

            // Find panels in header to invalidate paints
            foreach (Control c in pnlDashHeader.Controls) {
                if (c is TableLayoutPanel tbl) {
                    foreach (Control sc in tbl.Controls) {
                        if (sc is FlowLayoutPanel flow) {
                            foreach (Control pc in flow.Controls) pc.Invalidate();
                        }
                        else if (sc is Panel pStat && pStat.Tag != null && pStat.Tag.ToString()!.Contains("api_")) {
                            sc.Invalidate();
                        }
                        // Directly check for the mini stats if they are in the table
                        sc.Invalidate(); 
                    }
                }
            }
        }

        private void Log(string msg, string category = "System")
        {
            if (this.InvokeRequired) this.Invoke(new Action(() => Log(msg, category)));
            else 
            {
                txtLog.AppendText($"[{DateTime.Now:dd.MM HH:mm:ss}] {msg}\r\n");
                
                // Route to appropriate logger
                switch (category)
                {
                    case "News": Logger.News(msg); break;
                    case "AI": Logger.AI(msg); break;
                    case "Twitter": Logger.Twitter(msg); break;
                    case "Signal": Logger.Log("Signal", msg); break; // Custom category
                    case "Social": Logger.Log("Social", msg); break; // Custom category
                    case "FanZone": Logger.FanZone(msg); break;
                    case "System": default: Logger.Sys(msg); break;
                }

                // v3.0 Live History Update
                if (pnlHistory != null && pnlHistory.Visible && _historyLogView != null)
                {
                     string filter = _historyModuleFilter?.SelectedItem?.ToString() ?? "Tümü";
                     if (filter == "Tümü" || filter.Equals(category, StringComparison.OrdinalIgnoreCase))
                     {
                         // Format should match LoadActivityHistory style somewhat
                         // LoadActivityHistory shows: [Module] [Time] Message
                         // v3.1 FIX: Insert at top to match "Newest First" order of LoadActivityHistory
                         var logLine = $"[{category}] [{DateTime.Now:HH:mm:ss}] {msg}\n";
                         try 
                         {
                             _historyLogView.Select(0, 0);
                             _historyLogView.SelectedText = logLine;
                             _historyLogView.Select(0, 0); 
                         }
                         catch { } // Ignore UI race conditions
                     }
                }
            }
        }
        
        // Helper aliases for clearer code
        private void LogNews(string msg) => Log(msg, "News");
        private void LogAI(string msg) => Log(msg, "AI");
        private void LogSignal(string msg) => Log(msg, "Signal");
        private void LogSocial(string msg)
        {
            Log(msg, "Social");
            // Also log to Bot tab if it's interaction-related
            if (rtbBotLog != null && rtbBotLog.InvokeRequired == false)
            {
                rtbBotLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                rtbBotLog.ScrollToCaret();
            }
            else if (rtbBotLog != null)
            {
                rtbBotLog.Invoke(new Action(() =>
                {
                    rtbBotLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                    rtbBotLog.ScrollToCaret();
                }));
            }
        }
        private void LogSys(string msg) => Log(msg, "System");
        
        private void UpdateBotStatus(string status)
        {
            if (lblBotStatus == null) return;
            
            if (lblBotStatus.InvokeRequired)
                lblBotStatus.Invoke(new Action(() => lblBotStatus.Text = status));
            else
                lblBotStatus.Text = status;
        }
        private async Task CheckForInteractions()
        {
            if (_opManager.Interaction != null)
            {
                UpdateBotStatus("🎯 Hedef kitle etkileşimi...");
                await _opManager.Interaction.RunTargetedCheck("BIST");
                await Task.Delay(1000 * 30);
                await _opManager.Interaction.RunTargetedCheck("CRYPTO");
                UpdateBotStatus("IDLE");
            }
            
            var cfg = ConfigManager.Current;
            if (!cfg.BotInteractionEnabled)
            {
                UpdateBotStatus("⏸️ Bot devre dışı");
                return;
            }

            try
            {
                UpdateBotStatus("🔍 Trend arıyor...");
                
                // 1. Find relevant topics from Config
                var trends = await _opManager.SocialIntel.GetTrendsAsync();
                var relevantKeywords = cfg.BotTopicKeywords.Split(',').Select(k => k.Trim().ToLowerInvariant()).Where(k => !string.IsNullOrEmpty(k)).ToArray();
                
                if (relevantKeywords.Length == 0)
                {
                    LogSocial("⚠️ Bot konuları tanımlanmamış.");
                    UpdateBotStatus("⚠️ Konu yok");
                    return;
                }
                
                string? topic = trends.FirstOrDefault(t => relevantKeywords.Any(k => t.ToLowerInvariant().Contains(k)));
                
                if (string.IsNullOrEmpty(topic))
                {
                    LogSocial("⚠️ İlgili trend bulunamadı.");
                    UpdateBotStatus("⚠️ Trend yok");
                    return;
                }
                
                UpdateBotStatus($"🔍 Trend bulundu: {topic}");
                LogSocial($"📌 Trend: {topic}");
                
                // 2. Find viral tweet with filters
                var posts = await _opManager.SocialIntel.FindInfluencerAnalyses(topic + $" min_faves:{cfg.BotMinFavorites}", SocialIntelService.DetectMarket(topic));
                
                // 3. Apply filters from Config
                var now = DateTime.Now;
                var filteredPosts = posts
                    .Where(p => !_tweetedToday.Contains(p.Url))
                     .Where(p => p.PostDate > DateTime.MinValue && (now - p.PostDate).TotalHours < cfg.BotMaxTweetAgeHours)
                    .Where(p => p.FollowerCount >= cfg.BotMinFollowers)
                    .OrderByDescending(p => p.Engagement)
                    .ToList();
                
                if (filteredPosts.Count == 0)
                {
                    LogSocial($"⚠️ Filtrelere uygun tweet bulunamadı (≥{cfg.BotMinFollowers} takipçi, <{cfg.BotMaxTweetAgeHours}h).");
                    UpdateBotStatus("⚠️ Filtre uyumsuz");
                    return;
                }
                
                var post = filteredPosts[0];
                UpdateBotStatus($"💡 AI yanıt üretiyor...");
                var reply = await _opManager.Gemini.GenerateReply(post.Content, post.Handle);
                    
                    if (!string.IsNullOrEmpty(reply))
                    {
                        int id = Interlocked.Increment(ref _interactionIdCounter);
                        _pendingInteractionDict[id] = new InteractionState { Url = post.Url, Text = reply, Time = DateTime.Now };
                        _tweetedToday.Add(post.Url); // Mark as proposed
                        UpdateBotStatus($"⏳ Telegram onayı bekleniyor [ID: {id}] (@{post.Handle})");
                        
                        string safeContent = post.Content.Replace("\"", "'");
                        string displayContent = safeContent.Length > 200 ? safeContent.Substring(0, 197) + "..." : safeContent;

                        string msg = $"🤖 *FIRSAT BULUNDU!*\n\n👤 Kullanıcı: {post.Handle} ({post.FollowerCount:N0} takipçi)\n\uD83D\uDD17 [Tweeti Görüntüle]({post.Url})\n\n📝 *Tweet İçeriği:*\n_{displayContent}_\n\n💡 *ÖNERİLEN YANIT:*\n\"{reply}\"\n\n✏️ Yanıtı düzenlemek için yeni metni yazın\n✅ Onaylamak için */ONAY {id}*\n❌ İptal için */RED {id}*";
                        await _opManager.Telegram.SendMessageAsync(msg);
                        LogSocial($"🤖 @{post.Handle} için öneri gönderildi");

                     }
                     else
                     {
                         UpdateBotStatus("⚠️ AI yanıt üretelemedi");
                     }
                 }
                 catch (Exception) {}
        }

        private async Task ProcessTelegramCommands()
        {
            if (_isTelegramProcessing) return; // v3.0.9: Don't overlap requests
            _isTelegramProcessing = true;

            try
            {
                // v3.1.1: Check if token is available
                if (string.IsNullOrEmpty(ConfigManager.Current.TelegramBotToken))
                {
                    Logger.Telegram("⚠️ Telegram Token eksik, polling atlanıyor.");
                    _isTelegramProcessing = false;
                    return;
                }

                // Force check: If offset is 0, maybe we missed initialization
                if (_lastProcessedUpdateId == 0)
                {
                    // Initialization Logic moved here to be safe
                    // But we don't want to skip everything if it's the first run ever.
                    // Let's just trust GetUpdatesAsync handles strict order.
                }

                // Process ALL pending updates using offset (Long Polling if supported, but here just fetch)
                var updates = await _opManager.Telegram.GetUpdatesAsync(_lastProcessedUpdateId + 1);
                
                if (updates == null || updates.Count == 0) return;

                foreach (var update in updates)
                {
                    // Double check to avoid repeat processing
                    if (update.UpdateId <= _lastProcessedUpdateId) continue;
                    
                    _lastProcessedUpdateId = update.UpdateId;

                    // Log to dedicated Telegram Log
                    Logger.Telegram($"📥 Mesaj Alındı (ID: {update.UpdateId}): {update.Text}");
                    
                    // v3.1: Heartbeat to System log every command to ensure it's alive
                    Log($"📥 Telegram: {update.Text}", "System");

                    string msgText = update.Text.Trim();
                    if (string.IsNullOrEmpty(msgText)) continue;
                    
                    if (!msgText.StartsWith("/")) 
                    {
                         // Non-command message, maybe log it but ignore
                         continue;
                    }

                    string[] parts = msgText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string command = parts[0].ToUpper();
                    string args = parts.Length > 1 ? parts[1] : "";

                    Logger.Telegram($"⌨️ Komut Çalıştırılıyor: {command} {args}");

                    try 
                    {
                        switch (command)
                        {
                        case "/ANALIZ":
                            if (string.IsNullOrEmpty(args))
                            {
                                 await _opManager.Telegram.SendMessageAsync("⚠️ Kullanım: `/analiz [SEMBOL]`\nÖrn: `/analiz THYAO`");
                                 return;
                            }
                            
                            string symbol = args.Split(' ')[0];
                            string period = args.Contains(" ") ? args.Split(' ')[1] : "240";
                            if (period == "G") period = "Daily";

                            string market = SymbolData.DetectMarket(symbol);
                            Logger.Telegram($"🔍 {symbol} analizi isteniyor... (Pazar: {market})");
                            await _opManager.Telegram.SendMessageAsync($"🔍 *{symbol.ToUpper()}* analizi başlatılıyor [{market} - {period}dk]...");
                            
                            _lastAnalysisSymbol = symbol.ToUpper();
                            string currentChartId = txtTvChartId.Text.Trim();
                            _lastAnalysisResult = await _opManager.ManualAnalysis.PerformManualAnalysis(symbol, market, period, currentChartId, "TL");
                            
                            if (_lastAnalysisResult.Success)
                            {
                                Logger.Telegram($"✅ {symbol} analizi tamamlandı.");
                                string report = $"📊 *ANALİZ RAPORU: {symbol.ToUpper()}*\n\n{_lastAnalysisResult.ReportText}\n\n🔗 Grafik: {_lastAnalysisResult.TvLink}\n\n🐦 Paylaşmak için: */TWEETLE*";
                                await _opManager.Telegram.SendMessageAsync(report);
                            }
                            else
                            {
                                Logger.Telegram($"❌ {symbol} analizi başarısız: {_lastAnalysisResult.ReportText}");
                                 await _opManager.Telegram.SendMessageAsync($"❌ Analiz hatası: {_lastAnalysisResult.ReportText}");
                            }
                            break;

                        case "/ARASTIR":
                            if (string.IsNullOrEmpty(args))
                            {
                                await _opManager.Telegram.SendMessageAsync("⚠️ Kullanım: `/arastir [KONU]`\nÖrn: `/arastir $THYAO son haberler`");
                                return;
                            }
                            
                            await _opManager.Telegram.SendMessageAsync($"🕵️ *{args}* araştırılıyor... Lütfen bekleyin.");
                            var research = "⚠️ Sentinel araştırma modülü devrede değil.";
                            
                            string researchMsg = $"🔍 *İSTİHBARAT RAPORU: {args}*\n\n{research}";
                            await _opManager.Telegram.SendMessageAsync(researchMsg);
                            break;

                        case "/TWEETLE":
                        case "/PAYLAS":
                        case "/THREAD":
                            // CRITICAL: Log BEFORE try block to ensure we see execution
                            Logger.Telegram($"🎯 /TWEETLE Komutu Alındı (Update ID: {update.UpdateId})");
                            Logger.Sys($"🎯 /TWEETLE Komutu Başlatılıyor...");
                            
                            try {
                                Logger.Sys($"🚀 /TWEETLE Try Bloğu İçinde. LastAnalysisSuccess: {_lastAnalysisResult?.Success ?? false}");
                                
                                if (_lastAnalysisResult == null || !_lastAnalysisResult.Success)
                                {
                                     Logger.Sys("⚠️ /TWEETLE: Geçerli analiz yok.");
                                     await _opManager.Telegram.SendMessageAsync("⚠️ Önce geçerli bir analiz yapmalısınız!");
                                     break; // ✅ FIX: 'return' yerine 'break' kullan
                                }

                                Logger.Sys($"🧵 /TWEETLE: {_lastAnalysisSymbol} için Thread hazırlanıyor...");
                                await _opManager.Telegram.SendMessageAsync($"🧵 *{_lastAnalysisSymbol}* paylaşılıyor...");
                                
                                var sig = new SignalData 
                                { 
                                    Symbol = _lastAnalysisSymbol, 
                                    Price = _lastAnalysisResult.PriceInfo?.Price ?? 0,
                                    Score = 30,
                                    Analysis = _lastAnalysisResult.ReportText,
                                    Source = "MANUEL",
                                    Strategy = "Telegram",
                                    Period = "240",
                                    Basis = "TL"
                                };
                                
                                // Enrich
                                 _opManager.ManualAnalysis.EnrichSignalWithResult(sig, _lastAnalysisResult, "TL");
                                
                                Logger.Sys("🧵 /TWEETLE: PostSignalThread çağrılıyor...");
                                var (sent, errorMsg) = await _opManager.ThreadSvc.PostSignalThread(
                                    sig, 
                                    _lastAnalysisResult.ScreenshotPath ?? "", 
                                    _lastAnalysisResult.TvLink ?? "", 
                                    ConfigManager.Current.DailyTrends, 
                                    customAnalysis: _lastAnalysisResult.ReportText, 
                                    influencerPosts: _lastAnalysisResult.InfluencerPosts
                                );
                                
                                Logger.Sys($"🧵 /TWEETLE: PostSignalThread Sonucu - Sent: {sent}, Error: {errorMsg ?? "YOK"}");
                                
                                if (sent)
                                {
                                     Logger.Sys("✅ /TWEETLE: Başarıyla gönderildi.");
                                     await _opManager.Telegram.SendMessageAsync($"✅ *PAYLAŞILDI!*");
                                     _opManager.Spam.RecordTweet("MANUAL", "MANUAL");
                                     _opManager.Stats?.RecordTweet("ManualAnalysis", 1, _lastAnalysisResult.TvLink ?? "", sig.Symbol);
                                }
                                else
                                {
                                     Logger.Sys($"❌ /TWEETLE: Gönderim hatası: {errorMsg}");
                                     await _opManager.Telegram.SendMessageAsync($"❌ Hata: {errorMsg}");
                                     Logger.Telegram($"❌ Paylaşım Hatası: {errorMsg}");
                                }
                            }
                            catch (Exception tEx)
                            {
                                Logger.Sys($"❌ /TWEETLE KRİTİK HATA: {tEx}");
                                await _opManager.Telegram.SendMessageAsync($"❌ KRİTİK HATA: {tEx.Message}");
                            }
                            break;

                        case "/DURUM":
                        case "/STATUS":
                            var cfg = ConfigManager.Current;
                            cfg.CheckReset();
                            
                            // Modül bazlı tweet sayıları
                            var tweetSnapshot = _opManager.Stats.GetTweetSnapshot(10);
                            var moduleCounts = string.Join("\n", 
                                tweetSnapshot.TweetCounts
                                    .OrderByDescending(x => x.Value)
                                    .Select(x => $"  • {x.Key}: {x.Value}"));
                            
                            if (string.IsNullOrEmpty(moduleCounts)) moduleCounts = "  (Henüz kayıt yok)";
                            
                            var statusMsg = $"🤖 *SİSTEM DURUMU*\n\n" +
                                            $"🕒 Uptime: {(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).ToString(@"hh\:mm")}\n\n" +
                                            $"📊 *BUGÜN*\n" +
                                            $"  Tweet: {cfg.DailyTotalTweetCount}\n" +
                                            $"  API: {cfg.DailyTweetCount}/{cfg.TwitterApiDailyLimit}\n" +
                                            $"  Web: {cfg.DailyTotalTweetCount - cfg.DailyTweetCount}\n\n" +
                                            $"📈 *AYLIK*\n" +
                                            $"  Tweet: {cfg.MonthlyTotalTweetCount}\n" +
                                            $"  API: {cfg.MonthlyTweetCount}/{cfg.TwitterApiMonthlyLimit}\n\n" +
                                            $"🧵 *MODÜL BAZLI*\n{moduleCounts}\n\n" +
                                            $"📥 Son Update ID: {_lastProcessedUpdateId}";
                            await _opManager.Telegram.SendMessageAsync(statusMsg);
                            break;

                        case "/ONAY_HABER":
                        case "/ONAYHABER":
                            {
                                int id = -1;
                                var cmdParts = msgText.Split(' ');
                                if (cmdParts.Length > 1) int.TryParse(cmdParts[1], out id);

                                if (id > 0)
                                {
                                    if (_pendingNewsDict.TryRemove(id, out var pending))
                                    {
                                        await _opManager.Telegram.SendMessageAsync($"⏳ {id} onaylandı. Yayınlanıyor...");
                                        try
                                        {
                                            var (success, message) = await _opManager.NewsEng.ForcePostNews(pending.item, pending.summary);
                                            if (success)
                                            {
                                                await _opManager.Telegram.SendMessageAsync($"✅ BAŞARILI! {pending.item.Title}");
                                                Logger.News($"✅ Haber başarıyla yayınlandı: {pending.item.Title}");
                                            }
                                            else
                                            {
                                                await _opManager.Telegram.SendMessageAsync($"❌ HATA: {message}");
                                                Logger.News($"❌ Haber yayınlama hatası: {message}");
                                            }
                                        }
                                        catch (Exception pEx)
                                        {
                                            Logger.News($"❌ ForcePostNews Hatası: {pEx.Message}");
                                            await _opManager.Telegram.SendMessageAsync($"❌ Kritik Hata: {pEx.Message}");
                                        }
                                    }
                                    else await _opManager.Telegram.SendMessageAsync($"⚠️ ID:{id} geçersiz.");
                                }
                                else if (_pendingNewsDict.Count == 1)
                                {
                                    var first = _pendingNewsDict.ElementAt(0);
                                    if (_pendingNewsDict.TryRemove(first.Key, out var pending))
                                    {
                                        await _opManager.Telegram.SendMessageAsync($"⏳ Onaylandı: {pending.item.Title}");
                                        try
                                        {
                                            var (success, message) = await _opManager.NewsEng.ForcePostNews(pending.item, pending.summary);
                                            if (success)
                                            {
                                                await _opManager.Telegram.SendMessageAsync($"✅ BAŞARILI!");
                                            }
                                            else
                                            {
                                                await _opManager.Telegram.SendMessageAsync($"❌ HATA: {message}");
                                            }
                                        }
                                        catch (Exception pEx)
                                        {
                                            Logger.News($"❌ ForcePostNews Hatası: {pEx.Message}");
                                            await _opManager.Telegram.SendMessageAsync($"❌ Kritik Hata: {pEx.Message}");
                                        }
                                    }
                                }
                                else await _opManager.Telegram.SendMessageAsync("⚠️ ID belirtin.");
                            }
                            break;

                        case "/RED_HABER":
                        case "/REDHABER":
                            {
                                int id = -1;
                                var cmdParts = msgText.Split(' ');
                                if (cmdParts.Length > 1) int.TryParse(cmdParts[1], out id);
                                if (id > 0 && _pendingNewsDict.TryRemove(id, out _))
                                    await _opManager.Telegram.SendMessageAsync($"❌ {id} reddedildi.");
                                else if (id <=0) _pendingNewsDict.Clear();
                            }
                            break;
                            
                        case "/ONAY":
                             {
                                int id = -1;
                                var cmdParts = msgText.Split(' ');
                                if (cmdParts.Length > 1) int.TryParse(cmdParts[1], out id);

                                if (id > 0)
                                {
                                    if (_pendingInteractionDict.TryRemove(id, out var pending))
                                    {
                                        await _opManager.Telegram.SendMessageAsync($"✅ {id} onaylandı. Yanıtlanıyor...");
                                        await _opManager.SocialIntel.ReplyToTweetAsync(pending.Url, pending.Text);
                                    }
                                    else await _opManager.Telegram.SendMessageAsync($"⚠️ ID:{id} bulunamadı.");
                                }
                             }
                             break;


                             
                        case "/RED":
                        case "/IPTAL":
                             {
                                int id = -1;
                                var cmdParts = msgText.Split(' ');
                                if (cmdParts.Length > 1) int.TryParse(cmdParts[1], out id);
                                if (id > 0)
                                {
                                    if (_pendingInteractionDict.TryRemove(id, out var p))
                                    {
                                        await _opManager.Telegram.SendMessageAsync($"❌ {id} iptal.");
                                        _tweetedToday.Remove(p.Url);
                                    }
                                    else await _opManager.Telegram.SendMessageAsync($"⚠️ ID:{id} bulunamadı.");
                                }
                             }
                             break;
                        }
                    }
                    catch (Exception commandEx)
                    {
                        Logger.Telegram($"❌ Komut Hatası ({command}): {commandEx.Message}");
                        await _opManager.Telegram.SendMessageAsync($"❌ Komut işlenirken hata oluştu: {commandEx.Message}");
                    }
                } 
            }
            catch (Exception ex)
            {
                Logger.Telegram($"❌ ProcessTelegramCommands Hatası: {ex.Message}");
            }
            finally
            {
                _isTelegramProcessing = false;
            }
        }


        private async Task CheckDMs()
        {
             try
             {
                 var dms = await _opManager.SocialIntel.GetDMsAsync();
                 if (dms != null && dms.Any())
                 {
                     foreach (var dm in dms)
                     {
                         if (!_knownDMs.Contains(dm))
                         {
                             _knownDMs.Add(dm);
                             // Forward to Telegram
                             await _opManager.Telegram.SendMessageAsync($"📩 *YENİ DM GELDİ*\n\n{dm}");
                             Log("📩 Yeni DM Telegram'a iletildi.");
                         }
                     }
                 }
             }
             catch (Exception ex) { Log($"⚠️ DM Kontrol Hatası: {ex.Message}"); }
        }

        private async Task ProcessTargetedEngagement()
        {
            var accounts = ConfigManager.Current.TargetAccounts;
            if (string.IsNullOrWhiteSpace(accounts)) return;
            
            Log("🎯 Hedef hesap etkileşimleri kontrol ediliyor...");
            var users = accounts.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            var results = await _opManager.SocialIntel.InteractTargetsAsync(users);
            foreach(var kvp in results.data)
            {
                Log($"🎯 {kvp.Key}: {kvp.Value}");
            }
        }
        private void AttachHoverEffect(Button btn, Color hoverColor, Color normalColor)
        {
            btn.MouseEnter += (s, e) => { btn.BackColor = hoverColor; };
            btn.MouseLeave += (s, e) => { btn.BackColor = normalColor; };
        }
        private void OnPostsDetected(List<InfluencerPost> posts)
        {
            if (_opManager.Memory != null && posts != null && posts.Count > 0)
            {
                int learned = 0;
                foreach (var p in posts)
                {
                    if (_opManager.Memory.Learn(p)) learned++;
                }
                if (learned > 0)
                {
                    Log($"🧠 Hafıza: {learned} yeni veri öğrenildi.", "System");
                    _opManager.Memory.Save();
                }
            }
        }

        private void InitializeDeepScanTimer()
        {
            _deepScanTimer = new System.Windows.Forms.Timer();
            _deepScanTimer.Interval = 1000 * 60 * 45; // 45 dk (Deep Scan uzun sürer, çok sık yapma)
            _deepScanTimer.Tick += async (s, e) => 
            {
               if (_opManager.SocialIntel != null) 
               {
                   Log("🧠 Hafıza Taraması (Deep Scan) tetiklendi...", "System");
                   await _opManager.SocialIntel.PerformDeepScanAsync((msg) => Log(msg, "Social"));
                   
                   // Refresh DB view if visible
                   if (this.IsHandleCreated && !this.Disposing && !this.IsDisposed)
                   {
                       this.Invoke(new Action(() => RefreshKnowledgeBaseListView()));
                   }
               }
            };
            _deepScanTimer.Start();
        }



        private void ShowAboutDialog()
        {
            string versionContent = "Sürüm bilgisi yüklenemedi.";
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        var v = root.GetProperty("version").GetString();
                        var d = root.GetProperty("lastUpdate").GetString();
                        var log = root.GetProperty("changelog"); // Array

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"🚀 X'iDeAI v{v}");
                        sb.AppendLine($"📅 Tarih: {d}");
                        sb.AppendLine("\n📝 DEĞİŞİKLİKLER:");
                        foreach (var item in log.EnumerateArray())
                        {
                            sb.AppendLine(item.GetString());
                        }
                        
                        versionContent = sb.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                versionContent = $"Hata: {ex.Message}";
            }

            MessageBox.Show(versionContent, "X'iDeAI - Hakkında", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Helper for Signal Activity Log
        private void LogActivity(string message)
        {
            if (rtbSignalLog.InvokeRequired)
            {
                rtbSignalLog.Invoke(new Action(() => LogActivity(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            rtbSignalLog.AppendText($"[{timestamp}] {message}\n");
            rtbSignalLog.ScrollToCaret();
        }

        private void InitializeManualAnalysisTab(Control parent)
        {
             var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.FromArgb(20, 20, 20) };
             panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); // Controls
             panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F)); // Screenshot (Larger)

             // Left Side (Controls)
             var leftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20) };
             
             // Market Selection
             leftPanel.Controls.Add(new Label { Text = "🌍 PİYASA SEÇİMİ:", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
             cmbMarket = new ComboBox { Width = 200, BackColor = Color.White, DropDownStyle = ComboBoxStyle.DropDownList };
             cmbMarket.Items.AddRange(new object[] { "BIST", "Kripto", "Forex", "Emtia", "Endeks", "ABD" });
             cmbMarket.SelectedIndex = 0;
             
             // Symbol Input (Moved here to reference in event)
             txtAnalysisSymbol = new TextBox { Width = 200, BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White };
             // Configure Autocomplete
             txtAnalysisSymbol.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
             txtAnalysisSymbol.AutoCompleteSource = AutoCompleteSource.CustomSource;
             
             // Event to update autocomplete source
             cmbMarket.SelectedIndexChanged += (s, e) => {
                 string market = cmbMarket.SelectedItem?.ToString() ?? "BIST";
                 var symbols = Services.SymbolData.GetSymbols(market);
                 var collection = new AutoCompleteStringCollection();
                 collection.AddRange(symbols);
                 txtAnalysisSymbol.AutoCompleteCustomSource = collection;
             };
             // Trigger initial load
             cmbMarket.SelectedIndex = 0;

             leftPanel.Controls.Add(cmbMarket);
             
             // Timeframe Selection
             leftPanel.Controls.Add(new Label { Text = "⏰ PERİYOT:", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0,10,0,0) });
             cmbTimeframe = new ComboBox { Width = 200, BackColor = Color.White, DropDownStyle = ComboBoxStyle.DropDownList };
             cmbTimeframe.Items.AddRange(new object[] { "15", "60", "240", "G", "H", "A", "Y" });
             cmbTimeframe.SelectedIndex = 3; // Default Daily (G)
             leftPanel.Controls.Add(cmbTimeframe);

             // Basis Selection
             leftPanel.Controls.Add(new Label { Text = "💵 ANALİZ BAZI:", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0, 10, 0, 0) });
             cmbBasis = new ComboBox { Width = 200, BackColor = Color.White, DropDownStyle = ComboBoxStyle.DropDownList };
             cmbBasis.Items.AddRange(new object[] { "TL", "USD", "EUR", "XU100" });
             cmbBasis.SelectedIndex = 0; // Default TL
             leftPanel.Controls.Add(cmbBasis);

             // Symbol Input Label & Add
             leftPanel.Controls.Add(new Label { Text = "🔍 SEMBOL:", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0,10,0,0) });
             leftPanel.Controls.Add(txtAnalysisSymbol);

             // Analyze Button
             btnAnalyze = new Button { Text = "📈 ANALİZ BAŞLAT", Width = 200, Height = 40, BackColor = Color.DarkOrchid, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0,20,0,0) };
             btnAnalyze.Click += async (s, e) => {
                 if (!btnAnalyze.Enabled) return;
                 btnAnalyze.Enabled = false;
                 btnAnalyze.Text = "⏳ ANALİZ YAPILIYOR...";
                 try
                 {
                     await PerformManualAnalysis();
                 }
                 finally
                 {
                     btnAnalyze.Enabled = true;
                     btnAnalyze.Text = "📈 ANALİZ BAŞLAT";
                 }
             };
             leftPanel.Controls.Add(btnAnalyze);
             
             // Result Text
             rtbAnalysisResult = new RichTextBox { Width = 350, Height = 400, BackColor = Color.FromArgb(30,30,30), ForeColor = Color.White, Font = new Font("Segoe UI", 9), Margin = new Padding(0,20,0,0), BorderStyle = BorderStyle.None };
             leftPanel.Controls.Add(rtbAnalysisResult);

             // Tweet Controls
             chkPostAsThread = new CheckBox { Text = "🧵 Zincir (Thread) Olarak Paylaş", ForeColor = Color.Cyan, AutoSize = true, Margin = new Padding(0,10,0,0) };
             leftPanel.Controls.Add(chkPostAsThread);
             
             btnTweetAnalysis = new Button { Text = "🐦 TWEET OLARAK PAYLAŞ", Width = 350, Height = 40, BackColor = Color.DodgerBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, Enabled = false, Margin = new Padding(0,10,0,0) };
             btnTweetAnalysis.Click += async (s, e) => {
                 if (!string.IsNullOrEmpty(rtbAnalysisResult.Text)) {
                    // Modül: Manuel Analiz (per-module spam check)
                    if (ConfigManager.Current.SpamProtectManual)
                    {
                        if (!_opManager.Spam.CanPostGeneral(out string reason))
                        {
                            Log($"🛡️ Spam protection (Manuel): {reason}", "Twitter");
                            MessageBox.Show($"Spam Koruması: {reason}");
                            return;
                        }
                    }
                     try
                     {
                         if (chkPostAsThread.Checked)
                         {
                             btnTweetAnalysis.Enabled = false;
                             btnTweetAnalysis.Text = "🧵 Thread Hazırlanıyor...";
                             Log("🧵 Thread modunda tweet gönderiliyor...", "Twitter");
                             
                             var analysisText = rtbAnalysisResult.Text;
                             var pInfo = _lastAnalysisResult?.PriceInfo;
                             var scrPath = _lastAnalysisResult?.ScreenshotPath;
                             // Ensure media exists; otherwise skip image to avoid upload failure
                             if (string.IsNullOrEmpty(scrPath) || !File.Exists(scrPath))
                             {
                                 Log("⚠️ Screenshot bulunamadı veya silinmiş, görselsiz gönderiliyor.", "Twitter");
                                 scrPath = null;
                             }
                             var tvLink = _lastAnalysisResult?.TvLink ?? "";
                             var influencerPosts = _lastAnalysisResult?.InfluencerPosts; // Get cached influencer posts
                             
                             // Get selected period from combobox
                             string selectedPeriod = cmbTimeframe.SelectedItem?.ToString() ?? "60";
                             
                             var sig = new SignalData 
                             { 
                                 Symbol = txtAnalysisSymbol.Text.ToUpper(), 
                                 Price = pInfo?.Price ?? 0,
                                 Score = 30, 
                                 Analysis = analysisText,
                                 Source = "MANUEL",
                                 Strategy = "Teknik Analiz",
                                 Period = selectedPeriod,
                                 Basis = cmbBasis.SelectedItem?.ToString() ?? "TL"
                             };
                             
                             _opManager.ManualAnalysis.EnrichSignalWithResult(sig, _lastAnalysisResult!, sig.Basis);
                             
                             var chartPath = scrPath ?? string.Empty;
                              var (sent, errorMsg) = await _opManager.ThreadSvc.PostSignalThread(sig, chartPath, tvLink, ConfigManager.Current.DailyTrends, customAnalysis: analysisText, influencerPosts: influencerPosts);
                              
                              if(sent)
                              {
                                  Log("✅ Thread başarıyla yayınlandı!", "Twitter");
                                  _opManager.Spam.RecordTweet("MANUAL", "MANUAL");
                                  _opManager.Stats?.RecordTweet("ManualAnalysis", 4, tvLink, analysisText);
                                  MessageBox.Show("Thread yayınlandı!");
                              }
                              else
                              {
                                  Log($"❌ Thread hatası: {errorMsg}", "Twitter");
                                  MessageBox.Show("Hata oluştu: " + errorMsg);
                              }
                         }
                         else
                         {
                             btnTweetAnalysis.Enabled = false;
                             btnTweetAnalysis.Text = "⏳ Web üzerinden gönderiliyor...";
                             Log("🐦 Tek tweet gönderiliyor (Web/Selenium)...", "Twitter");
                             
                             // Web/Selenium First
                             // Pass media path explicitly (only if file exists)
                             var mediaPath = _lastAnalysisResult?.ScreenshotPath;
                             if (!string.IsNullOrEmpty(mediaPath) && !File.Exists(mediaPath))
                             {
                                 Log($"⚠️ Medya dosyası bulunamadı, medyasız gönderilecek: {mediaPath}", "Twitter");
                                 mediaPath = null;
                             }
                             Log($"📸 Medya yolu: {mediaPath ?? "YOK"}", "Twitter");
                             
                             var res = await _opManager.SocialIntel.PostTweet(rtbAnalysisResult.Text, mediaPath);
                             
                             if (res.status == "success")
                             {
                                 Log("✅ Tweet gönderildi (Web)!", "Twitter");
                                 _opManager.Spam.RecordTweet("MANUAL", "MANUAL");
                                 _opManager.Stats?.RecordTweet("ManualAnalysis", 1, "", rtbAnalysisResult.Text);
                                 MessageBox.Show("✅ Tweet gönderildi (Web)!");
                             }
                             else
                             {
                                 Log($"⚠️ Web başarısız: {res.ErrorMessage}. API fallback deneniyor...", "Twitter");
                                 // Fallback to API
                                 btnTweetAnalysis.Text = "⚠️ API ile deneniyor...";
                                 string? sentLink = await _opManager.Twitter.SendTweetAsync(rtbAnalysisResult.Text);
                                 if(!string.IsNullOrEmpty(sentLink))
                                 {
                                     Log("✅ Tweet gönderildi (API Fallback)!", "Twitter");
                                     _opManager.Spam.RecordTweet("MANUAL", "MANUAL");

                                     MessageBox.Show("✅ Tweet gönderildi (API - Medyasız)!");
                                 }
                                 else
                                 {
                                     Log($"❌ Her iki yöntem de başarısız! Web: {res.ErrorMessage} | API: {_opManager.Twitter.LastError}", "Twitter");
                                     MessageBox.Show($"❌ Başarısız!\nWeb: {res.ErrorMessage}\nAPI: {_opManager.Twitter.LastError}");
                                 }
                             }
                         }
                     }
                     catch (Exception ex)
                     {
                         Log($"❌ Tweet gönderme sırasında kritik hata: {ex.Message}", "Twitter");
                         MessageBox.Show($"❌ Kritik Hata: {ex.Message}");
                     }
                     finally
                     {
                         btnTweetAnalysis.Enabled = true;
                         btnTweetAnalysis.Text = "🐦 TWEET OLARAK PAYLAŞ";
                     }
                 }
             };
             leftPanel.Controls.Add(btnTweetAnalysis);

             // Right Side (Screenshot - ENLARGED)
             var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.Black };
             rightPanel.Controls.Add(new Label { Text = "📸 Grafik Görüntüsü", ForeColor = Color.Yellow, Dock = DockStyle.Top });
             picScreenshot = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
             rightPanel.Controls.Add(picScreenshot);

             panel.Controls.Add(leftPanel, 0, 0);
             panel.Controls.Add(rightPanel, 1, 0);
             
             parent.Controls.Add(panel);
        }
        private string GetAppVersion()
        {
            try
            {
                var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "2.3.3";
            }
            catch { return "2.3.3"; }
        }

        private void LoadCommentedTweets()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "commented_tweets.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var urls = doc.RootElement.GetProperty("urls").EnumerateArray();
                        foreach (var url in urls)
                        {
                            string? tweetUrl = url.GetString();
                            if (!string.IsNullOrEmpty(tweetUrl))
                                _tweetedToday.Add(tweetUrl);
                        }
                    }
                    Log($"✅ {_tweetedToday.Count} yorum yapılan tweet yüklendi.");
                }
            }
            catch { /* Silent fail */ }
        }

        private void SaveCommentedTweets()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "commented_tweets.json");
                var json = JsonSerializer.Serialize(new { urls = _tweetedToday.ToList() });
                File.WriteAllText(filePath, json);
            }
            catch { /* Silent fail */ }
        }

        private DataGridView dgvGuruApproval = null!;
        
        private void InitializeBotInteractionTab(Control tab)
        {
            tab.Controls.Clear();
            // Communication Hub (v2.0)
            var tabControl = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal, ItemSize = new Size(150, 30) };

            // Inbox tab has been removed (Sentinel)


            // --- TAB 2: PENDING (APPROVAL) ---
            var tpApproval = new TabPage("📤 Onay Bekleyenler") { BackColor = Color.FromArgb(30,30,30) };
            var splitApproval = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 400, BackColor = Color.FromArgb(45,45,50) };
            
            dgvGuruApproval = new DataGridView { 
                Dock = DockStyle.Fill, BackgroundColor = Color.FromArgb(35, 35, 40), ForeColor = Color.White, BorderStyle = BorderStyle.None, ReadOnly = true,
                AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(60, 60, 60), RowTemplate = { Height = 40 }
            };
            dgvGuruApproval.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.Cyan, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvGuruApproval.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(35, 35, 40), ForeColor = Color.WhiteSmoke, SelectionBackColor = Color.FromArgb(0, 120, 215) };
            dgvGuruApproval.Columns.Add("Date", "Tarih"); dgvGuruApproval.Columns.Add("Symbol", "Sembol");
            dgvGuruApproval.Columns.Add("Snippet", "İçerik Özeti"); dgvGuruApproval.Columns.Add("Status", "Durum");
            dgvGuruApproval.Columns.Add("Content", "FullContent"); dgvGuruApproval.Columns["Content"].Visible = false;
            dgvGuruApproval.Columns.Add("ImagePath", "ImagePath"); dgvGuruApproval.Columns["ImagePath"].Visible = false;
            splitApproval.Panel1.Controls.Add(dgvGuruApproval);

            var pnlAction = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var btnApprove = new Button { Text = "✅ YAYINLA", Dock = DockStyle.Top, Height = 50, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand };
            btnApprove.Click += async (s, e) => await ApproveSelectedThread();
            var btnReject = new Button { Text = "❌ REDDET", Dock = DockStyle.Top, Height = 40, BackColor = Color.Firebrick, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0,10,0,0) };
            btnReject.Click += (s, e) => RejectSelectedThread();
            var rtbPreview = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20,20,20), ForeColor = Color.White, BorderStyle = BorderStyle.None, ReadOnly = true, Font = new Font("Consolas", 10) };
            var picPreview = new PictureBox { Dock = DockStyle.Right, Width = 400, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            
            pnlAction.Controls.AddRange(new Control[] { rtbPreview, picPreview, new Label { Text = "Önizleme:", Dock = DockStyle.Top, ForeColor = Color.Cyan }, btnReject, btnApprove });
            
            dgvGuruApproval.SelectionChanged += (s, e) => {
                if (dgvGuruApproval.SelectedRows.Count > 0) {
                    var row = dgvGuruApproval.SelectedRows[0];
                    rtbPreview.Text = row.Cells["Content"].Value?.ToString() ?? "";
                    string img = row.Cells["ImagePath"].Value?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(img) && File.Exists(img)) try { using (var fs = new FileStream(img, FileMode.Open, FileAccess.Read)) picPreview.Image = Image.FromStream(fs); } catch { }
                    else picPreview.Image = null;
                }
            };
            splitApproval.Panel2.Controls.Add(pnlAction);
            tpApproval.Controls.Add(splitApproval);
            tabControl.TabPages.Add(tpApproval);

            // --- TAB 3: CONFIG (Bot Settings) ---
            var tpConfig = new TabPage("⚙️ Bot Ayarları") { BackColor = Color.FromArgb(30, 30, 30) };
            var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            tpConfig.Controls.Add(scrollPanel);
            
            int y = 10;
            chkBotEnabled = new CheckBox { Text = "🤖 Bot Etkileşimi Aktif", Location = new Point(10, y), Width = 300, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.White, Checked = ConfigManager.Current.BotInteractionEnabled };
            chkBotEnabled.CheckedChanged += (s, e) => { ConfigManager.Current.BotInteractionEnabled = chkBotEnabled.Checked; ConfigManager.Save(); };
            scrollPanel.Controls.Add(chkBotEnabled); y += 40;

            scrollPanel.Controls.Add(new Label { Text = "📌 Konular:", Location = new Point(10, y), ForeColor = Color.Cyan, AutoSize = true }); y += 25;
            txtBotKeywords = new TextBox { Location = new Point(10, y), Width = 600, Height = 60, Multiline = true, Text = ConfigManager.Current.BotTopicKeywords, BackColor = Color.FromArgb(60,60,60), ForeColor = Color.White };
            txtBotKeywords.TextChanged += (s, e) => { ConfigManager.Current.BotTopicKeywords = txtBotKeywords.Text; ConfigManager.Save(); };
            scrollPanel.Controls.Add(txtBotKeywords); y += 80;

            lblBotStatus = new Label { Text = "⏳ Beklemede", Location = new Point(10, y), Width = 600, ForeColor = Color.Gray };
            scrollPanel.Controls.Add(lblBotStatus); y += 30;

            rtbBotLog = new RichTextBox { Location = new Point(10, y), Width = 800, Height = 200, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.LimeGreen };
            scrollPanel.Controls.Add(rtbBotLog);

            tabControl.TabPages.Add(tpConfig);
            tab.Controls.Add(tabControl);

        }


        private async Task ApproveSelectedThread()
        {
            if (dgvGuruApproval.SelectedRows.Count == 0) return;
            var row = dgvGuruApproval.SelectedRows[0];
            
            string content = row.Cells["Content"].Value?.ToString() ?? "";
            string imgPath = row.Cells["ImagePath"].Value?.ToString() ?? "";
            
            if (string.IsNullOrEmpty(content)) return;

            // Disable UI
            Log("🧵 Thread onaylandı, yayınlanıyor...", "Twitter");
            
            // Post
            var tweets = content.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim())
                                .ToList();
                                
            var result = await _opManager.SocialIntel.PostThreadAsync(tweets, imgPath);
            
            if (result != null && result.status == "success")
            {
                Log("✅ Thread başarıyla yayınlandı!", "Twitter");


                // SAFETY FIX: Check if row still exists and belongs to grid
                if (dgvGuruApproval != null && !row.IsNewRow && dgvGuruApproval.Rows.Contains(row))
                {
                    dgvGuruApproval.Rows.Remove(row);
                }
                MessageBox.Show("Thread yayınlandı!");
            }
            else
            {
                MessageBox.Show($"Hata: {result?.ErrorMessage ?? "Bilinmeyen hata"}");
            }
        }

        private void RejectSelectedThread()
        {
            if (dgvGuruApproval.SelectedRows.Count == 0) return;
            var row = dgvGuruApproval.SelectedRows[0];
            if (MessageBox.Show("Bu taslağı silmek istediğinize emin misiniz?", "Onay", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                dgvGuruApproval.Rows.Remove(row);
            }
        }
        private void AddSignalToGrid(SignalData sig, string? overrideTime = null)
        {
            if (dgvSignals == null || dgvSignals.IsDisposed) return;

            try
            {
                // Status Determination (heuristic, can be improved with event args)
                string status = "✅ Yayınlandı"; 
                // Color coding
                Color rowColor = Color.FromArgb(35, 35, 40);
                Color statusColor = Color.LimeGreen;

                string timeDisplay = overrideTime ?? DateTime.Now.ToString("HH:mm:ss");

                int idx = dgvSignals.Rows.Add(
                    timeDisplay,
                    sig.Source,
                    sig.Symbol,
                    sig.Period,
                    sig.Price.ToString("N2"),
                    sig.Score,
                    status
                );

                var row = dgvSignals.Rows[idx];
                row.DefaultCellStyle.BackColor = rowColor;
                row.Cells[6].Style.ForeColor = statusColor; // Status column

                // Auto-scroll (only for live signals)
                if (overrideTime == null && dgvSignals.Rows.Count > 0)
                    dgvSignals.FirstDisplayedScrollingRowIndex = dgvSignals.Rows.Count - 1;

                // Cleanup (Keep last 100)
                if (dgvSignals.Rows.Count > 100)
                {
                    dgvSignals.Rows.RemoveAt(0);
                }

                UpdateKPICards();
            }
            catch { }
        }

        private void UpdateKPICards()
        {
            if (_opManager?.Performance == null || lblDailySignals == null || this.IsDisposed) return;
            
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(UpdateKPICards));
                    return;
                }

                var report = _opManager.Performance.GetDailyReport(DateTime.Now);
                var overall = _opManager.Performance.GetOverallStats();

                lblDailySignals.Text = report.TotalSignals.ToString();
                
                // HitRate display
                if (overall.TotalSignals > 0)
                    lblSuccessRate.Text = $"%{overall.HitRate:N1}";
                else
                    lblSuccessRate.Text = "%--";
                
                // Trend / Top Strategy
                var topStrat = report.StrategyStats.OrderByDescending(s => s.Value.AvgReturn).FirstOrDefault();
                lblTopStrategy.Text = !string.IsNullOrEmpty(topStrat.Key) ? topStrat.Key.ToUpper() : "NÖTR";
            }
            catch { }
        }

        private void LoadSignalHistory()
        {
            if (dgvSignals == null || _opManager.Performance == null) return;

            try
            {
                dgvSignals.Rows.Clear(); // FIX: Clear old history before loading
                var history = _opManager.Performance.GetRecentSignals(100);
                foreach (var rec in history)
                {
                    var sig = new SignalData
                    {
                        Symbol = rec.Symbol,
                        Strategy = rec.Strategy,
                        Period = rec.Period,
                        Price = rec.EntryPrice,
                        Score = rec.Score,
                        Source = rec.Source,
                        DetectedAt = rec.EntryTime
                    };
                    AddSignalToGrid(sig, rec.EntryTime.ToString("HH:mm:ss"));
                }
                
                if (dgvSignals.Rows.Count > 0)
                    dgvSignals.FirstDisplayedScrollingRowIndex = dgvSignals.Rows.Count - 1;
                    
                Log($"📋 {history.Count} adet geçmiş sinyal tabloya yüklendi.", "System");
            }
            catch (Exception ex)
            {
                Log($"⚠️ Geçmiş sinyaller yüklenirken hata: {ex.Message}", "System");
            }
        }
        private void InitializeGuruPanel()
        {
            if (_guruPanelInitialized) return;
            _guruPanelInitialized = true;

            pnlGuruCenter.Padding = new Padding(15);
            
            var lblTitle = new Label { 
                Text = "👑 ÜSTAT TAKİP MERKEZİ - @EFELERiiNEFESi3", 
                Dock = DockStyle.Top, 
                Height = 50, 
                ForeColor = Color.Gold, 
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlGuruCenter.Controls.Add(lblTitle);

            var pnlActions = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(0, 5, 0, 10) };
            var btnScanHoca = new Button { 
                Text = "🔍 Hocayı Şimdi Tara", 
                Width = 180, 
                Height = 40, 
                BackColor = Color.SeaGreen, 
                ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnScanHoca.Click += async (s, e) => await ScanGuruAccountAsync();
            pnlActions.Controls.Add(btnScanHoca);

            var btnAnalyzeSelected = new Button { 
                Text = "🧠 Seçili Tabloyu Thread Yap", 
                Width = 220, 
                Height = 40, 
                Location = new Point(190, 5),
                BackColor = Color.FromArgb(0, 120, 215), 
                ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAnalyzeSelected.Click += async (s, e) => await AnalyzeSelectedGuruTableAsync();
            pnlActions.Controls.Add(btnAnalyzeSelected);

            var chkGuruAuto = new CheckBox { 
                Text = "🤖 Otomatik Takip & Thread (3s)", 
                Location = new Point(420, 15), 
                AutoSize = true, 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Checked = ConfigManager.Current.IsGuruAutomationEnabled
            };
            chkGuruAuto.CheckedChanged += (s, e) => {
                ConfigManager.Current.IsGuruAutomationEnabled = chkGuruAuto.Checked;
                ConfigManager.Save();
                Log($"🤖 Üstat Otomasyonu: {(chkGuruAuto.Checked ? "AKTİF" : "PASİF")}", "System");
            };
            pnlActions.Controls.Add(chkGuruAuto);

            pnlGuruCenter.Controls.Add(pnlActions);

            dgvGuru = new DataGridView {
                // Dock = DockStyle.Fill, // REMOVED to fix overlap issue
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(15, 125), // Manually placed below Title(50) + Actions(60) + Padding
                Size = new Size(pnlGuruCenter.Width - 30, pnlGuruCenter.Height - 140),
                BackgroundColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Vertical, // FORCE SCROLLBAR
                Font = new Font("Segoe UI", 9)
            };
            dgvGuru.BringToFront(); // Ensure it's on top
            dgvGuru.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            dgvGuru.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvGuru.ColumnHeadersHeight = 35;
            dgvGuru.GridColor = Color.FromArgb(50, 50, 50);
            dgvGuru.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            dgvGuru.DefaultCellStyle.ForeColor = Color.White;
            dgvGuru.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
            dgvGuru.DefaultCellStyle.SelectionForeColor = Color.White;
            
            // FIX: Set Alternating colors to ensure odd rows are visible
            dgvGuru.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 38);
            dgvGuru.AlternatingRowsDefaultCellStyle.ForeColor = Color.White;
            dgvGuru.RowTemplate.Height = 40;

            dgvGuru.Columns.Add("Date", "Tarih");
            dgvGuru.Columns.Add("Content", "İçerik (Özet)");
            dgvGuru.Columns.Add("Image", "Görsel Var mı?");
            dgvGuru.Columns.Add("Url", "Link");

            pnlGuruCenter.Controls.Add(dgvGuru);
            
            // Bring actions to front
            pnlActions.BringToFront();
            lblTitle.BringToFront();
        }

        private async Task ScanGuruAccountAsync()
        {
            Log("📡 @EFELERiiNEFESi3 taranıyor...", "System");
            // Pass empty symbol to SocialIntelService to fetch timeline WITHOUT symbol filtering
            var res = await _opManager.SocialIntel.FindInfluencerAnalyses("", "BIST", new List<string> { "@EFELERiiNEFESi3" });
            
            _guruPosts.Clear();
            _guruPosts.AddRange(res);

            dgvGuru.Rows.Clear();
            foreach (var post in _guruPosts)
            {
                bool hasImage = !string.IsNullOrEmpty(post.ImageUrl);
                dgvGuru.Rows.Add(post.PostDate.ToString("dd.MM HH:mm"), post.Content, hasImage ? "✅ EVET" : "❌ HAYIR", post.Url);
            }
            Log($"✅ @EFELERiiNEFESi3 tarandı, {res.Count} tweet verisi alındı. Grid Satır Sayısı: {dgvGuru.Rows.Count}", "System");
            
            if (dgvGuru.Rows.Count > 0)
            {
                dgvGuru.ClearSelection();
                dgvGuru.FirstDisplayedScrollingRowIndex = 0;
            }
        }

        private async Task AnalyzeSelectedGuruTableAsync()
        {
            if (dgvGuru.SelectedRows.Count == 0) return;
            
            var selectedIdx = dgvGuru.SelectedRows[0].Index;
            var post = _guruPosts[selectedIdx];

            if (string.IsNullOrEmpty(post.ImageUrl))
            {
                MessageBox.Show("Bu paylaşımda tablo (görsel) bulunamadı.");
                return;
            }

            Log($"🎨 @EFELERiiNEFESi3 tablosu AI ile analiz ediliyor: {post.Url}", "System");
            
            // 1. Ekran bas bas bağırsın
            var (items, tableName) = await _opManager.Gemini.ParseGuruTableFromImage(post.ImageUrl.Split(',')[0]);
            
            if (items.Count == 0)
            {
                Log("⚠️ Görselde tablo veya hisse sembolü tespit edilemedi.", "System");
                return;
            }

            Log($"✅ {items.Count} hisse tespit edildi. ({tableName}) Analizler ve grafikler hazırlanıyor...", "System");

            foreach (var (symbol, period) in items)
            {
                // Market detection for chart & price
                string marketPrefix = "BIST";
                if (symbol.EndsWith("USDT") || symbol.Length > 5) marketPrefix = "BINANCE"; // Simple heuristic
                
                // Screenshot/Chart capture
                string? chartPath = null;
                try
                {
                    Log($"📸 #{symbol} için TradingView grafiği yakalanıyor...", "System");
                    string tvSymbol = (marketPrefix == "BIST") ? $"BIST:{symbol}" : symbol;
                    chartPath = await _opManager.Screenshot.CaptureChart(tvSymbol, period, ConfigManager.Current.TradingViewChartId);
                }
                catch (Exception ex) { Log($"⚠️ Grafik yakalanamadı: {ex.Message}", "System"); }

                // Technical Context (Price info)
                string techContext = "";
                string priceContext = ""; // Explicit price context for prompt
                decimal currentPrice = 0;
                try
                {
                    var price = await _opManager.PriceFetch.GetPriceAsync(symbol, (marketPrefix == "BIST" ? "BIST" : "Kripto"));
                    if (price != null) 
                    {
                        techContext = $"GÜNCEL FİYAT: {price.FormatFullInfo()}"; // Legacy
                        priceContext = $@"BİLGİ PANCERESİ:
- Fiyat: {price.Price}
- Değişim: %{price.Change24h:N2}
- Hacim: {price.Volume24h:N0}";
                        currentPrice = price.Price;
                    }
                }
                catch { }

                // 4.5 Visual Analysis (New Phase 2 Feature)
                string visualContext = "Görsel analiz yapılamadı.";
                if (!string.IsNullOrEmpty(chartPath) && File.Exists(chartPath))
                {
                    Log($"👁️ #{symbol} grafiği yapay zeka tarafından inceleniyor...", "System");
                    visualContext = await _opManager.Gemini.AnalyzeChartImage(symbol, chartPath);
                }

                // FIX: Record Signal for Daily Report Integration
                try 
                {
                    var guruSignal = new SignalData
                    {
                        Symbol = symbol,
                        Strategy = tableName, // E.g., "EFE HMA"
                        Period = period,
                        Price = currentPrice,
                        Score = 80, // Default high score for Guru picks
                        MaxScore = 100,
                        Source = "GURU", // Explicit source
                        DetectedAt = DateTime.Now,
                        Analysis = $"Guru Taraması: {tableName} (@{post.Handle})"
                    };
                    _opManager.Performance.RecordSignal(guruSignal);
                    _opManager.Stats.RecordActivity("SignalEngine", $"GURU Signal Recorded: {symbol} ({tableName})", true);
                }
                catch (Exception ex) 
                { 
                    Log($"⚠️ Performans kaydı yapılamadı: {ex.Message}", "System"); 
                }

                Log($"✍️ #{symbol} ({period}) için görsel destekli thread hazırlanıyor...", "System");
                // Updated call signature
                var thread = await _opManager.Gemini.GenerateGuruHonoringThread(symbol, period, post.Handle, post.Url, tableName, "Efelerin Efesi", techContext, chartPath, visualContext, priceContext);
                
                if (!string.IsNullOrEmpty(thread))
                {
                    if (ConfigManager.Current.IsGuruAutomationEnabled)
                    {
                        Log($"🚀 Otomatik paylaşım aktif: #{symbol} gönderiliyor...", "Social");
                        var tweets = thread.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(x => x.Trim())
                                           .ToList();
                        var res = await _opManager.SocialIntel.PostThreadAsync(tweets, chartPath);
                        if (res != null && res.status == "success")
                        {

                            Log($"✅ #{symbol} Guru onurlandırma threadi başarıyla paylaşıldı.", "Social");
                        }
                    }
                    else
                    {
                        // Add to Approval Grid
                        string snippet = thread.Length > 50 ? thread.Substring(0, 50) + "..." : thread;
                        dgvGuruApproval.Rows.Add(DateTime.Now.ToString("HH:mm"), symbol, snippet, "Hazır (Hoca)", thread, chartPath);
                        Log($"💾 #{symbol} threadi 'Bot Etkileşim -> Onay Bekleyenler' listesine eklendi.", "Social");
                        
                        // Switch to Bot tab to show result
                        this.Invoke(new Action(() => ShowPanel(pnlBot, btnNavBot)));
                    }
                }
            }
        }
        private void InitializeFenerbahcePanel()
        {
            if (_fenerbahceInitialized) return;
            _fenerbahceInitialized = true;

            pnlFenerbahce.Controls.Clear();

            // Header (Lacivert Zemin, Sarı Yazı)
            var lblHeader = new Label { 
                Text = "FENERBAHÇE FAN ZONE 💙💛", 
                Dock = DockStyle.Top, 
                Height = 60, 
                Font = new Font("Segoe UI", 18, FontStyle.Bold), 
                ForeColor = Color.Yellow, 
                BackColor = Color.Navy, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            pnlFenerbahce.Controls.Add(lblHeader);

            // Controls Panel
            var pnlControls = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(40,40,40) };
            
            var btnStart = new Button { Text = "▶️ SALDIR", Width = 150, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Green };
            btnStart.Click += (s,e) => { 
                _opManager.FanZone.Start(); 
                MessageBox.Show("Saldır Fener! Modül devrede.", "FanZone", MessageBoxButtons.OK, MessageBoxIcon.Information); 
            };
            
            var btnStop = new Button { Text = "⏹️ MOLA", Width = 100, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Red };
            btnStop.Click += (s,e) => { 
                _opManager.FanZone.Stop(); 
                MessageBox.Show("Modül durduruldu.", "FanZone", MessageBoxButtons.OK, MessageBoxIcon.Information); 
            };

            var btnClear = new Button { Text = "🧹 Temizle", Width = 100, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            btnClear.Click += (s,e) => { dgvFenerbahce.Rows.Clear(); };

            pnlControls.Controls.Add(btnStop);
            pnlControls.Controls.Add(btnStart);
            pnlControls.Controls.Add(btnClear);
            pnlFenerbahce.Controls.Add(pnlControls);

            // Grid
            dgvFenerbahce = new DataGridView { 
                // Dock = DockStyle.Fill, // REMOVED to prevent overlap
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(10, 120), // Manually positioned below header
                Size = new Size(pnlFenerbahce.Width - 20, pnlFenerbahce.Height - 130),
                BackgroundColor = Color.MidnightBlue, 
                ForeColor = Color.Yellow,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Vertical, // Ensure vertical scroll
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells // Ensure text wrapping calls
            };
            // Header çakışmasını önlemek için Padding veya Margin yetmez (Dock fill olunca).
            // Parent panelin Padding'ini ayarlamak daha doğru olurdu ama burada grid özelliklerini iyileştiriyoruz.
            // Styling
            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
            headerStyle.BackColor = Color.Navy;
            headerStyle.ForeColor = Color.Yellow;
            headerStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgvFenerbahce.ColumnHeadersDefaultCellStyle = headerStyle;
            dgvFenerbahce.EnableHeadersVisualStyles = false;

            dgvFenerbahce.DefaultCellStyle.BackColor = Color.FromArgb(30,30,40);
            dgvFenerbahce.DefaultCellStyle.ForeColor = Color.White;
            dgvFenerbahce.DefaultCellStyle.SelectionBackColor = Color.Navy;
            dgvFenerbahce.DefaultCellStyle.SelectionForeColor = Color.Yellow;
            dgvFenerbahce.DefaultCellStyle.WrapMode = DataGridViewTriState.True; // Metin kaydırma (önemli)
            
            // Layout Fix: Grid'in panellerin altında kalmamasını sağla (Z-Order)
            // Dock=Fill olduğu için, Top panellerden sonra (Z-Order'da en altta) olmalı.
            // pnlControls ve lblHeader'ın Grid'in üstüne binmesini engellemek için Padding eklemiyoruz, Dock sistemi bunu çözer.
            // Ama 'SendToBack' ile Grid'i en alta itmek garanti çözümdür.
            
            // Grid Columns
            dgvFenerbahce.Columns.Add("Time", "Zaman");
            dgvFenerbahce.Columns.Add("Source", "Kaynak");
            dgvFenerbahce.Columns.Add("Tweet", "Tweet");
            dgvFenerbahce.Columns.Add("Reaction", "AI Tepkisi");
            dgvFenerbahce.Columns.Add("Status", "Durum"); // v3.1: Added Status Column
            
            // v3.2 UI FIX: Optimized layout for user request
            dgvFenerbahce.Columns["Time"].Width = 55;
            dgvFenerbahce.Columns["Time"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None; // Sabit
            
            dgvFenerbahce.Columns["Source"].Width = 110;
            dgvFenerbahce.Columns["Source"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None; // Sabit
            
            dgvFenerbahce.Columns["Status"].Width = 85; 
            dgvFenerbahce.Columns["Status"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None; // Sabit
            
            dgvFenerbahce.Columns["Tweet"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgvFenerbahce.Columns["Tweet"].FillWeight = 55; // Daha geniş
            
            dgvFenerbahce.Columns["Reaction"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgvFenerbahce.Columns["Reaction"].FillWeight = 45; // Biraz daha dar

            pnlFenerbahce.Controls.Add(dgvFenerbahce);
            
            // Adjust Z-Order
            dgvFenerbahce.BringToFront(); // Bring grid to front
            pnlControls.BringToFront();   // But keep controls above it
            lblHeader.BringToFront();     // And header top-most
            dgvFenerbahce.SendToBack(); // KRİTİK: Dock layout'un düzgün çalışması için Grid en arkada olmalı.

            // Event Wiring
            _opManager.FanZone.OnNewFanContent += (tweet) => {
                this.Invoke((MethodInvoker)(() => {
                    // v3.1: Added Status column handling
                    string status = "✅ Tamam";
                    if (string.IsNullOrEmpty(tweet.AIReaction)) status = "⚠️ Atlandı";
                    
                    dgvFenerbahce.Rows.Insert(0, 
                        tweet.DetectedAt.ToString("HH:mm"), 
                        tweet.Handle + (tweet.Source == "Gündem" ? " (Gündem)" : ""), 
                        tweet.Text, 
                        tweet.AIReaction,
                        status
                    );
                    if (dgvFenerbahce.Rows.Count > 100) dgvFenerbahce.Rows.RemoveAt(100);
                }));
            };
        }

    }
}

