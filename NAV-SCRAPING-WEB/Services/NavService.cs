using NAV_SCRAPING.WEB.Models;

namespace NAV_SCRAPING.WEB.Services;

public class NavService
{
    private readonly IEnumerable<INavScraper> _scrapers;

    public NavService(IEnumerable<INavScraper> scrapers)
    {
        _scrapers = scrapers;
    }

    public async Task<List<FundData>> GetAllNavsAsync()
    {
        var tasks = _scrapers.Select(s => s.ScrapeAsync());
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(x => x).ToList();
    }
}
