using NAV_SCRAPING.WEB.Models;
using HtmlAgilityPack;
using System.Globalization;
using Microsoft.Extensions.Logging;
using System.Net;

namespace NAV_SCRAPING.WEB.Services.Scrapers;

public class AssetPlusScraper : INavScraper
{
    private const string TargetUrl = "https://www.assetfund.co.th/home/funds-price.aspx";
    private readonly HttpClient _client;
    private readonly ILogger<AssetPlusScraper> _logger;

    public AssetPlusScraper(HttpClient client, ILogger<AssetPlusScraper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => "Asset Plus";
    public string Url => TargetUrl;

    public async Task<List<FundData>> ScrapeAsync()
    {
        _client.DefaultRequestHeaders.Clear();
        // Mimic a browser to avoid some basic blocks, though for this site curl worked fine.
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        string html;
        try
        {
            _logger.LogInformation("Fetching Asset Plus URL: {Url}", TargetUrl);
            html = await _client.GetStringAsync(TargetUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Asset Plus URL");
            return new List<FundData>();
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var allFunds = new List<FundData>();

        // Find the main table
        // Based on analysis: <table class="table border-white">
        var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'table border-white')]");
        if (table == null)
        {
            _logger.LogWarning("Asset Plus: Data table not found");
            return allFunds;
        }

        // The structure is multiple <thead> then <tbody> pairs.
        // We can select all tbodies
        var tbodies = table.SelectNodes(".//tbody");
        if (tbodies == null)
        {
            _logger.LogWarning("Asset Plus: No tbodies found");
            return allFunds;
        }

        foreach (var tbody in tbodies)
        {
            // Find corresponding category header
            // The thead should be the immediately preceding sibling
            var thead = tbody.PreviousSibling;
            while (thead != null && (thead.Name != "thead" || thead.NodeType != HtmlNodeType.Element))
            {
                thead = thead.PreviousSibling;
            }

            string category = "General";
            if (thead != null)
            {
                var th = thead.SelectSingleNode(".//th");
                if (th != null)
                {
                    category = WebUtility.HtmlDecode(th.InnerText).Trim();
                }
            }

            var rows = tbody.SelectNodes(".//tr");
            if (rows == null) continue;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                if (cells == null || cells.Count < 10) continue;

                // Layout:
                // 0: Short Name
                // 1: Fund Name
                // 2: Currency
                // 3: Date (dd/MM/yy e.g. 12/01/69 -> 2569)
                // 4: Net Asset
                // 5: NAV
                // 6: Offer
                // 7: Bid
                // 8: Change
                // 9: Change %

                try
                {
                    var shortName = WebUtility.HtmlDecode(cells[0].InnerText).Trim();
                    var fundName = WebUtility.HtmlDecode(cells[1].InnerText).Trim();
                    var currency = WebUtility.HtmlDecode(cells[2].InnerText).Trim();
                    var dateStr = WebUtility.HtmlDecode(cells[3].InnerText).Trim();
                    
                    var navStr = WebUtility.HtmlDecode(cells[5].InnerText).Trim();
                    var changeStr = WebUtility.HtmlDecode(cells[8].InnerText).Trim();
                    var changePctStr = WebUtility.HtmlDecode(cells[9].InnerText).Trim();
                    var offerStr = WebUtility.HtmlDecode(cells[6].InnerText).Trim();
                    var bidStr = WebUtility.HtmlDecode(cells[7].InnerText).Trim();
                    var netAssetStr = WebUtility.HtmlDecode(cells[4].InnerText).Trim();

                    // Parse Date
                    // Uses Thai Buddhist Calendar Short Year (yy) = 69 (2569).
                    // CultureInfo "th-TH" should handle this.
                    DateTime? date = null;
                    if (DateTime.TryParse(dateStr, new CultureInfo("th-TH"), DateTimeStyles.None, out var dt))
                    {
                        date = dt;
                    }

                     // Parse Decimals
                    decimal? nav = ParseDecimal(navStr);
                    decimal change = ParseDecimal(changeStr) ?? 0;
                    decimal changePct = ParseDecimal(changePctStr) ?? 0;
                    decimal? offer = ParseDecimal(offerStr);
                    decimal? bid = ParseDecimal(bidStr);
                    decimal? netAsset = ParseDecimal(netAssetStr);

                    allFunds.Add(new FundData
                    {
                        Source = Name,
                        Category = category,
                        FundName = fundName,
                        ShortName = shortName,
                        Currency = currency,
                        Date = date,
                        NAV = nav,
                        Change = change,
                        ChangePercent = changePct,
                        Offer = offer,
                        Bid = bid,
                        TotalNetAsset = netAsset
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing row for {ShortName}", cells[0]?.InnerText);
                }
            }
        }

        return allFunds;
    }

    private decimal? ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "-" || s == "N/A") return null;
        // Identify formatting. "3,643,223,619.94" -> commas are thousands separators.
        // "10.6527" -> dot is decimal.
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return null;
    }
}
