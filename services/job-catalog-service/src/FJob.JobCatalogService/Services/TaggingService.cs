namespace FJob.JobCatalogService.Services;

public sealed class TaggingService
{
    private static readonly Dictionary<string, string[]> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["intern"] = ["intern", "internship", "thuc tap", "thuc tap sinh"],
        ["it"] = ["it", "cong nghe thong tin", "cntt", "software", "developer"],
        ["python"] = ["python"],
        ["dotnet"] = [".net", "dotnet", "asp.net", "c#"],
        ["java"] = ["java"],
        ["crawler"] = ["crawler", "scrapy", "playwright"]
    };

    public string[] MergeAndNormalize(string title, string description, IEnumerable<string> sourceTags)
    {
        var bag = new HashSet<string>(sourceTags.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize), StringComparer.OrdinalIgnoreCase);

        var text = $"{title} {description}".ToLowerInvariant();
        foreach (var (tag, keywords) in Rules)
        {
            if (keywords.Any(text.Contains))
            {
                bag.Add(tag);
            }
        }

        return bag.OrderBy(x => x).ToArray();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
