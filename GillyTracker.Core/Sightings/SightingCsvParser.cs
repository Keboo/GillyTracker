using System.Globalization;

namespace GillyTracker.Core.Sightings;

public static class SightingCsvParser
{
    private static readonly string[] RequiredColumns = ["Latitude", "Longitude"];

    /// <summary>
    /// Parses CSV content describing dog sighting reports. The CSV must contain a header row
    /// with (at minimum) "Latitude" and "Longitude" columns. Optional columns are "Details"
    /// and "CreatedDate" (ISO 8601). Column order and casing are not significant.
    /// </summary>
    public static SightingCsvParseResult Parse(TextReader reader)
    {
        List<SightingCsvRow> rows = [];
        List<SightingCsvRowError> errors = [];

        string? headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            errors.Add(new SightingCsvRowError(1, "The CSV file is empty."));
            return new SightingCsvParseResult(rows, errors);
        }

        var headers = SplitCsvLine(headerLine)
            .Select(h => h.Trim())
            .ToList();

        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            columnIndex[headers[i]] = i;
        }

        var missingColumns = RequiredColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
        if (missingColumns.Count > 0)
        {
            errors.Add(new SightingCsvRowError(1, $"Missing required column(s): {string.Join(", ", missingColumns)}."));
            return new SightingCsvParseResult(rows, errors);
        }

        columnIndex.TryGetValue("Details", out int detailsIndex);
        bool hasDetails = columnIndex.ContainsKey("Details");
        columnIndex.TryGetValue("CreatedDate", out int createdDateIndex);
        bool hasCreatedDate = columnIndex.ContainsKey("CreatedDate");
        int latitudeIndex = columnIndex["Latitude"];
        int longitudeIndex = columnIndex["Longitude"];

        int lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = SplitCsvLine(line);

            if (!TryGetField(fields, latitudeIndex, out string rawLatitude) ||
                !decimal.TryParse(rawLatitude, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal latitude))
            {
                errors.Add(new SightingCsvRowError(lineNumber, "Latitude is missing or not a valid number."));
                continue;
            }

            if (!TryGetField(fields, longitudeIndex, out string rawLongitude) ||
                !decimal.TryParse(rawLongitude, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal longitude))
            {
                errors.Add(new SightingCsvRowError(lineNumber, "Longitude is missing or not a valid number."));
                continue;
            }

            if (!CoordinateValidator.IsValid(latitude, longitude))
            {
                errors.Add(new SightingCsvRowError(lineNumber, "Latitude must be between -90 and 90 and longitude must be between -180 and 180."));
                continue;
            }

            string? details = hasDetails && TryGetField(fields, detailsIndex, out string rawDetails) && !string.IsNullOrWhiteSpace(rawDetails)
                ? rawDetails.Trim()
                : null;

            if (details?.Length > 2000)
            {
                errors.Add(new SightingCsvRowError(lineNumber, "Details must be 2000 characters or fewer."));
                continue;
            }

            DateTimeOffset createdDate = DateTimeOffset.UtcNow;
            if (hasCreatedDate && TryGetField(fields, createdDateIndex, out string rawCreatedDate) && !string.IsNullOrWhiteSpace(rawCreatedDate))
            {
                if (!DateTimeOffset.TryParse(rawCreatedDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out createdDate))
                {
                    errors.Add(new SightingCsvRowError(lineNumber, "CreatedDate is not a valid date/time."));
                    continue;
                }
            }

            rows.Add(new SightingCsvRow(latitude, longitude, details, createdDate));
        }

        return new SightingCsvParseResult(rows, errors);
    }

    private static bool TryGetField(IReadOnlyList<string> fields, int index, out string value)
    {
        if (index < 0 || index >= fields.Count)
        {
            value = string.Empty;
            return false;
        }

        value = fields[index];
        return true;
    }

    private static List<string> SplitCsvLine(string line)
    {
        List<string> fields = [];
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}

public record SightingCsvRow(decimal Latitude, decimal Longitude, string? Details, DateTimeOffset CreatedDate);

public record SightingCsvRowError(int LineNumber, string Message);

public record SightingCsvParseResult(IReadOnlyList<SightingCsvRow> Rows, IReadOnlyList<SightingCsvRowError> Errors);
