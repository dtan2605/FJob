namespace FJob.AdminOperationsService;

public sealed class AdminOperationsOptions
{
    public const string SectionName = "AdminOperations";

    public string OrchestrationBaseUrl { get; init; } = "http://localhost:5102";
    public string JobCatalogBaseUrl { get; init; } = "http://localhost:5101";
    public string ApiGatewayBaseUrl { get; init; } = "http://localhost:5100";
    public string IdentityBaseUrl { get; init; } = "http://localhost:5104";
}
