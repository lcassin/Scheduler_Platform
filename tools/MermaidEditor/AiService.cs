using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MermaidEditor;

/// <summary>
/// Provides AI chat completions via OpenAI, Azure OpenAI, or Anthropic APIs.
/// Supports text and image inputs, with streaming responses.
/// </summary>
public class AiService : IDisposable
{
    private readonly HttpClient _httpClient;

    public AiService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    /// <summary>
    /// Sends a chat completion request and streams the response token by token.
    /// </summary>
    /// <param name="messages">The conversation messages (role + content)</param>
    /// <param name="provider">AI provider: "OpenAI" or "Anthropic"</param>
    /// <param name="apiKey">API key for the provider</param>
    /// <param name="model">Model name (e.g., "gpt-4o", "claude-sonnet-4-20250514")</param>
    /// <param name="endpoint">Optional custom endpoint (for Azure OpenAI)</param>
    /// <param name="onToken">Callback invoked for each streamed token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task StreamChatAsync(
        List<ChatMessage> messages,
        string provider,
        string apiKey,
        string model,
        string endpoint,
        Action<string> onToken,
        CancellationToken cancellationToken = default)
    {
        if (provider == "Anthropic")
        {
            await StreamAnthropicAsync(messages, apiKey, model, onToken, cancellationToken);
        }
        else
        {
            await StreamOpenAiAsync(messages, apiKey, model, endpoint, onToken, cancellationToken);
        }
    }

    private async Task StreamOpenAiAsync(
        List<ChatMessage> messages,
        string apiKey,
        string model,
        string endpoint,
        Action<string> onToken,
        CancellationToken cancellationToken)
    {
        string url;
        bool isAzure = false;

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var ep = endpoint.TrimEnd('/');
            // Detect Azure OpenAI endpoints
            if (ep.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase) ||
                ep.Contains("cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                isAzure = true;
                url = $"{ep}/openai/deployments/{model}/chat/completions?api-version=2025-04-01-preview";
            }
            else
            {
                url = ep.EndsWith("/chat/completions") ? ep : $"{ep}/v1/chat/completions";
            }
        }
        else
        {
            url = "https://api.openai.com/v1/chat/completions";
        }

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = BuildOpenAiMessages(messages),
            ["stream"] = true,
            ["max_completion_tokens"] = 4096
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        if (isAzure)
        {
            request.Headers.Add("api-key", apiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AiServiceException($"API error ({response.StatusCode}): {errorBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;
            var data = line.Substring(6);
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var content))
                    {
                        var token = content.GetString();
                        if (token != null)
                        {
                            onToken(token);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Skip malformed SSE chunks
            }
        }
    }

    private async Task StreamAnthropicAsync(
        List<ChatMessage> messages,
        string apiKey,
        string model,
        Action<string> onToken,
        CancellationToken cancellationToken)
    {
        const string url = "https://api.anthropic.com/v1/messages";

        // Extract system message if present
        string? systemPrompt = null;
        var userMessages = new List<ChatMessage>();
        foreach (var msg in messages)
        {
            if (msg.Role == "system")
            {
                systemPrompt = msg.TextContent;
            }
            else
            {
                userMessages.Add(msg);
            }
        }

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = BuildAnthropicMessages(userMessages),
            ["max_tokens"] = 4096,
            ["stream"] = true
        };
        if (systemPrompt != null)
        {
            requestBody["system"] = systemPrompt;
        }

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AiServiceException($"API error ({response.StatusCode}): {errorBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;
            var data = line.Substring(6);

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (type == "content_block_delta" &&
                        root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("text", out var text))
                    {
                        var token = text.GetString();
                        if (token != null)
                        {
                            onToken(token);
                        }
                    }
                    else if (type == "message_stop")
                    {
                        break;
                    }
                }
            }
            catch (JsonException)
            {
                // Skip malformed SSE chunks
            }
        }
    }

    private static List<object> BuildOpenAiMessages(List<ChatMessage> messages)
    {
        var result = new List<object>();
        foreach (var msg in messages)
        {
            if (msg.ImageData != null && msg.ImageData.Count > 0)
            {
                // Multi-modal message with images
                var contentParts = new List<object>();
                if (!string.IsNullOrEmpty(msg.TextContent))
                {
                    contentParts.Add(new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = msg.TextContent
                    });
                }
                foreach (var img in msg.ImageData)
                {
                    contentParts.Add(new Dictionary<string, object>
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new Dictionary<string, string>
                        {
                            ["url"] = $"data:{img.MediaType};base64,{img.Base64Data}"
                        }
                    });
                }
                result.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.Role,
                    ["content"] = contentParts
                });
            }
            else
            {
                result.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.Role,
                    ["content"] = msg.TextContent ?? ""
                });
            }
        }
        return result;
    }

    private static List<object> BuildAnthropicMessages(List<ChatMessage> messages)
    {
        var result = new List<object>();
        foreach (var msg in messages)
        {
            if (msg.ImageData != null && msg.ImageData.Count > 0)
            {
                var contentParts = new List<object>();
                foreach (var img in msg.ImageData)
                {
                    contentParts.Add(new Dictionary<string, object>
                    {
                        ["type"] = "image",
                        ["source"] = new Dictionary<string, string>
                        {
                            ["type"] = "base64",
                            ["media_type"] = img.MediaType,
                            ["data"] = img.Base64Data
                        }
                    });
                }
                if (!string.IsNullOrEmpty(msg.TextContent))
                {
                    contentParts.Add(new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = msg.TextContent
                    });
                }
                result.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.Role,
                    ["content"] = contentParts
                });
            }
            else
            {
                result.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.Role,
                    ["content"] = msg.TextContent ?? ""
                });
            }
        }
        return result;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Represents a chat message with optional image attachments.
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "user"; // "system", "user", "assistant"
    public string TextContent { get; set; } = "";
    public List<ImageAttachment>? ImageData { get; set; }
}

/// <summary>
/// Represents a base64-encoded image attachment.
/// </summary>
public class ImageAttachment
{
    public string Base64Data { get; set; } = "";
    public string MediaType { get; set; } = "image/png"; // "image/png", "image/jpeg", etc.
    public string FileName { get; set; } = "";
}

/// <summary>
/// Exception thrown by AiService for API errors.
/// </summary>
public class AiServiceException : Exception
{
    public AiServiceException(string message) : base(message) { }
}
