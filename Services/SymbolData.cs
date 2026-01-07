using System.Collections.Generic;

namespace XiDeAI_Pro.Services
{
    public static class SymbolData
    {
        public static string[] GetSymbols(string market)
        {
            return market switch
            {
                "Kripto" => CryptoSymbols,
                "BIST" => BistSymbols,
                "Forex" => ForexSymbols,
                "Emtia" => EmtiaSymbols,
                "Endeks" => EndeksSymbols,
                "ABD" => AbdSymbols,
                _ => new string[0]
            };
        }

        private static readonly string[] CryptoSymbols = new[]
        {
            // Top 10 - English + Turkish
            "BTCUSDT", "Bitcoin", "ETHUSDT", "Ethereum", "BNBUSDT", "BNB", "XRPUSDT", "Ripple", "SOLUSDT", "Solana", 
            "ADAUSDT", "Cardano", "DOGEUSDT", "Dogecoin", "AVAXUSDT", "Avalanche", "DOTUSDT", "Polkadot", "TRXUSDT", "Tron",
            // Top 20-50
            "MATICUSDT", "Polygon", "LINKUSDT", "Chainlink", "LTCUSDT", "Litecoin", "SHIBUSDT", "Shiba Inu", "UNIUSDT", "Uniswap", 
            "BCHUSDT", "Bitcoin Cash", "ATOMUSDT", "Cosmos", "XLMUSDT", "Stellar", "ICPUSDT", "Internet Computer", "HBARUSDT", "Hedera",
            "ETCUSDT", "Ethereum Classic", "CROUSDT", "Cronos", "FILUSDT", "Filecoin", "NEARUSDT", "NEAR", "VETUSDT", "VeChain", 
            "ALGOUSDT", "Algorand", "QNTUSDT", "Quant", "GRTUSDT", "The Graph", "APEUSDT", "Apecoin", "SANDUSDT", "Sandbox",
            // Popular Alts & DeFi
            "MANAUSDT", "Decentraland", "AXSUSDT", "Axie Infinity", "THETAUSDT", "Theta", "EGLDUSDT", "Elrond", 
            "EOSUSDT", "EOS", "AAVEUSDT", "Aave", "FLOWUSDT", "Flow", "XTZUSDT", "Tezos", "CHZUSDT", "Chiliz", "MKRUSDT", "Maker",
            // Popular Alts
            "FETUSDT", "Fetch AI", "AGIXUSDT", "SingularityNET", "INJUSDT", "Injective", "RNDRUSDT", "Render", 
            "ARBUSDT", "Arbitrum", "OPUSDT", "Optimism", "SUIUSDT", "Sui", "APTUSDT", "Aptos", "PEPEUSDT", "Pepe", "FLOKIUSDT", "Floki",
            "LDOUSDT", "Lido DAO", "SNXUSDT", "Synthetix", "CRVUSDT", "Curve", "COMPUSDT", "Compound", "KAVAUSDT", "Kava", 
            "RUNEUSDT", "Thorchain", "FTMUSDT", "Fantom", "ZILUSDT", "Zilliqa", "ENJUSDT", "Enjin", "BATUSDT", "BAT",
            "ZECUSDT", "Zcash", "DASHUSDT", "Dash", "NEOUSDT", "NEO", "XMRUSDT", "Monero", "IOTAUSDT", "IOTA", 
            "HOTUSDT", "Holotoken", "CELOUSDT", "Celo", "ANKRUSDT", "Ankr", "RVNUSDT", "Ravencoin", "WAVESUSDT", "Waves",
            "KSMUSDT", "KSM", "LUNAUSDT", "Luna", "LUNCUSDT", "Lunc", "AUDIOUSDT", "Audius", "GMTUSDT", "GMT", 
            "GALAUSDT", "Gala", "MINAUSDT", "Mina", "IMXUSDT", "Immutable", "STXUSDT", "Stacks", "TWTUSDT", "Twitter",
            "1INCHUSDT", "1inch", "ROSEUSDT", "Oasis", "IOTXUSDT", "IoTeX", "JASMYUSDT", "Jasmy", "DARUSDT", "Dar", 
            "TLMUSDT", "Tellor", "ALICEUSDT", "Alice", "SLPUSDT", "Smooth Love", "ILVUSDT", "Illuvium", "YGGUSDT", "Yield Guild"
        };

