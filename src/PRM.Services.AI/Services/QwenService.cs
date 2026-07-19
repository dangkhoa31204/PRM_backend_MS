using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PRM.Services.AI.Models;

namespace PRM.Services.AI.Services;

public class QwenService : IQwenService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<QwenService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public QwenService(HttpClient httpClient, IConfiguration config, ILogger<QwenService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ChatResponseDto> GetChatResponseAsync(List<ChatMessageDto> messages)
    {
        var baseUrl = (_config["Qwen:BaseUrl"] ?? "http://localhost:11434/v1").TrimEnd('/');
        var apiKey = _config["Qwen:ApiKey"] ?? string.Empty;
        var model = _config["Qwen:Model"] ?? "qwen2.5:7b";

        var payload = new
        {
            model = model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            temperature = 0.7
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                _logger.LogError("Qwen API returned error: {StatusCode} - {Error}", response.StatusCode, errorMsg);
                return new ChatResponseDto
                {
                    Reply = "Xin lỗi, hiện tại hệ thống AI đang bận hoặc dịch vụ Ollama chưa bật. Vui lòng kiểm tra lại.",
                    Provider = "Qwen (Error Fallback)"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return new ChatResponseDto
            {
                Reply = content ?? string.Empty,
                Provider = model
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi Qwen Service API");
            return new ChatResponseDto
            {
                Reply = "Không thể kết nối đến dịch vụ AI Qwen tại http://localhost:11434. Vui lòng kiểm tra lại dịch vụ Ollama.",
                Provider = "Qwen (Connection Error)"
            };
        }
    }

    public async Task<VoiceOrderResponse> ParseVoiceOrderAsync(VoiceOrderRequest request)
    {
        var systemPrompt = new ChatMessageDto
        {
            Role = "system",
            Content = @"Bạn là trợ lý AI trích xuất thông tin đặt món ăn từ lời nói giọng nói của khách hàng hoặc nhân viên.
Nhiệm vụ: Trích xuất các món ăn, số lượng, và ghi chú từ câu nói.
BẮT BUỘC trả về định dạng JSON thuần túy (không dùng markdown block ```json) theo cấu trúc:
{
  ""success"": true,
  ""message"": ""Đã phân tích thành công"",
  ""extractedItems"": [
    {
      ""menuItemName"": ""tên món"",
      ""quantity"": 1,
      ""note"": ""ghi chú nếu có""
    }
  ]
}"
        };

        var userMessage = new ChatMessageDto
        {
            Role = "user",
            Content = $"Câu thoại giọng nói: \"{request.VoiceText}\""
        };

        var chatResult = await GetChatResponseAsync(new List<ChatMessageDto> { systemPrompt, userMessage });
        
        try
        {
            var cleanJson = chatResult.Reply.Replace("```json", "").Replace("```", "").Trim();
            var result = JsonSerializer.Deserialize<VoiceOrderResponse>(cleanJson, _jsonOptions);
            return result ?? new VoiceOrderResponse { Success = false, Message = "Không thể đọc dữ liệu JSON từ AI." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể parse JSON từ phản hồi voice order của Qwen");
            return new VoiceOrderResponse
            {
                Success = true,
                Message = chatResult.Reply,
                ExtractedItems = new List<VoiceOrderItemDto>()
            };
        }
    }

    public async Task<SentimentAnalysisResponse> AnalyzeSentimentAsync(SentimentAnalysisRequest request)
    {
        var systemPrompt = new ChatMessageDto
        {
            Role = "system",
            Content = @"Bạn là chuyên gia phân tích đánh giá khách hàng cho nhà hàng.
BẮT BUỘC trả về JSON thuần túy (không dùng markdown code fence) theo dạng:
{
  ""overallSentiment"": ""Positive"" | ""Neutral"" | ""Negative"",
  ""executiveSummary"": ""Tóm tắt ngắn gọn 2-3 câu"",
  ""highlightedIssues"": [""Vấn đề 1"", ""Vấn đề 2""],
  ""actionItems"": [""Hành động 1"", ""Hành động 2""]
}"
        };

        var feedbackText = string.Join("\n- ", request.Feedbacks);
        var userMessage = new ChatMessageDto
        {
            Role = "user",
            Content = $"Danh sách đánh giá của khách hàng:\n- {feedbackText}"
        };

        var chatResult = await GetChatResponseAsync(new List<ChatMessageDto> { systemPrompt, userMessage });

        try
        {
            var cleanJson = chatResult.Reply.Replace("```json", "").Replace("```", "").Trim();
            var result = JsonSerializer.Deserialize<SentimentAnalysisResponse>(cleanJson, _jsonOptions);
            return result ?? new SentimentAnalysisResponse { ExecutiveSummary = chatResult.Reply };
        }
        catch
        {
            return new SentimentAnalysisResponse
            {
                OverallSentiment = "Neutral",
                ExecutiveSummary = chatResult.Reply
            };
        }
    }
}
