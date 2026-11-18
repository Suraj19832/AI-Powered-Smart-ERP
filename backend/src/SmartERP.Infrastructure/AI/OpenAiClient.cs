using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SmartERP.Application.AI;

namespace SmartERP.Infrastructure.AI;

public class OpenAiOptions
{
    public const string Section = "OpenAI";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4.1-mini";
    /// <summary>Any OpenAI-compatible endpoint (OpenAI, Azure OpenAI proxy, local gateway).</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public int TimeoutSeconds { get; set; } = 90;
}

/// <summary>Minimal OpenAI Chat Completions client: tool calling, vision/PDF input and strict JSON-schema output.</summary>
public class OpenAiClient(HttpClient http, IOptions<OpenAiOptions> options) : IAiClient
{
    private readonly OpenAiOptions _o = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.ApiKey);
    public string Model => _o.Model;

    public async Task<AiResponse> ChatAsync(IReadOnlyList<AiMessage> messages, IReadOnlyList<AiToolDefinition>? tools, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = _o.Model,
            ["messages"] = new JsonArray(messages.Select(ToJson).ToArray<JsonNode?>()),
        };
        if (tools is { Count: > 0 })
        {
            body["tools"] = new JsonArray(tools.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = t.Parameters.DeepClone()
                }
            }).ToArray<JsonNode?>());
            body["tool_choice"] = "auto";
        }

        var msg = await SendAsync(body, ct);
        var calls = new List<AiToolCall>();
        if (msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
            foreach (var c in tc.EnumerateArray())
            {
                var fn = c.GetProperty("function");
                calls.Add(new AiToolCall(c.GetProperty("id").GetString()!, fn.GetProperty("name").GetString()!,
                    fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}"));
            }
        return new AiResponse(Content(msg), calls);
    }

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        var r = await ChatAsync([AiMessage.System(system), AiMessage.User(user)], null, ct);
        return r.Content ?? "";
    }

    public async Task<string> ExtractJsonAsync(string system, string instruction, AiAttachment file, string schemaName, JsonObject jsonSchema, CancellationToken ct)
    {
        var dataUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(file.Data)}";
        JsonNode attachment = file.ContentType == "application/pdf"
            ? new JsonObject { ["type"] = "file", ["file"] = new JsonObject { ["filename"] = file.FileName, ["file_data"] = dataUrl } }
            : new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = dataUrl, ["detail"] = "high" } };

        var body = new JsonObject
        {
            ["model"] = _o.Model,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = instruction }, attachment)
                }),
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = schemaName, ["strict"] = true, ["schema"] = jsonSchema.DeepClone() }
            }
        };
        var msg = await SendAsync(body, ct);
        if (msg.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String)
            throw new InvalidOperationException("The AI model refused to process the document: " + refusal.GetString());
        return Content(msg) ?? "{}";
    }

    private static JsonObject ToJson(AiMessage m)
    {
        var o = new JsonObject { ["role"] = m.Role, ["content"] = m.Content };
        if (m.ToolCallId != null) o["tool_call_id"] = m.ToolCallId;
        if (m.ToolCalls is { Count: > 0 })
            o["tool_calls"] = new JsonArray(m.ToolCalls.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments }
            }).ToArray<JsonNode?>());
        return o;
    }

    private static string? Content(JsonElement msg) =>
        msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;

    private async Task<JsonElement> SendAsync(JsonObject body, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("OpenAI API key is not configured.");
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_o.BaseUrl.TrimEnd('/') + "/"), "chat/completions"))
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ApiKey);

        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            string detail;
            try { detail = JsonDocument.Parse(text).RootElement.GetProperty("error").GetProperty("message").GetString() ?? text; }
            catch { detail = text; }
            throw new HttpRequestException($"OpenAI API error {(int)res.StatusCode}: {(detail.Length > 300 ? detail[..300] : detail)}");
        }
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").Clone();
    }
}
