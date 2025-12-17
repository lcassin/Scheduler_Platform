using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchedulerPlatform.UI.Services;

/// <summary>
/// Extension methods for HttpClient that handle 401 Unauthorized responses gracefully.
/// These methods prevent exceptions from propagating when the user's session has expired,
/// allowing the centralized session expiration handling in MainLayout to redirect to login.
/// </summary>
public static class ApiClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Sends a GET request and deserializes the JSON response.
    /// Returns default(T) if the response is 401 Unauthorized (session expired).
    /// </summary>
    public static async Task<T?> GetFromJsonSafeAsync<T>(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync(requestUri, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Session expired - return default and let the redirect happen
            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            // For other errors, throw so the caller can handle appropriately
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// Sends a POST request with JSON content and returns the response.
    /// Returns a failed result if the response is 401 Unauthorized (session expired).
    /// </summary>
    public static async Task<ApiResult> PostAsJsonSafeAsync<T>(
        this HttpClient client,
        string requestUri,
        T content,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(requestUri, content, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ApiResult.SessionExpired();
        }

        return new ApiResult(response.IsSuccessStatusCode, response.StatusCode, response);
    }

    /// <summary>
    /// Sends a POST request with JSON content and deserializes the response.
    /// Returns default(TResponse) if the response is 401 Unauthorized (session expired).
    /// </summary>
    public static async Task<ApiResult<TResponse>> PostAsJsonSafeAsync<TRequest, TResponse>(
        this HttpClient client,
        string requestUri,
        TRequest content,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(requestUri, content, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ApiResult<TResponse>.SessionExpired();
        }

        if (!response.IsSuccessStatusCode)
        {
            return new ApiResult<TResponse>(false, response.StatusCode, default, response);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        return new ApiResult<TResponse>(true, response.StatusCode, result, response);
    }

    /// <summary>
    /// Sends a PUT request with JSON content and returns the response.
    /// Returns a failed result if the response is 401 Unauthorized (session expired).
    /// </summary>
    public static async Task<ApiResult> PutAsJsonSafeAsync<T>(
        this HttpClient client,
        string requestUri,
        T content,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PutAsJsonAsync(requestUri, content, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ApiResult.SessionExpired();
        }

        return new ApiResult(response.IsSuccessStatusCode, response.StatusCode, response);
    }

    /// <summary>
    /// Sends a DELETE request and returns the response.
    /// Returns a failed result if the response is 401 Unauthorized (session expired).
    /// </summary>
    public static async Task<ApiResult> DeleteSafeAsync(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        var response = await client.DeleteAsync(requestUri, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ApiResult.SessionExpired();
        }

        return new ApiResult(response.IsSuccessStatusCode, response.StatusCode, response);
    }
}

/// <summary>
/// Represents the result of an API call, including success status and HTTP status code.
/// </summary>
public class ApiResult
{
    public bool IsSuccess { get; }
    public HttpStatusCode StatusCode { get; }
    public bool IsSessionExpired => StatusCode == HttpStatusCode.Unauthorized;
    public HttpResponseMessage? Response { get; }

    public ApiResult(bool isSuccess, HttpStatusCode statusCode, HttpResponseMessage? response = null)
    {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        Response = response;
    }

    public static ApiResult SessionExpired() => new(false, HttpStatusCode.Unauthorized);

    /// <summary>
    /// Throws an exception if the request was not successful (excluding session expiration).
    /// Use this when you want to handle session expiration gracefully but still throw on other errors.
    /// </summary>
    public void EnsureSuccessOrSessionExpired()
    {
        if (!IsSuccess && !IsSessionExpired)
        {
            Response?.EnsureSuccessStatusCode();
        }
    }
}

/// <summary>
/// Represents the result of an API call that returns data.
/// </summary>
public class ApiResult<T> : ApiResult
{
    public T? Data { get; }

    public ApiResult(bool isSuccess, HttpStatusCode statusCode, T? data, HttpResponseMessage? response = null)
        : base(isSuccess, statusCode, response)
    {
        Data = data;
    }

    public new static ApiResult<T> SessionExpired() => new(false, HttpStatusCode.Unauthorized, default);
}