        private static readonly string[] BistSymbols = new[]
        {
            // Ana Endeksler (Main Indices)
            "XU100", "Borsa İstanbul 100", "XU030", "BIST 30", "XU050", "BIST 50", "XBANK", "Bankalar Endeksi", "XUSIN", "Sanayi Endeksi", "XUTEK", "Teknoloji Endeksi", "XGMYO", "Gayrimenkul Endeksi", "XELKT", "Elektrik Endeksi", "XMANA", "Madenler Endeksi",
            // BIST 100 & Popular
            "THYAO", "Turkish Airlines", "GARAN", "Garanti Bankası", "AKBNK", "Akbank", "ISCTR", "İşbank", "YKBNK", "Yapı Kredi", "VAKBN", "Vakıfbank", "HALKB", "Halkbank", "EREGL", "Ereğli Demir", "ASELS", "Aselsan", "SISE", "Sisecam",
            "KCHOL", "Koç Holding", "SAHOL", "Sabancı Holding", "TUPRS", "TÜPRAŞ", "BIMAS", "BİM", "TCELL", "Türkcell", "TTKOM", "Telekom", "PETKM", "Petkim", "KOZAL", "Koza Altın", "KOZAA", "Koza Aladağ", "IPEKE", "İpekyolu",
            "KRDMD", "Kardelen Madencilik", "PGSUS", "Pegasus", "TAVHL", "TAV Havalimanları", "ARCLK", "Arçelik", "VESTL", "Vestel", "ENKAI", "Enka", "TOASO", "Tofaş", "FROTO", "Froto", "OTKAR", "Otokar", "TTRAK", "Türk Traktör",
            "DOAS", "Doğuş", "SOKM", "Socar", "MGROS", "Migros", "AEFES", "Anadolu Efes", "CCOLA", "Coca-Cola", "ULKER", "Ülker", "GUBRF", "Gübre", "HEKTS", "Hektor", "SASA", "SASA Polyester", "KONTR", "Kontrol",
            "SMRTG", "Smartek", "GESAN", "Gesan", "EUREN", "Eurener", "ALARK", "Alarko", "ODAS", "Ödaş", "ZOREN", "Zoreran", "AKSEN", "Aksen", "CANTE", "Cantaş", "AYDEM", "Aydın Elektrik", "GWIND", "Genel Rüzgar"
        };

        private static readonly string[] ForexSymbols = new[]
        {
            // Major Pairs - English + Turkish
            "EURUSD", "Euro/Dolar", "GBPUSD", "Sterlin/Dolar", "USDJPY", "Dolar/Yen", "USDCHF", "Dolar/Frank", "USDCAD", "Dolar/Kanada Doları", "AUDUSD", "Avusturya Doları/Dolar", "NZDUSD", "Yeni Zelanda Doları/Dolar",
            // EUR Pairs
            "EURGBP", "Euro/Sterlin", "EURJPY", "Euro/Yen", "EURCHF", "Euro/Frank", "EURCAD", "Euro/Kanada Doları", "EURAUD", "Euro/Avusturya Doları", "EURNZD", "Euro/Yeni Zelanda Doları",
            // GBP Pairs
            "GBPJPY", "Sterlin/Yen", "GBPCHF", "Sterlin/Frank", "GBPCAD", "Sterlin/Kanada Doları", "GBPAUD", "Sterlin/Avusturya Doları", "GBPNZD", "Sterlin/Yeni Zelanda Doları",
            // Other Pairs
            "CADJPY", "Kanada Doları/Yen", "CHFJPY", "Frank/Yen", "AUDJPY", "Avusturya Doları/Yen", "NZDJPY", "Yeni Zelanda Doları/Yen", "AUDCAD", "Avusturya Doları/Kanada Doları", "AUDCHF", "Avusturya Doları/Frank",
            // Precious Metals
            "XAUUSD", "Altın/Dolar", "XAGUSD", "Gümüş/Dolar", 
            // Energy
            "WTI", "WTI Petrol", "BRENT", "Brent Petrol", "NATGAS", "Doğalgaz"
        };

