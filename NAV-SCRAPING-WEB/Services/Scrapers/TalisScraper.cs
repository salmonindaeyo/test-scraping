using NAV_SCRAPING.WEB.Models;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NAV_SCRAPING.WEB.Services.Scrapers;

public class TalisScraper : INavScraper
{
    private const string NavUrlBase = "https://nav.talisam.co.th/";
    private const string NavUrl = "https://nav.talisam.co.th/index_NAV_Sum.jsp?p_lang=EN";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TalisScraper> _logger;

    public TalisScraper(IHttpClientFactory httpClientFactory, ILogger<TalisScraper> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "Talis AM";
    public string Url => NavUrl;

    public async Task<List<FundData>> ScrapeAsync()
    {
        // Use a client configured with cookies in Program.cs
        var client = _httpClientFactory.CreateClient("Talis"); 
        
        // 1) Pre-warm
        _logger.LogInformation("Talis: Pre-warming...");
        await SafeGetAsync(client, NavUrlBase);

        // 2) Fetch JSP
        _logger.LogInformation("Talis: Fetching {Url}", NavUrl);
        var html = await FetchWithAntiLockAsync(client, NavUrl, referer: "https://www.talisam.co.th/nav/");
        if (string.IsNullOrWhiteSpace(html)) 
        {
             _logger.LogWarning("Talis: HTML is empty");
             return new List<FundData>();
        }

        // 3) Parse
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables == null || tables.Count == 0) 
        {
            _logger.LogWarning("Talis: No tables found");
            return new List<FundData>();
        }

        var allRows = new List<FundData>();

        foreach (var table in tables)
        {
            var category = FindNearestCategoryText(table);
            var headerMap = BuildHeaderMap(table);
            if (headerMap.Count == 0) continue;

            var bodyRows = table.SelectNodes(".//tbody/tr");
            if (bodyRows == null) continue;

            foreach (var tr in bodyRows)
            {
                var tds = tr.SelectNodes("./td|./th");
                if (tds == null || tds.Count == 0) continue;

                var fundName = GetByHeader(tds, headerMap, "Fund Name", "กองทุน");
                var shortName = GetByHeader(tds, headerMap, "Short Name", "ชื่อย่อ");

                if (string.IsNullOrWhiteSpace(shortName) && string.IsNullOrWhiteSpace(fundName)) continue;

                var row = new FundData
                {
                    Source = Name,
                    Category = category,
                    FundName = fundName,
                    ShortName = shortName,
                    NAV = ParseDescimal(GetByHeader(tds, headerMap, "NAV", "มูลค่าหน่วยลงทุน")),
                    TotalNetAsset = ParseDescimal(GetByHeader(tds, headerMap, "Total Net Asset Value", "มูลค่าทรัพย์สินสุทธิ")),
                    Offer = ParseDescimal(GetByHeader(tds, headerMap, "Offer", "ราคาขาย")),
                    Bid = ParseDescimal(GetByHeader(tds, headerMap, "Bid", "ราคาซื้อคืน")),
                    Change = ParseDescimal(GetByHeader(tds, headerMap, "Change", "เปลี่ยนแปลง")),
                    ChangePercent = ParseDescimal(GetByHeader(tds, headerMap, "%Change", "%เปลี่ยนแปลง").Replace("%", "")),
                    Date = ParseDate(GetByHeader(tds, headerMap, "Date", "วันที่"))
                };
                allRows.Add(row);
            }
        }

        return allRows;
    }

    private decimal? ParseDescimal(string val)
    {
        val = val.Replace(",", "").Trim();
        if (decimal.TryParse(val, out var d)) return d;
        return null;
    }

    private DateTime? ParseDate(string val)
    {
        if (DateTime.TryParse(val, new CultureInfo("en-US"), DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(val, new CultureInfo("th-TH"), DateTimeStyles.None, out d)) return d;
        return null;
    }

    // Networking Helpers from original code
    private async Task<string?> SafeGetAsync(HttpClient client, string url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        var resp = await client.SendAsync(req);
        if ((int)resp.StatusCode == 423)
        {
            await Task.Delay(2000);
            return null;
        }
        return await resp.Content.ReadAsStringAsync();
    }

    private async Task<string?> FetchWithAntiLockAsync(HttpClient client, string url, string? referer = null)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, AppendCacheBuster(url, attempt));
            req.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            if (!string.IsNullOrWhiteSpace(referer))
                req.Headers.Referrer = new Uri(referer);

            var resp = await client.SendAsync(req);
            if ((int)resp.StatusCode == 423)
            {
                await Task.Delay(2000);
                continue;
            }
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }
        return null;
    }

    private string AppendCacheBuster(string url, int attempt)
    {
        var sep = url.Contains("?") ? "&" : "?";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"{url}{sep}_={ts}_{attempt}";
    }

    // Parsing Helpers
    private string FindNearestCategoryText(HtmlNode table)
    {
        var prev = table.PreviousSibling;
        while (prev != null)
        {
            var textCandidate = ExtractHeadingText(prev);
            if (!string.IsNullOrWhiteSpace(textCandidate) && IsCategoryHeading(textCandidate))
                return textCandidate;
            prev = prev.PreviousSibling;
        }
        var parent = table.ParentNode;
        while (parent != null)
        {
            var heading = parent.SelectSingleNode(".//preceding-sibling::*[self::h2 or self::h3 or self::h4 or contains(@class,'heading')][1]");
            var textCandidate = ExtractHeadingText(heading);
            if (!string.IsNullOrWhiteSpace(textCandidate) && IsCategoryHeading(textCandidate))
                return textCandidate;
            parent = parent.ParentNode;
        }
        return "";
    }

    private string ExtractHeadingText(HtmlNode? node)
    {
        if (node == null) return "";
        var t = HtmlEntity.DeEntitize(node.InnerText ?? "").Trim();
        t = Regex.Replace(t, @"\s+", " ");
        return t;
    }

    private bool IsCategoryHeading(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string[] keywords = { "Money Market", "Mixed Fund", "Equity Fund", "Foreign Investment Fund", "Retirement Mutual Fund", "Super Saving Fund", "Thai ESG", "กองทุนรวมตลาดเงิน", "กองทุนรวมผสม", "กองทุนหุ้น", "กองทุนรวมที่ลงทุนในต่างประเทศ", "กองทุนรวมเพื่อการเลี้ยงชีพ", "กองทุนรวมเพื่อการออม", "กองทุนรวมไทยเพื่อความยั่งยืน" };
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private Dictionary<int, string> BuildHeaderMap(HtmlNode table)
    {
        var map = new Dictionary<int, string>();
        var headerRow = table.SelectSingleNode(".//thead/tr") ?? table.SelectSingleNode(".//tr[1]");
        var cells = headerRow?.SelectNodes("./th|./td");
        if (cells == null) return map;

        for (int i = 0; i < cells.Count; i++)
        {
            var name = HtmlEntity.DeEntitize(cells[i].InnerText ?? "").Trim();
            name = Regex.Replace(name, @"\s+", " ");
            map[i] = name;
        }
        return map;
    }

    private string GetByHeader(HtmlNodeCollection tds, Dictionary<int, string> headerMap, params string[] keys)
    {
        foreach (var kv in headerMap)
        {
            if (keys.Any(k => kv.Value.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                if (kv.Key < tds.Count)
                {
                    var val = HtmlEntity.DeEntitize(tds[kv.Key].InnerText ?? "").Trim();
                    return Regex.Replace(val, @"\s+", " ");
                }
            }
        }
        return "";
    }
}
