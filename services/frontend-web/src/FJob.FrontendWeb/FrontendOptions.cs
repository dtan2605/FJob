namespace FJob.FrontendWeb;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string ApiGatewayBaseUrl { get; init; } = "http://localhost:5100";

    public string AppName { get; init; } = "FJob Tìm việc";
}