        private static readonly string[] EmtiaSymbols = new[]
        {
            // Precious Metals (Kıymetli Metaller)
            "XAUUSD", "Altın/Dolar", "XAGUSD", "Gümüş/Dolar", "XPTUSD", "Platinum/Dolar", "XPDUSD", "Paladium/Dolar", "GOLD", "Altın", "SILVER", "Gümüş", "PLATINUM", "Platinum", "PALLADIUM", "Paladium",
            // Energy (Enerji)
            "WTI", "WTI Petrol", "BRENT", "Brent Petrol", "NATGAS", "Doğalgaz", "USOIL", "US Petrol", "UKOIL", "UK Petrol", "NGAS", "Doğalgaz",
            // Industrial Metals (Endüstriyel Metaller)
            "COPPER", "Bakır", "ALUMINUM", "Alüminyum", "ZINC", "Çinko", "NICKEL", "Nikel", "LEAD", "Kurşun", "TIN", "Kalay", "COBALT", "Kobalt",
            // Agricultural - Grains (Tarımsal - Tahıllar)
            "WHEAT", "Buğday", "CORN", "Mısır", "SOYBEAN", "Soya Fasulyesi", "OAT", "Avena", "CANOLA", "Kanola", "RICE", "Pirinç",
            // Agricultural - Soft Commodities (Tarımsal - Yumuşak Emtialar)
            "SUGAR", "Şeker", "COFFEE", "Kahve", "COCOA", "Kakao", "COTTON", "Pamuk",
            // Livestock (Hayvansal Ürünler)
            "CATTLE", "Sığır", "HOG", "Domuz", "LUMBER", "Kereste",
            // Other (Diğer)
            "CARBON", "Karbon", "URANIUM", "Uranyum", "LITHIUM", "Lityum"
        };

        private static readonly string[] EndeksSymbols = new[]
        {
            // Türkiye (Turkey)
            "XU100", "XU100", "XU030", "XU030", "XU050", "XU050", "XBANK", "Bankalar", "XUSIN", "Sanayi", "XUTEK", "Teknoloji", "XGMYO", "Gayrimenkul", "XELKT", "Elektrik", "XMANA", "Madenler", "XHOLD", "Holding", "XULAS", "Ulaştırma",
            
            // ABD (United States)
            "SPX", "S&P500", "SP500", "S&P500", "NASDAQ", "NASDAQ", "NDX", "Nasdaq100", "NASDAQ100", "Nasdaq100", "DJI", "Dow Jones", "DOW", "Dow Jones", "DOWJONES", "Dow Jones", "RUT", "Russell 2000", "RUSSELL2000", "Russell 2000", 
            "VIX", "Volatilite Endeksi", "NYA", "NYSE Composite", "XLF", "Finansal", "XLE", "Enerji", "XLK", "Teknoloji", "XLV", "Sağlık", "XLI", "Sanayi", "XLP", "Tüketici Ürünleri", "XLY", "Tüketici Seçimi", "XLU", "Kamu Hizmetleri", "XLRE", "Gayrimenkul",
            
            // Avrupa (Europe)
            "DAX", "DAX30", "DAX40", "DAX40", "FTSE", "FTSE100", "FTSE100", "FTSE100", "CAC40", "CAC40", "STOXX50", "Euro Stoxx 50", "EUROSTOXX50", "Euro Stoxx 50", "SMI", "Swiss Market Index", "IBEX35", "IBEX35", 
            "AEX", "Amsterdam Exchange", "BEL20", "Belçika 20", "OMX", "OMX Baltic", "ATX", "Vienna Stock Exchange", "PSI20", "Lizbon 20", "ISEQ", "İrlanda",
            
            // Asya-Pasifik (Asia-Pacific)
            "NIKKEI", "Nikkei 225", "NIKKEI225", "Nikkei 225", "TOPIX", "Tokyo Price Index", "HANGSENG", "Hong Kong 50", "HSI", "Hong Kong 50", "KOSPI", "Kore Composite", "KOSDAQ", "Kore Tech", "SHANGHAI", "Shanghai Composite", "SHCOMP", "Shanghai Composite",
            "CSI300", "China A50", "SZSE", "Shenzhen", "TWII", "Tayvan Ağır", "TAIWAN", "Tayvan", "SENSEX", "India 50", "NIFTY", "India Nifty", "NIFTY50", "India Nifty 50", "ASX200", "Avusturya 200", "NZX50", "Yeni Zelanda 50",
            "JAKARTA", "Jakarta Borsası", "SET", "Tayland SET", "KLSE", "Malezya Borsası", "PSE", "Filipin Borsası", "VNINDEX", "Vietnam Endeksi"
        };

