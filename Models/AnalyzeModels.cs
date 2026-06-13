using System.Text.Json.Nodes;

namespace AiAnalyze.Models;

public class AnalyzeRequest
{
    public string LogContent { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public string? SystemPrompt { get; set; }
}

public class AnalyzeResponse
{
    public string Result { get; set; } = string.Empty;
    public string? ReasoningContent { get; set; }
    public JsonNode? Usage { get; set; }
    public string Model { get; set; } = string.Empty;
}
