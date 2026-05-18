using FJob.Contracts.Jobs;
using FJob.Contracts.Search;

namespace FJob.JobSearchService.Services;

public sealed class SearchDocumentMapper
{
    public SearchDocument Map(JobRecord job)
    {
        var salary = SalaryParser.ParseMillions(job.Salary);

        return new SearchDocument
        {
            Id = job.Id,
            Title = job.Title,
            NormalizedTitle = TextNormalizer.Normalize(job.Title),
            Company = job.Company,
            NormalizedCompany = TextNormalizer.Normalize(job.Company),
            Source = job.Source,
            Url = job.Url,
            Location = job.Location,
            NormalizedLocation = TextNormalizer.Normalize(job.Location),
            Salary = job.Salary,
            SalaryMinMillions = salary.min,
            SalaryMaxMillions = salary.max,
            Description = job.Description,
            NormalizedDescription = TextNormalizer.Normalize(job.Description),
            Tags = job.Tags.Select(TextNormalizer.NormalizeTag).ToArray(),
            PostedAtUtc = job.PostedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc
        };
    }
}
