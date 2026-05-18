using FJob.Contracts.Operations;

namespace FJob.CrawlOrchestrationService.Models;

public sealed class SourceControlDocument
{
    public List<SourceControlState> Items { get; init; } = [];
}
