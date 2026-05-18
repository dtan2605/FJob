using Microsoft.Extensions.Logging;

namespace FJob.Observability;

public static class ResilienceExecutor
{
    public static async Task ExecuteAsync(
        string operationName,
        ILogger logger,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken,
        int maxAttempts = 3,
        int timeoutSeconds = 5)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await action(timeoutCts.Token);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                logger.LogWarning(
                    ex,
                    "Operation {OperationName} failed on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    operationName,
                    attempt,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw new InvalidOperationException(
            $"Operation {operationName} failed after retry attempts.",
            lastException);
    }

    public static async Task<T> ExecuteAsync<T>(
        string operationName,
        ILogger logger,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        int maxAttempts = 3,
        int timeoutSeconds = 5)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                return await action(timeoutCts.Token);
            }
            catch (Exception ex) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                logger.LogWarning(
                    ex,
                    "Operation {OperationName} failed on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    operationName,
                    attempt,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw new InvalidOperationException(
            $"Operation {operationName} failed after retry attempts.",
            lastException);
    }
}
