using System.Text.Json.Nodes;

namespace SmartERP.Application.AI;

public record AiToolCall(string Id, string Name, string Arguments);

public record AiMessage(string Role, string? Content, string? ToolCallId = null, IReadOnlyList<AiToolCall>? ToolCalls = null)
{
    public static AiMessage System(string c) => new("system", c);
    public static AiMessage User(string c) => new("user", c);
    public static AiMessage Assistant(string? c, IReadOnlyList<AiToolCall>? calls = null) => new("assistant", c, null, calls);
    public static AiMessage Tool(string id, string c) => new("tool", c, id);
}

public record AiToolDefinition(string Name, string Description, JsonObject Parameters);

public record AiResponse(string? Content, IReadOnlyList<AiToolCall> ToolCalls);

public record AiAttachment(byte[] Data, string ContentType, string FileName);

/// <summary>LLM provider abstraction (implemented with the OpenAI Chat Completions API in Infrastructure).</summary>
public interface IAiClient
{
    bool IsConfigured { get; }
    string Model { get; }
    Task<AiResponse> ChatAsync(IReadOnlyList<AiMessage> messages, IReadOnlyList<AiToolDefinition>? tools, CancellationToken ct);
    Task<string> CompleteAsync(string system, string user, CancellationToken ct);
    /// <summary>Sends a document (PDF or image) and returns JSON conforming to <paramref name="jsonSchema"/>.</summary>
    Task<string> ExtractJsonAsync(string system, string instruction, AiAttachment file, string schemaName, JsonObject jsonSchema, CancellationToken ct);
}

public class AppSettings
{
    public string CompanyName { get; set; } = "SmartERP Demo Pvt Ltd";
    public string Currency { get; set; } = "INR";
    public string Locale { get; set; } = "en-IN";
}
