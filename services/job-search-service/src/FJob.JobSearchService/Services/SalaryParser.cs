using System.Globalization;
using System.Text.RegularExpressions;

namespace FJob.JobSearchService.Services;

public static partial class SalaryParser
{
    public static (decimal? min, decimal? max) ParseMillions(string salary)
    {
        if (string.IsNullOrWhiteSpace(salary))
        {
            return (null, null);
        }

        var normalized = salary.Trim().ToLowerInvariant();
        if (normalized.Contains("negotiable") || normalized.Contains("thoa thuan"))
        {
            return (null, null);
        }

        var matches = NumberRegex().Matches(normalized);
        if (matches.Count == 0)
        {
            return (null, null);
        }

        var values = matches
            .Select(match => decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : (decimal?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (values.Length == 0)
        {
            return (null, null);
        }

        if (values.Length == 1)
        {
            return (values[0], values[0]);
        }

        return (values.Min(), values.Max());
    }

    [GeneratedRegex(@"\d+(\.\d+)?", RegexOptions.Compiled)]
    private static partial Regex NumberRegex();
}
