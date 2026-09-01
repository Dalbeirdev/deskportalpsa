using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Application.Assistant;
using Desk.Application.Common;

namespace Desk.Infrastructure.Assistant;

/// <summary>
/// Google Gemini via the Generative Language REST API.
///
/// The key travels in a header, never the query string: a URL carrying a credential ends up in
/// proxy logs and browser history, and this one is the tenant's own billing account.
/// </summary>
public sealed class GeminiModel(HttpClient http) : IAssistantModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<string> CompleteAsync(
        string apiKey, string model, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 900 },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent")
        {
            Content = JsonContent.Create(body, options: Json),
        };
        req.Headers.Add("x-goog-api-key", apiKey);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new ValidationFailedException("The assistant could not be reached. Try again shortly.");
        }

        if (!resp.IsSuccessStatusCode)
        {
            // Say which half is wrong: a rejected key is an administrator's job, a rate limit is a
            // matter of waiting, and telling them apart saves an afternoon.
            var detail = await SafeErrorAsync(resp, ct);
            throw new ValidationFailedException(resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    "Google rejected the API key. Check it in Assistant settings.",
                HttpStatusCode.TooManyRequests =>
                    "Google is rate-limiting this key right now. Try again in a moment.",
                HttpStatusCode.BadRequest =>
                    $"Google rejected the request{(detail is null ? "" : $": {detail}")}",
                _ => $"The assistant failed ({(int)resp.StatusCode}){(detail is null ? "." : $": {detail}")}",
            });
        }

        var parsed = await resp.Content.ReadFromJsonAsync<GeminiResponse>(Json, ct);
        var text = parsed?.Candidates?
            .SelectMany(c => c.Content?.Parts ?? [])
            .Select(p => p.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

        return string.IsNullOrWhiteSpace(text)
            // A blocked or empty completion is not a crash, but it is not an answer either.
            ? throw new ValidationFailedException("The assistant returned nothing for this ticket.")
            : text.Trim();
    }

    private static async Task<string?> SafeErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg) ? msg.GetString() : null;
        }
        catch { return null; }
    }

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; }
    }
    private sealed class Candidate
    {
        [JsonPropertyName("content")] public Content? Content { get; set; }
    }
    private sealed class Content
    {
        [JsonPropertyName("parts")] public List<Part>? Parts { get; set; }
    }
    private sealed class Part
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
