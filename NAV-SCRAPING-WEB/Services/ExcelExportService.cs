using NAV_SCRAPING.WEB.Models;
using ClosedXML.Excel;

namespace NAV_SCRAPING.WEB.Services;

public class ExcelExportService
{
    public byte[] ExportToExcel(List<FundData> data)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("NAV Data");

        // Header
        worksheet.Cell(1, 1).Value = "Source";
        worksheet.Cell(1, 2).Value = "Category";
        worksheet.Cell(1, 3).Value = "Fund Name";
        worksheet.Cell(1, 4).Value = "Short Name";
        worksheet.Cell(1, 5).Value = "Date";
        worksheet.Cell(1, 6).Value = "NAV";
        worksheet.Cell(1, 7).Value = "Change";
        worksheet.Cell(1, 8).Value = "% Change";
        worksheet.Cell(1, 9).Value = "Bid";
        worksheet.Cell(1, 10).Value = "Offer";
        worksheet.Cell(1, 11).Value = "Total Net Asset";

        for (int i = 0; i < data.Count; i++)
        {
            var row = i + 2;
            var item = data[i];
            worksheet.Cell(row, 1).Value = item.Source;
            worksheet.Cell(row, 2).Value = item.Category;
            worksheet.Cell(row, 3).Value = item.FundName;
            worksheet.Cell(row, 4).Value = item.ShortName;
            worksheet.Cell(row, 5).Value = item.Date;
            worksheet.Cell(row, 6).Value = item.NAV;
            worksheet.Cell(row, 7).Value = item.Change;
            worksheet.Cell(row, 8).Value = item.ChangePercent;
            worksheet.Cell(row, 9).Value = item.Bid;
            worksheet.Cell(row, 10).Value = item.Offer;
            worksheet.Cell(row, 11).Value = item.TotalNetAsset;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
