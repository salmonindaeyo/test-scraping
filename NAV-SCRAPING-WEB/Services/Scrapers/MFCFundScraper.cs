using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAV_SCRAPING.WEB.Models;

namespace NAV_SCRAPING.WEB.Services.Scrapers;

public class MFCFundScraper : INavScraper
{
    private const string ApiUrl = "https://did-web.mfcfund.com/webservice_app/api/nav/GetNAV_FundType/Fund/Mobile%20App?_type=All";
    private const string WebUrl = "https://mfcfund.com/unit-value/";
    
    private readonly HttpClient _client;
    private readonly ILogger<MFCFundScraper> _logger;

    public MFCFundScraper(HttpClient client, ILogger<MFCFundScraper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => "MFC Fund";
    public string Url => WebUrl;

    public async Task<List<FundData>> ScrapeAsync()
    {
        var allFunds = new List<FundData>();

        try
        {
            _logger.LogInformation("Fetching MFC Fund data from API: {Url}", ApiUrl);

            // The API expects a POST request, even with no body
            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json"); 
            // Or just empty content with length 0
            
            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            
            var apiResult = JsonSerializer.Deserialize<MfcApiResponse>(json);

            if (apiResult?.NAVFund == null)
            {
                _logger.LogWarning("MFC Fund: API returned no data or invalid format");
                return allFunds;
            }

            foreach (var stock in apiResult.NAVFund)
            {
                try
                {
                    // Parse Date "13/01/2026"
                    DateTime? date = null;
                    if (DateTime.TryParseExact(stock.NAVDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        date = dt;
                    }

                    // Parse Decimal fields (API returns numbers as JSON numbers, but they might be null?)
                    // The example JSON showed them as numbers: "NAV": 2.748
                    
                    decimal? netAsset = null;
                    if (decimal.TryParse(stock.TotalAssetSize, NumberStyles.Any, CultureInfo.InvariantCulture, out var nas))
                    {
                        netAsset = nas;
                    }

                    allFunds.Add(new FundData
                    {
                        Source = Name,
                        Category = "Mutual Fund", // API doesn't seem to explicitly categorize in the list, defaulting
                        FundName = stock.FundNameTH?.Trim(),
                        ShortName = stock.FundCode?.Trim(),
                        Currency = "THB",
                        Date = date,
                        NAV = stock.NAV,
                        Change = stock.NAVChange,
                        ChangePercent = stock.NAVPercentChange,
                        Offer = stock.NAVBuy,
                        Bid = stock.NAVSell,
                        TotalNetAsset = netAsset
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MFC Fund: Error parsing item {Code}", stock.FundCode);
                }
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching MFC Fund data");
        }

        return allFunds;
    }

    // Classes for JSON Deserialization
    private class MfcApiResponse
    {
        [JsonPropertyName("NAVFund")]
        public List<MfcFundItem>? NAVFund { get; set; }
    }

    private class MfcFundItem
    {
        [JsonPropertyName("FundCode")]
        public string? FundCode { get; set; }

        [JsonPropertyName("FundNameTH")]
        public string? FundNameTH { get; set; }
        
        [JsonPropertyName("NAV")]
        public decimal? NAV { get; set; }

        [JsonPropertyName("NAV_Buy")]
        public decimal? NAVBuy { get; set; }

        [JsonPropertyName("NAV_Sell")]
        public decimal? NAVSell { get; set; }

        [JsonPropertyName("NAV_Change")]
        public decimal? NAVChange { get; set; }

        [JsonPropertyName("NAV_Percent_Change")]
        public decimal? NAVPercentChange { get; set; }

        [JsonPropertyName("NAVDate")]
        public string? NAVDate { get; set; }

        [JsonPropertyName("Total_AssetSize")]
        public string? TotalAssetSize { get; set; } // JSON string in example: "25356468.13"
    }
}
