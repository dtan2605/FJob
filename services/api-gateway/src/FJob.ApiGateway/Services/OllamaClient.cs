using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FJob.ApiGateway.Services;

public sealed class OllamaClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AiAdvisorOptions _options;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient httpClient, IOptions<AiAdvisorOptions> options, ILogger<OllamaClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.UseOllama && !string.IsNullOrWhiteSpace(_options.BaseUrl) && !string.IsNullOrWhiteSpace(_options.Model);

    public async Task<CvAdviceResponse?> AnalyzeAsync(CvAdviceRequest request, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", BuildPayload(request), JsonOptions, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama returned non-success status code {StatusCode} for CV advice request.", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaChatCompletionResponse>(JsonOptions, timeoutCts.Token);
            var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Ollama returned an empty response body for CV advice.");
                return null;
            }

            try
            {
                var json = ExtractJson(content);
                return JsonSerializer.Deserialize<CvAdviceResponse>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Ollama response for CV advice.");
                return null;
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ollama CV advice request timed out.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call Ollama for CV advice. Falling back to built-in analyzer.");
            return null;
        }
    }

    private object BuildPayload(CvAdviceRequest request)
    {
        var prompt = $$"""
        Bạn là chuyên gia tối ưu CV cho tuyển dụng công nghệ.
        Hãy đọc CV và mô tả công việc, rồi trả về JSON hợp lệ duy nhất, không thêm markdown, không thêm giải thích ngoài JSON.

        Yêu cầu JSON:
        {
          "matchPercent": số nguyên 0-100,
          "summary": "tóm tắt ngắn bằng tiếng Việt",
          "strengths": ["...", "..."],
          "missingSkills": ["...", "..."],
          "improvements": ["...", "..."]
        }

        Quy tắc:
        - Viết hoàn toàn bằng tiếng Việt có dấu.
        - Tập trung vào kỹ năng, kinh nghiệm, dự án, từ khóa ATS, cách viết lại CV.
        - "strengths", "missingSkills", "improvements" mỗi mảng từ 2 đến 5 ý.
        - Nếu thông tin không chắc chắn thì diễn đạt thận trọng.

        Công việc:
        - Tiêu đề: {{request.JobTitle}}
        - Công ty: {{request.Company}}
        - Địa điểm: {{request.Location}}
        - Tags: {{string.Join(", ", request.Tags ?? Array.Empty<string>())}}
        - Mô tả: {{request.Description}}

        CV:
        {{request.CvText}}
        """;

        return new
        {
            model = _options.Model,
            temperature = _options.Temperature,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Bạn chỉ được trả về JSON hợp lệ."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed[firstBrace..(lastBrace + 1)];
            }
        }

        return trimmed;
    }

    private sealed record OllamaChatCompletionResponse(OllamaChoice[]? Choices);
    private sealed record OllamaChoice(OllamaMessage? Message);
    private sealed record OllamaMessage(string? Content);
}
