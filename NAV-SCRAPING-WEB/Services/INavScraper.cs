using NAV_SCRAPING.WEB.Models;

namespace NAV_SCRAPING.WEB.Services;

public interface INavScraper
{
    string Name { get; }
    string Url { get; }
    Task<List<FundData>> ScrapeAsync();
}
