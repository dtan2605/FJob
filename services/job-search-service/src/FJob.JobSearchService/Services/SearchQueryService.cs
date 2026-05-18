using FJob.Contracts.Search;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace FJob.JobSearchService.Services;

public sealed class SearchQueryService(
    ISearchIndexStore store,
    SearchCacheService cache,
    IOptions<SearchOptions> options)
{
    private readonly SearchCacheService _cache = cache;
    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["intern"] = ["intern", "internship", "thuc tap", "thuc-tap", "thực tập"],
        ["python"] = ["python"],
        ["dotnet"] = [".net", "dotnet", "asp.net", "c#"],
        ["it"] = ["it", "cntt", "cong nghe thong tin", "công nghệ thông tin"]
    };

    public async Task<SearchResponse> SearchAsync(SearchQueryRequest request, CancellationToken cancellationToken)
    {
        var cachedResponse = await _cache.GetAsync(request, cancellationToken);
        if (cachedResponse is not null)
        {
            return cachedResponse;
        }

        var items = await store.GetAllAsync(cancellationToken);
        var queryGroups = ExpandQueryGroups(request.Keyword);

        var matched = items
            .Select(item => new { Item = item, Score = ComputeScore(item, queryGroups) })
            .Where(x => x.Score > 0 || queryGroups.Count == 0);

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var normalizedLocation = TextNormalizer.Normalize(request.Location);
            matched = matched.Where(x =>
                x.Item.NormalizedLocation.Contains(normalizedLocation, StringComparison.OrdinalIgnoreCase));
        }

        if (request.Tags.Length > 0)
        {
            var normalizedTags = request.Tags.Select(TextNormalizer.NormalizeTag).ToArray();
            matched = matched.Where(x =>
                normalizedTags.All(tag => x.Item.Tags.Any(existing =>
                    existing.Equals(tag, StringComparison.OrdinalIgnoreCase))));
        }

        if (request.Sources.Length > 0)
        {
            matched = matched.Where(x =>
                request.Sources.Any(source => source.Equals(x.Item.Source, StringComparison.OrdinalIgnoreCase)));
        }

        if (request.SalaryMinMillions.HasValue)
        {
            matched = matched.Where(x => x.Item.SalaryMaxMillions is null || x.Item.SalaryMaxMillions >= request.SalaryMinMillions);
        }

        if (request.SalaryMaxMillions.HasValue)
        {
            matched = matched.Where(x => x.Item.SalaryMinMillions is null || x.Item.SalaryMinMillions <= request.SalaryMaxMillions);
        }

        if (request.PostedWithinDays.HasValue && request.PostedWithinDays.Value > 0)
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-request.PostedWithinDays.Value);
            matched = matched.Where(x => x.Item.PostedAtUtc >= threshold);
        }

        matched = request.SortBy.Equals("relevance", StringComparison.OrdinalIgnoreCase)
            ? matched.OrderByDescending(x => x.Score).ThenByDescending(x => x.Item.PostedAtUtc)
            : matched.OrderByDescending(x => x.Item.PostedAtUtc).ThenByDescending(x => x.Score);

        var totalCount = matched.Count();
        var page = Math.Max(1, request.Page);
        var requestedPageSize = request.PageSize <= 0 ? options.Value.DefaultPageSize : request.PageSize;
        var pageSize = Math.Clamp(requestedPageSize, 1, options.Value.MaxPageSize);

        var resultItems = matched
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SearchResultItem
            {
                Id = x.Item.Id,
                Title = x.Item.Title,
                Company = x.Item.Company,
                Source = x.Item.Source,
                Url = x.Item.Url,
                Location = x.Item.Location,
                Salary = x.Item.Salary,
                Description = x.Item.Description,
                SalaryMinMillions = x.Item.SalaryMinMillions,
                SalaryMaxMillions = x.Item.SalaryMaxMillions,
                Tags = x.Item.Tags,
                PostedAtUtc = x.Item.PostedAtUtc,
                Score = x.Score
            })
            .ToArray();

        var response = new SearchResponse
        {
            Items = resultItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        await _cache.SetAsync(request, response, cancellationToken);
        return response;
    }

    private static double ComputeScore(SearchDocument item, IReadOnlyCollection<string[]> queryGroups)
    {
        if (queryGroups.Count == 0)
        {
            return 1;
        }

        var haystack = $"{item.NormalizedTitle} {item.NormalizedCompany} {item.NormalizedDescription} {string.Join(" ", item.Tags)}";
        double score = 0;

        foreach (var group in queryGroups)
        {
            var groupMatched = false;
            foreach (var token in group)
            {
                var normalizedToken = TextNormalizer.Normalize(token);
                if (!ContainsNormalizedTerm(haystack, normalizedToken))
                {
                    continue;
                }

                groupMatched = true;
                if (ContainsNormalizedTerm(item.NormalizedTitle, normalizedToken))
                {
                    score += 3;
                }
                else if (item.Tags.Any(tag => ContainsNormalizedTerm(tag.Replace('-', ' '), normalizedToken)))
                {
                    score += 2;
                }
                else
                {
                    score += 1;
                }
            }

            if (!groupMatched)
            {
                return 0;
            }
        }

        return score;
    }

    private static bool ContainsNormalizedTerm(string text, string normalizedToken)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(normalizedToken))
        {
            return false;
        }

        var parts = normalizedToken
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Regex.Escape)
            .ToArray();

        if (parts.Length == 0)
        {
            return false;
        }

        var pattern = $@"(?<![a-z0-9]){string.Join(@"[\s-]+", parts)}(?![a-z0-9])";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static IReadOnlyCollection<string[]> ExpandQueryGroups(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<string[]>();
        }

        var terms = keyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TextNormalizer.Normalize)
            .ToArray();

        return terms
            .Select(term =>
            {
                var synonymGroup = Synonyms.FirstOrDefault(kvp =>
                    kvp.Key.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Value.Any(value => value.Equals(term, StringComparison.OrdinalIgnoreCase)));

                return synonymGroup.Key is null
                    ? new[] { term }
                    : synonymGroup.Value
                        .Concat([synonymGroup.Key])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
            })
            .ToArray();
    }
}
