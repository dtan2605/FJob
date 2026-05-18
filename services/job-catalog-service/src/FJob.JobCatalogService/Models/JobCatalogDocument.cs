using FJob.Contracts.Jobs;

namespace FJob.JobCatalogService.Models;

public sealed class JobCatalogDocument
{
    public List<JobRecord> Items { get; init; } = [];
}
