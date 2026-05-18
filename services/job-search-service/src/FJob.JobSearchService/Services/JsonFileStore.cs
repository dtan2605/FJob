using System.Text.Json;

namespace FJob.JobSearchService.Services;

public sealed class JsonFileStore(IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task<T?> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task WriteAsync<T>(string fileName, T value, CancellationToken cancellationToken)
    {
        var path = GetPath(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private string GetPath(string fileName) =>
        Path.Combine(environment.ContentRootPath, "App_Data", fileName);
}
