using ClosedXML.Excel;
using MdmBilling.ViewModels;

namespace MdmBilling.Services;

public class ExcelBatchReader
{
    public ExcelReadResult Read(Stream stream)
    {
        var result = new ExcelReadResult();

        try
        {
            using var workbook = new XLWorkbook(stream);

            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet is null)
            {
                result.Errors.Add("The Excel file does not contain a worksheet.");
                return result;
            }

            var header = worksheet.FirstRowUsed();

            if (header is null)
            {
                result.Errors.Add("The file is empty.");
                return result;
            }

            int batchColumn = 0;
            int meterColumn = 0;

            foreach (var cell in header.CellsUsed())
            {
                var headerText = cell.GetString().Trim();

                if (headerText.Equals(
                    "Batch Number",
                    StringComparison.OrdinalIgnoreCase))
                {
                    batchColumn = cell.Address.ColumnNumber;
                }
                else if (headerText.Equals(
                    "Meter Number",
                    StringComparison.OrdinalIgnoreCase))
                {
                    meterColumn = cell.Address.ColumnNumber;
                }
            }

            if (batchColumn == 0 || meterColumn == 0)
            {
                result.Errors.Add(
                    "Row 1 must have the columns 'Batch Number' and 'Meter Number'.");

                return result;
            }

            var seenMeters = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            var batchNumbers = new HashSet<string>(
                StringComparer.Ordinal);

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                int rowNumber = row.RowNumber();

                // GetFormattedString reads the displayed Excel value.
                var batchNumber = row.Cell(batchColumn)
                    .GetFormattedString()
                    .Trim();

                var meterNumber = row.Cell(meterColumn)
                    .GetString()
                    .Trim();

                // Ignore completely empty rows.
                if (string.IsNullOrWhiteSpace(batchNumber) &&
                    string.IsNullOrWhiteSpace(meterNumber))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(batchNumber))
                {
                    result.Errors.Add(
                        $"Row {rowNumber}: Batch Number is empty.");

                    continue;
                }

                if (string.IsNullOrWhiteSpace(meterNumber))
                {
                    result.Errors.Add(
                        $"Row {rowNumber}: Meter Number is empty.");

                    continue;
                }

                // Batch number must be a positive whole number.
                if (!IsValidBatchNumber(batchNumber))
                {
                    result.Errors.Add(
                        $"Row {rowNumber}: Batch Number ({batchNumber}) must contain digits only, such as 1, 2, or 3.");

                    continue;
                }

                batchNumbers.Add(batchNumber);

                // Duplicate meters are skipped.
                if (!seenMeters.Add(meterNumber))
                {
                    result.Warnings.Add(
                        $"Row {rowNumber}: duplicate meter {meterNumber} was skipped.");

                    continue;
                }

                result.Meters.Add(meterNumber);
            }

            result.BatchNumbers.AddRange(batchNumbers);

            if (result.Meters.Count == 0 &&
                result.Errors.Count == 0)
            {
                result.Errors.Add("The file has no meters.");
            }
        }
        catch (Exception)
        {
            result.Errors.Add(
                "Could not read the file. Make sure it is a valid .xlsx file.");
        }

        return result;
    }

    private static bool IsValidBatchNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Only digits 0-9 are accepted.
        // The first digit must not be zero.
        if (value[0] == '0')
        {
            return false;
        }

        return value.All(c => c >= '0' && c <= '9');
    }
}