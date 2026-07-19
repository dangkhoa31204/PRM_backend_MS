namespace PRM.Services.AI.Models;

public class ChatMessageDto
{
    public string Role { get; set; } = "user"; // "system", "user", or "assistant"
    public string Content { get; set; } = string.Empty;
}

public class ChatRequestDto
{
    public List<ChatMessageDto> Messages { get; set; } = new();
    public bool Stream { get; set; } = false;
}

public class ChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public string Provider { get; set; } = "Qwen";
}

public class VoiceOrderRequest
{
    public string VoiceText { get; set; } = string.Empty;
    public int? TableId { get; set; }
}

public class VoiceOrderItemDto
{
    public string MenuItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
}

public class VoiceOrderResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public List<VoiceOrderItemDto> ExtractedItems { get; set; } = new();
}

public class SentimentAnalysisRequest
{
    public List<string> Feedbacks { get; set; } = new();
}

public class SentimentAnalysisResponse
{
    public string OverallSentiment { get; set; } = "Neutral";
    public string ExecutiveSummary { get; set; } = string.Empty;
    public List<string> HighlightedIssues { get; set; } = new();
    public List<string> ActionItems { get; set; } = new();
}
