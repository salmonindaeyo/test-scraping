using NAV_SCRAPING.WEB.Services;
using Microsoft.AspNetCore.Mvc;

namespace NAV_SCRAPING.WEB.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NavController : ControllerBase
{
    private readonly NavService _navService;
    private readonly ExcelExportService _excelService;

    public NavController(NavService navService, ExcelExportService excelService)
    {
        _navService = navService;
        _excelService = excelService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _navService.GetAllNavsAsync();
        return Ok(data);
    }

    [HttpGet("download")]
    public async Task<IActionResult> DownloadExcel()
    {
        var data = await _navService.GetAllNavsAsync();
        var fileContent = _excelService.ExportToExcel(data);
        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"nav_data_{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
