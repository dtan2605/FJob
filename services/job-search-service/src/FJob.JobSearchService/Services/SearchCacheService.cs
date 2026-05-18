using System.Text;
using System.Text.Json;
using FJob.Contracts.Search;
using Microsoft.Extensions.Caching.Distributed;

namespace FJob.JobSearchService.Services;

public sealed class SearchCacheService
{
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
    };

    public SearchCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<SearchResponse?> GetAsync(SearchQueryRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(request);
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cached is null || cached.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<SearchResponse>(cached, _serializerOptions);
    }

    public async Task SetAsync(SearchQueryRequest request, SearchResponse response, CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(request);
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, _serializerOptions);
        await _cache.SetAsync(cacheKey, payload, CacheOptions, cancellationToken);
    }

    private static string BuildCacheKey(SearchQueryRequest request)
    {
        var normalizedKeyword = request.Keyword?.Trim().ToLowerInvariant() ?? string.Empty;
        var location = request.Location?.Trim().ToLowerInvariant() ?? string.Empty;
        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? string.Empty;
        var tags = request.Tags is null ? string.Empty : string.Join(',', request.Tags.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var sources = request.Sources is null ? string.Empty : string.Join(',', request.Sources.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        return string.Join(":",
            "job-search",
            normalizedKeyword,
            location,
            sortBy,
            request.Page,
            request.PageSize,
            request.SalaryMinMillions?.ToString() ?? string.Empty,
            request.SalaryMaxMillions?.ToString() ?? string.Empty,
            request.PostedWithinDays?.ToString() ?? string.Empty,
            tags,
            sources);
    }
}

internal sealed class NullDistributedCache : IDistributedCache
{
    public byte[] Get(string key) => null!;
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
    public void Refresh(string key) { }
    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    public void Remove(string key) { }
    public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
}
