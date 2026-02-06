using System.Net;

namespace SiNan.SDK;

internal static class HttpRetry
{
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        SiNanClientOptions options,
        CancellationToken cancellationToken)
    {
        var retryCount = Math.Max(0, options.RetryCount);
        var delayMs = Math.Max(0, options.RetryDelayMs);
        var maxDelayMs = Math.Max(delayMs, options.RetryMaxDelayMs);

        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            using var request = requestFactory();

            try
            {
                var response = await client.SendAsync(request, cancellationToken);
                if (IsTransient(response.StatusCode) && attempt < retryCount)
                {
                    response.Dispose();
                    await Task.Delay(ComputeDelay(delayMs, maxDelayMs, attempt), cancellationToken);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException) when (attempt < retryCount)
            {
                await Task.Delay(ComputeDelay(delayMs, maxDelayMs, attempt), cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < retryCount)
            {
                await Task.Delay(ComputeDelay(delayMs, maxDelayMs, attempt), cancellationToken);
            }
        }

        throw new ApiException("Request failed after retries.", HttpStatusCode.ServiceUnavailable, "retry_exhausted", null, null);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code >= 500;
    }

    private static TimeSpan ComputeDelay(int baseDelayMs, int maxDelayMs, int attempt)
    {
        if (baseDelayMs <= 0)
        {
            return TimeSpan.Zero;
        }

        var delay = baseDelayMs * (int)Math.Pow(2, attempt);
        delay = Math.Min(delay, maxDelayMs);
        return TimeSpan.FromMilliseconds(delay);
    }
}
