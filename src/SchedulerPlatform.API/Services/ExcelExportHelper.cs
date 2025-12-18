using ClosedXML.Excel;

namespace SchedulerPlatform.API.Services;

/// <summary>
/// Centralized helper service for creating Excel exports with consistent formatting.
/// Provides standardized table styling with auto-filter headers and alternating row colors.
/// </summary>
public static class ExcelExportHelper
{
    /// <summary>
    /// Creates an Excel workbook with a single worksheet containing the provided data.
    /// Applies consistent table formatting with auto-filter headers and alternating light blue/white rows.
    /// </summary>
    /// <typeparam name="T">The type of data items to export.</typeparam>
    /// <param name="sheetName">Name of the worksheet.</param>
    /// <param name="tableName">Name of the Excel table (must be unique within workbook).</param>
    /// <param name="headers">Array of column header names.</param>
    /// <param name="data">Collection of data items to export.</param>
    /// <param name="rowMapper">Function that maps each data item to an array of cell values.</param>
    /// <returns>Byte array containing the Excel file content.</returns>
    public static byte[] CreateExcelExport<T>(
        string sheetName,
        string tableName,
        string[] headers,
        IEnumerable<T> data,
        Func<T, object?[]> rowMapper)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Write headers
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Write data rows
        int row = 2;
        foreach (var item in data)
        {
            var values = rowMapper(item);
            for (int col = 0; col < values.Length; col++)
            {
                SetCellValue(worksheet.Cell(row, col + 1), values[col]);
            }
            row++;
        }

        // Create table with auto-filter and alternating row colors
        if (row > 1) // Only create table if there's data
        {
            var dataRange = worksheet.Range(1, 1, row - 1, headers.Length);
            var table = dataRange.CreateTable(tableName);
            table.Theme = XLTableTheme.TableStyleLight9; // Light blue alternating rows
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Creates an Excel workbook with data and returns it as a byte array.
    /// This overload allows for more control over the worksheet creation.
    /// </summary>
    /// <param name="sheetName">Name of the worksheet.</param>
    /// <param name="tableName">Name of the Excel table.</param>
    /// <param name="worksheetBuilder">Action to populate the worksheet with headers and data.</param>
    /// <param name="columnCount">Number of columns in the data.</param>
    /// <returns>Byte array containing the Excel file content.</returns>
    public static byte[] CreateExcelExport(
        string sheetName,
        string tableName,
        Action<IXLWorksheet> worksheetBuilder,
        int columnCount)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Let the caller populate the worksheet
        worksheetBuilder(worksheet);

        // Find the last row with data
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        // Create table with auto-filter and alternating row colors
        if (lastRow > 1)
        {
            var dataRange = worksheet.Range(1, 1, lastRow, columnCount);
            var table = dataRange.CreateTable(tableName);
            table.Theme = XLTableTheme.TableStyleLight9; // Light blue alternating rows
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Sets a cell value handling different types appropriately.
    /// </summary>
    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = "";
                break;
            case DateTime dateTime:
                cell.Value = dateTime;
                break;
            case DateTimeOffset dateTimeOffset:
                cell.Value = dateTimeOffset.DateTime;
                break;
            case bool boolValue:
                cell.Value = boolValue ? "Yes" : "No";
                break;
            case int intValue:
                cell.Value = intValue;
                break;
            case long longValue:
                cell.Value = longValue;
                break;
            case decimal decimalValue:
                cell.Value = decimalValue;
                break;
            case double doubleValue:
                cell.Value = doubleValue;
                break;
            case float floatValue:
                cell.Value = floatValue;
                break;
            default:
                cell.Value = value.ToString() ?? "";
                break;
        }
    }

    /// <summary>
    /// Escapes a string value for CSV export.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value safe for CSV.</returns>
    public static string CsvEscape(string? value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    /// <summary>
    /// Creates a CSV export from the provided data.
    /// </summary>
    /// <typeparam name="T">The type of data items to export.</typeparam>
    /// <param name="headers">Comma-separated header line.</param>
    /// <param name="data">Collection of data items to export.</param>
    /// <param name="rowMapper">Function that maps each data item to a CSV line.</param>
    /// <returns>Byte array containing the CSV file content (UTF-8 encoded).</returns>
    public static byte[] CreateCsvExport<T>(
        string headers,
        IEnumerable<T> data,
        Func<T, string> rowMapper)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine(headers);

        foreach (var item in data)
        {
            csv.AppendLine(rowMapper(item));
        }

        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    }
}
