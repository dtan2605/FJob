using System.Diagnostics;
using System.Text.Json;
using FJob.Contracts.Crawl;
using Microsoft.Extensions.Options;

namespace FJob.CrawlOrchestrationService.Services;

public sealed class PythonCrawlerExecutor(IHostEnvironment environment, IOptions<OrchestrationOptions> options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<CrawlExecutionResult> ExecuteAsync(
        CrawlRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(
                requestFile,
                JsonSerializer.Serialize(request, SerializerOptions),
                cancellationToken);

            var scriptPath = Path.GetFullPath(
                Path.Combine(environment.ContentRootPath, options.Value.ExecutionScriptPath));

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = options.Value.PythonExecutable,
                    Arguments = $"\"{scriptPath}\" --request-file \"{requestFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Crawler execution failed with exit code {process.ExitCode}: {error}");
            }

            var result = JsonSerializer.Deserialize<CrawlExecutionResult>(output, SerializerOptions);
            return result ?? throw new InvalidOperationException("Crawler returned empty payload.");
        }
        finally
        {
            if (File.Exists(requestFile))
            {
                File.Delete(requestFile);
            }
        }
    }
}
