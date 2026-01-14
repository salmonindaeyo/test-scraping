using NAV_SCRAPING.WEB.Models;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;

namespace NAV_SCRAPING.WEB.Services.Scrapers;

public class LHFundScraper : INavScraper
{
    private const string TargetUrl = "https://www.lhfund.co.th/MutualFund/FundNav";
    private readonly HttpClient _client;
    private readonly ILogger<LHFundScraper> _logger;

    public LHFundScraper(HttpClient client, ILogger<LHFundScraper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => "LH Fund";
    public string Url => TargetUrl;

    public async Task<List<FundData>> ScrapeAsync()
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        string html;
        try
        {
            _logger.LogInformation("Fetching LH Fund URL: {Url}", TargetUrl);
            html = await _client.GetStringAsync(TargetUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching LH Fund URL");
            return new List<FundData>();
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var allFunds = new List<FundData>();

        // Find the specific table
        var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'table-nav')]");
        if (table == null)
        {
            _logger.LogWarning("LH Fund: Data table not found");
            return allFunds;
        }

        var tbodies = table.SelectNodes(".//tbody");
        if (tbodies == null)
        {
            _logger.LogWarning("LH Fund: No tbodies found");
            return allFunds;
        }

        foreach (var tbody in tbodies)
        {
            // Extract Category
            string category = "General";
            var categoryNode = tbody.SelectSingleNode(".//tr[contains(@class, 'captions')]//h3");
            if (categoryNode != null)
            {
                category = WebUtility.HtmlDecode(categoryNode.InnerText).Trim();
            }

            var rows = tbody.SelectNodes(".//tr");
            if (rows == null) continue;

            foreach (var row in rows)
            {
                // Skip headers and category rows
                if (row.SelectSingleNode("th") != null) continue;
                if (row.Attributes["class"]?.Value?.Contains("captions") == true) continue;

                var cells = row.SelectNodes("td");
                // We expect at least 8 data columns (index 0 to 7)
                if (cells == null || cells.Count < 8) continue;

                // Index Mapping:
                // 0: Code/Name (in <a>)
                // 1: NAV
                // 2: Net Asset
                // 3: Offer
                // 4: Bid
                // 5: Change
                // 6: ChangePercent
                // 7: Date

                try
                {
                    var linkNode = cells[0].SelectSingleNode(".//a");
                    var shortName = WebUtility.HtmlDecode(linkNode?.InnerText ?? cells[0].InnerText).Trim();
                    var fundName = WebUtility.HtmlDecode(linkNode?.Attributes["title"]?.Value ?? shortName).Trim();
                    
                    // Cleanup names if they contain newlines or excessive spaces
                    shortName = System.Text.RegularExpressions.Regex.Replace(shortName, @"\s+", " ").Trim();
                    fundName = System.Text.RegularExpressions.Regex.Replace(fundName, @"\s+", " ").Trim();

                    var navStr = WebUtility.HtmlDecode(cells[1].InnerText).Trim();
                    var netAssetStr = WebUtility.HtmlDecode(cells[2].InnerText).Trim();
                    var offerStr = WebUtility.HtmlDecode(cells[3].InnerText).Trim();
                    var bidStr = WebUtility.HtmlDecode(cells[4].InnerText).Trim();
                    var changeStr = WebUtility.HtmlDecode(cells[5].InnerText).Trim();
                    var changePctStr = WebUtility.HtmlDecode(cells[6].InnerText).Trim();
                    var dateStr = WebUtility.HtmlDecode(cells[7].InnerText).Trim();

                    DateTime? date = null;
                    // Date format in HTML is dd/MM/yyyy (e.g. 13/01/2026)
                    if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        date = dt;
                    }

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
                        Currency = "THB", // Default assumption, usually THB for these local funds
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
                    _logger.LogWarning(ex, "LH Fund: Error parsing row for {ShortName}", cells[0]?.InnerText);
                }
            }
        }

        return allFunds;
    }

    private decimal? ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "-" || s == "N/A") return null;
        // Clean up any stray characters if needed, but usually just commas
        // "1,744,337,477.40"
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return null;
    }
}
