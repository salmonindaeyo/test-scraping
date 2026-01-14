using NAV_SCRAPING.WEB.Models;
using HtmlAgilityPack;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NAV_SCRAPING.WEB.Services.Scrapers;

public class KAssetScraper : INavScraper
{
    private const string TargetUrl = "https://www.kasikornasset.com/kasset/th/mutual-fund/investment-policy/Pages/index.aspx";
    private readonly HttpClient _client;
    private readonly ILogger<KAssetScraper> _logger;

    public KAssetScraper(HttpClient client, ILogger<KAssetScraper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => "Kasikorn Asset";
    public string Url => TargetUrl;

    public async Task<List<FundData>> ScrapeAsync()
    {
        // 1. Fetch HTML
        // Use fake User-Agent
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        string html;
        try 
        {
            _logger.LogInformation("Fetching KAsset URL: {Url}", TargetUrl);
            html = await _client.GetStringAsync(TargetUrl);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error fetching KAsset URL");
             return new List<FundData>();
        }

        // 2. Parse HTML to find hidden input
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var hiddenInput = doc.GetElementbyId("hdnxx");
        if (hiddenInput == null) 
        {
            _logger.LogWarning("KAsset: hdnxx hidden input not found");
            return new List<FundData>();
        }

        string jsonVal = hiddenInput.GetAttributeValue("value", "");
        if (string.IsNullOrEmpty(jsonVal)) 
        {
            _logger.LogWarning("KAsset: hdnxx value is empty");
            return new List<FundData>();
        }

        // 3. Decode
        string decodedJson = WebUtility.HtmlDecode(jsonVal).Trim();

        // 4. Parse JSON
        List<KAssetRootObject> data;
        try
        {
            data = JsonSerializer.Deserialize<List<KAssetRootObject>>(decodedJson) ?? new List<KAssetRootObject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing KAsset JSON");
            return new List<FundData>();
        }

        _logger.LogInformation("KAsset: Found {Count} categories", data.Count);
    
        var allFunds = new List<FundData>();

        foreach (var category in data)
        {
            if (category.table_fund == null) continue;

            foreach (var raw in category.table_fund)
            {
                decimal nav = 0;
                decimal navPast = 0;
                decimal change = 0;
                decimal changePct = 0;
                
                bool hasNav = decimal.TryParse(raw.R18_NAV, out nav);
                bool hasPast = decimal.TryParse(raw.R18_NAV_PAST, out navPast);
                
                if (hasNav && hasPast && navPast != 0)
                {
                    change = nav - navPast;
                    changePct = (change / navPast) * 100;
                }

                // Format Date: website uses DD-MM-YYYY
                DateTime? dt = null;
                if(DateTime.TryParseExact(raw.R18_NAV_DATE, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsed))
                {
                    dt = parsed;
                }

                allFunds.Add(new FundData
                {
                    Source = Name,
                    Category = category.category_id,
                    FundName = raw.FND_DSC_TH,
                    ShortName = raw.FND_CD,
                    Date = dt,
                    NAV = hasNav ? nav : null,
                    Currency = !string.IsNullOrEmpty(raw.Currency) ? raw.Currency : "THB",
                    Change = change,
                    ChangePercent = changePct,
                    Offer = ParseDecimal(raw.R18_NAV_OFFER),
                    Bid = ParseDecimal(raw.R18_NAV_BID),
                    TotalNetAsset = ParseDecimal(raw.R18_TODAY_NET_ASSET)
                });
            }
        }
        return allFunds;
    }

    private decimal? ParseDecimal(string s)
    {
        if (s == "N/A" || string.IsNullOrWhiteSpace(s)) return null;
        if (decimal.TryParse(s, out var d)) return d;
        return null;
    }

    // Internal classes for JSON
    private class KAssetRootObject
    {
        public string category_id { get; set; }
        public List<KAssetRawFundData> table_fund { get; set; }
    }

    private class KAssetRawFundData
    {
        public string FND_CD { get; set; }
        public string FND_DSC_TH { get; set; } 
        public string R18_NAV_DATE { get; set; }
        public string R18_NAV { get; set; }
        public string R18_NAV_PAST { get; set; }
        public string R18_NAV_OFFER { get; set; } 
        public string R18_NAV_BID { get; set; } 
        public string R18_TODAY_NET_ASSET { get; set; }
        public string Currency { get; set; }
    }
}
