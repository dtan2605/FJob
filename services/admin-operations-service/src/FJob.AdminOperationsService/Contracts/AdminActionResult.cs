namespace FJob.AdminOperationsService.Contracts;

public sealed class AdminActionResult<T>
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
}
