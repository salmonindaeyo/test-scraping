using NAV_SCRAPING.WEB.Models;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;

namespace NAV_SCRAPING.WEB.Services.Scrapers;

public class DaoFundScraper : INavScraper
{
    private const string TargetUrl = "https://www.daolinvestment.co.th/mutual-fund/info/nav";
    private readonly HttpClient _client;
    private readonly ILogger<DaoFundScraper> _logger;

    public DaoFundScraper(HttpClient client, ILogger<DaoFundScraper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => "Dao Investment";
    public string Url => TargetUrl;

    public async Task<List<FundData>> ScrapeAsync()
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        string html;
        try
        {
            _logger.LogInformation("Fetching Dao Investment URL: {Url}", TargetUrl);
            html = await _client.GetStringAsync(TargetUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Dao Investment URL");
            return new List<FundData>();
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var allFunds = new List<FundData>();

        var tables = doc.DocumentNode.SelectNodes("//table[contains(@class, 'MuiTable-root')]");

        if (tables == null)
        {
             _logger.LogWarning("Dao: No tables found");
             return allFunds;
        }

        foreach (var table in tables)
        {
            // Try to find the category from the preceding header
            // Navigate up to the box container and look for the header
            string category = "Mutual Fund";
            var container = table.ParentNode?.ParentNode; 
            // table -> div(MuiBox-root css-0) -> div(MuiBox-root css-iepdu0 or similar)
            // The header is in div(MuiBox-root css-zyncma) -> span(MuiTypography-header2)
            // which is a sibling of div(MuiBox-root css-0)
            
            if (container != null)
            {
               // container is likely MuiBox-root css-iebdu0 (or similar hash)
               // Search for header inside the container but outside the table box
               var headerNode = container.SelectSingleNode(".//span[contains(@class, 'MuiTypography-header2')]");
               if (headerNode != null)
               {
                   category = WebUtility.HtmlDecode(headerNode.InnerText).Trim();
               }
            }

            var rows = table.SelectNodes(".//tr[contains(@class, 'MuiTableRow-root')]");
            if (rows == null) continue;

            foreach (var row in rows)
            {
                var nameCell = row.SelectSingleNode(".//td[contains(@class, 'fundName')]");
                if (nameCell == null) continue;
                
                // Skip header rows by checking if H6 exists (headers usually use H3 or TH)
                if (nameCell.SelectSingleNode(".//h6") == null) continue; 

                try
                {
                    // 1. Fund Code and Name
                    var linkNode = nameCell.SelectSingleNode(".//a");
                    var codeNode = linkNode?.SelectSingleNode(".//div[contains(@class, 'css-vxcmzt')]//span") 
                                   ?? linkNode?.SelectSingleNode(".//span"); 
                    var nameNode = linkNode?.SelectSingleNode(".//div[contains(@class, 'css-1kxrhf3')]");

                    string shortName = codeNode?.InnerText?.Trim() ?? "";
                    string fundName = nameNode?.InnerText?.Trim() ?? "";

                    // 2. NAV & Date
                    var navCell = row.SelectSingleNode(".//td[contains(@class, 'nav')]");
                    decimal? nav = null;
                    DateTime? date = null;

                    if (navCell != null)
                    {
                        var textNodes = navCell.SelectNodes(".//text()")
                            ?.Select(n => WebUtility.HtmlDecode(n.InnerText).Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
                        
                        if (textNodes != null)
                        {
                            foreach (var text in textNodes)
                            {
                                if (nav == null)
                                {
                                    // Try parse as decimal first
                                    var val = ParseDecimal(text);
                                    if (val != null) 
                                    {
                                        nav = val;
                                        continue;
                                    }
                                }
                                if (date == null)
                                {
                                    var dt = ParseThaiDate(text);
                                    if (dt != null)
                                    {
                                        date = dt;
                                    }
                                }
                            }
                        }
                    }

                    // 3. Sell Price
                    var sellCell = row.SelectSingleNode(".//td[contains(@class, 'sellPrice')]");
                    decimal? offer = ExtractFirstDecimal(sellCell);

                    // 4. Buy Price
                    var buyCell = row.SelectSingleNode(".//td[contains(@class, 'buyPrice')]");
                    decimal? bid = ExtractFirstDecimal(buyCell);

                    // 5. Changes
                    var changeCell = row.SelectSingleNode(".//td[contains(@class, 'changes')]");
                    decimal change = ExtractFirstDecimal(changeCell) ?? 0;

                    // 6. Total Nav
                    var totalNavCell = row.SelectSingleNode(".//td[contains(@class, 'totalNav')]");
                    decimal? totalNetAsset = ExtractFirstDecimal(totalNavCell);

                    allFunds.Add(new FundData
                    {
                        Source = Name,
                        Category = category,
                        FundName = fundName,
                        ShortName = shortName,
                        Currency = "THB",
                        Date = date,
                        NAV = nav,
                        Change = change,
                        ChangePercent = change, 
                        Offer = offer,
                        Bid = bid,
                        TotalNetAsset = totalNetAsset
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Dao: Error parsing row");
                }
            }
        }

        return allFunds;
    }
    
    private string GetCleanText(HtmlNode node)
    {
        if (node == null) return null;
        return WebUtility.HtmlDecode(node.InnerText).Trim();
    }

    private decimal? ExtractFirstDecimal(HtmlNode node)
    {
        if (node == null) return null;
        var textNodes = node.SelectNodes(".//text()");
        if (textNodes == null) return null;
        
        foreach (var textNode in textNodes)
        {
            var clean = WebUtility.HtmlDecode(textNode.InnerText).Trim();
            if (string.IsNullOrEmpty(clean) || clean == "-") continue;
            var val = ParseDecimal(clean);
            if (val != null) return val;
        }
        return null;
    }

    private decimal? ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "-" || s == "N/A") return null;
        s = s.Replace(",", "").Replace("+", ""); // Remove commas and plus signs
        // Filter out any non-numeric chars except . and -? (e.g. "USD ")
        // Simple heuristic: try parse. If fails, maybe split by space?
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return null;
    }

    private DateTime? ParseThaiDate(string dateStr)
    {
        // Format: "13 ม.ค. 69"
        try 
        {
            var parts = dateStr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;

            int day = int.Parse(parts[0]);
            string monthStr = parts[1];
            int yearShort = int.Parse(parts[2]);

            // Convert Year BE short (69) to AD full
            // 2569 -> 2026. 
            // 69 -> 2569.
            int yearBE = 2500 + yearShort;
            int yearAD = yearBE - 543;

            int month = ParseThaiMonth(monthStr);
            if (month == 0) return null;

            return new DateTime(yearAD, month, day);
        }
        catch
        {
            return null;
        }
    }

    private int ParseThaiMonth(string m)
    {
        return m switch
        {
            "ม.ค." => 1,
            "ก.พ." => 2,
            "มี.ค." => 3,
            "เม.ย." => 4,
            "พ.ค." => 5,
            "มิ.ย." => 6,
            "ก.ค." => 7,
            "ส.ค." => 8,
            "ก.ย." => 9,
            "ต.ค." => 10,
            "พ.ย." => 11,
            "ธ.ค." => 12,
            _ => 0
        };
    }
}