        private static readonly string[] AbdSymbols = new[]
        {
            // Mega Cap Tech (FAANG+)
            "AAPL", "Apple", "MSFT", "Microsoft", "GOOGL", "Google", "GOOG", "Google", "AMZN", "Amazon", "META", "Meta", "NVDA", "NVIDIA", "TSLA", "Tesla", "NFLX", "Netflix",
            
            // Tech Giants
            "AMD", "AMD", "INTC", "Intel", "QCOM", "Qualcomm", "AVGO", "Broadcom", "ADBE", "Adobe", "CRM", "Salesforce", "ORCL", "Oracle", "CSCO", "Cisco", "IBM", "IBM", "NOW", "ServiceNow", "INTU", "Intuit",
            "TXN", "Texas Instruments", "AMAT", "Applied Materials", "MU", "Micron", "LRCX", "Lam Research", "KLAC", "KLA", "SNPS", "Synopsys", "CDNS", "Cadence", "MCHP", "Microchip", "ADI", "Analog Devices", "NXPI", "NXP",
            
            // Semiconductors
            "ASML", "ASML", "TSM", "TSMC", "ARM", "Arm", "MRVL", "Marvell", "ON", "ON Semiconductor", "MPWR", "Monolithic Power", "SWKS", "Skyworks", "QRVO", "Qorvo", "GFS", "Globalfoundries",
            
            // Software & Cloud
            "SNOW", "Snowflake", "DDOG", "Datadog", "PLTR", "Palantir", "CRWD", "CrowdStrike", "PANW", "Palo Alto", "ZS", "Zscaler", "NET", "Cloudflare", "OKTA", "Okta", "MDB", "MongoDB", "WDAY", "Workday", "TEAM", "Atlassian",
            "DKNG", "DraftKings", "RBLX", "Roblox", "U", "Unity", "PATH", "UiPath", "BILL", "Bill.com", "S", "Sprinklr", "DOCN", "DigitalOcean", "GTLB", "Gtlb",
            
            // E-Commerce & Consumer
            "SHOP", "Shopify", "BABA", "Alibaba", "JD", "JD.com", "PDD", "PinDuoDuo", "MELI", "Mercado Libre", "ETSY", "Etsy", "EBAY", "eBay", "W", "Wayfair",
            
            // Social Media & Communication
            "SNAP", "Snapchat", "PINS", "Pinterest", "TWTR", "Twitter", "SPOT", "Spotify", "MTCH", "Match Group", "BMBL", "Bumble",
            
            // FinTech & Payments
            "V", "Visa", "MA", "Mastercard", "PYPL", "PayPal", "SQ", "Square", "COIN", "Coinbase", "AFRM", "Affirm", "SOFI", "SoFi", "UPST", "Upstart", "NU", "Nu", "HOOD", "Robinhood",
            
            // Banks & Finance
            "JPM", "JP Morgan", "BAC", "Bank of America", "WFC", "Wells Fargo", "C", "Citigroup", "GS", "Goldman Sachs", "MS", "Morgan Stanley", "BLK", "BlackRock", "SCHW", "Schwab", "USB", "US Bancorp", "PNC", "PNC Financial", "TFC", "Truist", "BK", "Bank of New York",
            
            // Healthcare & Pharma
            "UNH", "UnitedHealth", "JNJ", "Johnson & Johnson", "PFE", "Pfizer", "ABBV", "AbbVie", "TMO", "Thermo Fisher", "LLY", "Eli Lilly", "MRK", "Merck", "ABT", "Abbott", "DHR", "Danaher", "AMGN", "Amgen", "GILD", "Gilead",
            "BMY", "Bristol Myers", "VRTX", "Vertex", "REGN", "Regeneron", "ISRG", "Intuitive Surgical", "CVS", "CVS Health", "CI", "Cigna", "HUM", "Humana", "BIIB", "Biogen", "MRNA", "Moderna", "BNTX", "BioNTech",
            
            // Biotech
            "ILMN", "Illumina", "ALNY", "Alnylam", "CRSP", "CRISPR", "NTLA", "Intellia", "BEAM", "Beam Therapeutics", "EDIT", "Editas", "SGEN", "Seagen", "BGNE", "BeiGene",
            
            // EV & Automotive
            "F", "Ford", "GM", "General Motors", "RIVN", "Rivian", "LCID", "Lucid", "NIO", "NIO", "XPEV", "XPeng", "LI", "Li Auto", "FSR", "Fisker",
            
            // Energy
            "XOM", "ExxonMobil", "CVX", "Chevron", "COP", "ConocoPhillips", "SLB", "Schlumberger", "EOG", "EOG Resources", "MPC", "Marathon Petroleum", "PSX", "Phillips 66", "VLO", "Valero", "OXY", "Occidental", "HAL", "Halliburton",
            
            // Industrials
            "BA", "Boeing", "CAT", "Caterpillar", "HON", "Honeywell", "UPS", "UPS", "DE", "Deere", "LMT", "Lockheed Martin", "RTX", "Raytheon", "GE", "General Electric", "MMM", "3M", "EMR", "Emerson",
            
            // Consumer Goods
            "PG", "Procter Gamble", "KO", "Coca-Cola", "PEP", "PepsiCo", "WMT", "Walmart", "COST", "Costco", "TGT", "Target", "NKE", "Nike", "SBUX", "Starbucks", "MCD", "McDonalds", "DIS", "Disney", "HD", "Home Depot", "LOW", "Lowes",
            "TJX", "TJX Companies", "DG", "Dollar General", "DLTR", "Dollar Tree", "ROST", "Ross Stores", "LULU", "Lululemon", "DECK", "Decathlon",
            
            // Media & Entertainment
            "CMCSA", "Comcast", "CHTR", "Charter", "PARA", "Paramount", "WBD", "Warner Bros", "LYV", "Live Nation", "NWSA", "News Corp", "FOXA", "Fox",
            
            // Travel & Hospitality
            "ABNB", "Airbnb", "BKNG", "Booking", "EXPE", "Expedia", "UBER", "Uber", "LYFT", "Lyft", "DAL", "Delta", "UAL", "United", "AAL", "American Airlines", "LUV", "Southwest", "MAR", "Marriott", "HLT", "Hilton",
            
            // REITs
            "AMT", "American Tower", "PLD", "Prologis", "CCI", "Crown Castle", "EQIX", "Equinix", "PSA", "Public Storage", "SPG", "Simon Property", "O", "Realty Income", "WELL", "Welltower", "DLR", "Digital Realty", "AVB", "AvalonBay",
            
            // Utilities
            "NEE", "NextEra Energy", "DUK", "Duke Energy", "SO", "Southern Company", "D", "Dominion", "AEP", "American Electric Power", "EXC", "Exelon", "SRE", "Sempra", "PCG", "PG&E",
            
            // Materials
            "LIN", "Linde", "APD", "Air Products", "SHW", "Sherwin-Williams", "ECL", "Ecolab", "NEM", "Newmont", "FCX", "Freeport-McMoRan", "NUE", "Nucor", "ALB", "Albemarle",
            
            // Defense & Aerospace
            "NOC", "Northrop Grumman", "GD", "General Dynamics", "LHX", "L3Harris", "HII", "Huntington Ingalls", "TDG", "TransDigm", "LDOS", "Leidos",
            
            // Agriculture
            "ADM", "ADM", "BG", "Bunge", "CF", "CF Industries", "MOS", "Mosaic", "NTR", "Nutrien", "FMC", "FMC",
            
            // Special Situations
            "BRK.A", "Berkshire Hathaway A", "BRK.B", "Berkshire Hathaway B", "SPCE", "Virgin Galactic", "GRAB", "Grab", "OPEN", "Open", "CPNG", "Coupang", "DIDI", "DiDi"
        };
    }
}
