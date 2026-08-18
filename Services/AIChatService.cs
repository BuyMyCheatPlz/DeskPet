using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DeskPet.Models;

namespace DeskPet.Services;

/// <summary>
/// Talks to an OpenAI-compatible chat completions endpoint. Works with
/// DeepSeek, OpenAI, and any custom provider that exposes /chat/completions.
/// </summary>
public sealed class AIChatService
{
    public static AIChatService Instance { get; } = new();

    public record ChatMessage(string Role, string Content);

    /// <summary>System prompt shared by both the standalone chat window and the
    /// inline pet chat (kept in one place so tone stays consistent).</summary>
    public const string SystemPrompt =
        "You are a cute desktop pet (a small animal living on the user's desktop). " +
        "Reply warmly, playfully and briefly (1-3 short sentences). " +
        "You can use a few emoji. Keep the tone light and friendly.";

    private AIChatService() { }

    public async Task<string> SendAsync(IEnumerable<ChatMessage> messages)
    {
        var s = AppSettings.Instance;
        if (string.IsNullOrWhiteSpace(s.AiApiKey))
            throw new InvalidOperationException("No API key configured. Open Settings → AI.");

        var baseUrl = s.AiBaseUrl.TrimEnd('/');
        var endpoint = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? baseUrl
            : baseUrl + "/chat/completions";

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(90);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.AiApiKey);

        var payload = new
        {
            model = s.AiModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            stream = false,
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(endpoint, content);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"AI request failed ({resp.StatusCode}): {Truncate(body, 300)}");

        using var doc = JsonDocument.Parse(body);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0) return "";
        var message = choices[0].GetProperty("message");
        return message.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
