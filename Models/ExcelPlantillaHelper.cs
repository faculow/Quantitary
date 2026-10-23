using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Quantitary.Models;

public static class ExcelPlantillaHelper
{
    public static byte[] GenerarPlantillaProductos()
    {
        var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var sheet = new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Productos"
            };
            sheets.Append(sheet);

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            var row = new Row();

            var columnas = new[]
            {
                "Nombre",
                "SKU",
                "Categoria",
                "Precio",
                "Proveedor"
            };

            foreach (var nombre in columnas)
            {
                var cell = new Cell
                {
                    DataType = CellValues.String,
                    CellValue = new CellValue(nombre)
                };
                row.Append(cell);
            }

            sheetData.Append(row);

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }
}
