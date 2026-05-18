namespace FJob.ApiGateway.Services;

public sealed class AiAdvisorOptions
{
    public const string SectionName = "AiAdvisor";

    public bool UseOllama { get; init; } = false;
    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "qwen2.5:3b";
    public double Temperature { get; init; } = 0.2d;
    public int TimeoutSeconds { get; init; } = 45;
}
