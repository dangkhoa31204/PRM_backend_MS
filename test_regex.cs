using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public class SepayWebhookPayload
{
    [JsonPropertyName("transactionContent")]
    public string TransactionContent { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

class Program
{
    static void Main()
    {
        string json = "{\"transferAmount\": 10000, \"transferType\": \"in\", \"content\": \"AROMA32\"}";
        var payload = JsonSerializer.Deserialize<SepayWebhookPayload>(json);
        
        string transactionContent = !string.IsNullOrEmpty(payload.Content) 
            ? payload.Content 
            : payload.TransactionContent;
            
        Console.WriteLine($"payload.Content: {payload.Content}");
        Console.WriteLine($"transactionContent: {transactionContent}");
        
        var match = Regex.Match(transactionContent, @"AROMA[\s_]*(\d+)", RegexOptions.IgnoreCase);
        Console.WriteLine($"Regex success: {match.Success}");
    }
}
