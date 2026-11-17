using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HubLink.Models;

namespace HubLink.Services;

public class CsvExportService
{
    public async Task ExportAsync(
        string filePath,
        IReadOnlyList<Shipment> shipments)
    {
        if (shipments.Count == 0)
        {
            throw new InvalidOperationException(
                "There are no displayed shipments to export.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Enter a full destination path for the CSV file.");
        }

        string fullPath = Path.GetFullPath(filePath.Trim());

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The export filename must end with .csv.");
        }

        string? directory = Path.GetDirectoryName(fullPath);

        if (string.IsNullOrEmpty(directory)
            || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                "The destination folder does not exist.");
        }

        var csv = new StringBuilder();

        csv.Append(
            "TrackingNumber,Origin,Destination,Status,LastUpdatedUtc\r\n");

        foreach (var shipment in shipments)
        {
            if (shipment.LastUpdatedUtc is null)
            {
                throw new InvalidDataException(
                    "A shipment has no valid update time.");
            }

            string timestamp = shipment.LastUpdatedUtc.Value
                .UtcDateTime
                .ToString("O", CultureInfo.InvariantCulture);

            csv.Append(Escape(shipment.TrackingNumber));
            csv.Append(',');
            csv.Append(Escape(shipment.Origin));
            csv.Append(',');
            csv.Append(Escape(shipment.Destination));
            csv.Append(',');
            csv.Append(Escape(shipment.Status));
            csv.Append(',');
            csv.Append(Escape(timestamp));
            csv.Append("\r\n");
        }

        // Write a temporary file first to avoid leaving a partial report.
        string temporaryPath = Path.Combine(
            directory,
            $".hublink-{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                csv.ToString(),
                new UTF8Encoding(true));

            // Existing files are preserved; choose a new name to export again.
            File.Move(temporaryPath, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Cleanup must not hide the original export error.
                }
                catch (UnauthorizedAccessException)
                {
                    // Cleanup must not hide the original export error.
                }
            }
        }
    }

    private static string Escape(string? value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}