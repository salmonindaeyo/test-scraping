namespace NAV_SCRAPING.WEB.Models;

public class FundData
{
    public string Category { get; set; } = "";
    public string FundName { get; set; } = "";
    public string ShortName { get; set; } = ""; // FundCode in KAsset can be mapped here
    public decimal? NAV { get; set; }
    public decimal? TotalNetAsset { get; set; }
    public decimal? Offer { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Change { get; set; }
    public decimal? ChangePercent { get; set; }
    public DateTime? Date { get; set; }
    public string Source { get; set; } = "";
    public string Currency { get; set; } = "THB";
}
