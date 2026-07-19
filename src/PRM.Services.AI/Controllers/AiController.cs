using Microsoft.AspNetCore.Mvc;
using PRM.Services.AI.Models;
using PRM.Services.AI.Services;

namespace PRM.Services.AI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly IQwenService _qwenService;
    private readonly ILogger<AiController> _logger;

    public AiController(IQwenService qwenService, ILogger<AiController> logger)
    {
        _qwenService = qwenService;
        _logger = logger;
    }

    /// <summary>
    /// AI Sommelier / Menu Recommendation for Customers (React Web)
    /// </summary>
    [HttpPost("customer-chat")]
    public async Task<ActionResult<ChatResponseDto>> CustomerChat([FromBody] ChatRequestDto request)
    {
        if (request.Messages == null || !request.Messages.Any())
        {
            return BadRequest(new { Message = "Nội dung cuộc trò chuyện không được để trống." });
        }

        // Auto-fix empty or numeric roles to 'user'
        foreach (var msg in request.Messages)
        {
            if (string.IsNullOrWhiteSpace(msg.Role) || msg.Role == "0" || msg.Role == "1" || msg.Role == "2")
            {
                msg.Role = "user";
            }
        }

        // Fetch real-time menu from Restaurant Service
        var menuContext = await _qwenService.GetMenuContextAsync();

        // Enforce customer assistant system prompt with strict food & drink guardrails + real-time menu
        var systemMsg = request.Messages.FirstOrDefault(m => m.Role == "system");
        string strictSystemPrompt = "Bạn là AI Sommelier - Trợ lý tư vấn ẩm thực thân thiện của nhà hàng.\n" +
            menuContext + "\n" +
            "QUY TẮC BẮT BUỘC:\n" +
            "1. Bạn CHỈ ĐƯỢC PHÉP trả lời các câu hỏi liên quan đến ẩm thực, đồ ăn, thức uống, thực đơn, nguyên liệu, khẩu vị, dinh dưỡng và dịch vụ nhà hàng.\n" +
            "2. Khi tư vấn, hãy ƯU TIÊN GỢI Ý CÁC MÓN CÓ TRONG THỰC ĐƠN NHÀ HÀNG ở trên (nêu đúng tên món và giá tiền).\n" +
            "3. Tuyệt đối KHÔNG trả lời các chủ đề ngoài lề (như lập trình, toán học, thời tiết, chính trị, tin tức, công nghệ, game, câu hỏi chung...).\n" +
            "4. Nếu khách hàng hỏi bất kỳ chủ đề ngoài lề nào, bạn PHẢI lịch sự từ chối bằng câu: \"Xin lỗi bạn, mình là AI Sommelier của nhà hàng nên chỉ có thể hỗ trợ tư vấn các thông tin về thực đơn, đồ ăn và thức uống thôi ạ. Bạn có muốn mình gợi ý món ăn hay thức uống gì không?\"";

        if (systemMsg == null)
        {
            request.Messages.Insert(0, new ChatMessageDto
            {
                Role = "system",
                Content = strictSystemPrompt
            });
        }
        else
        {
            systemMsg.Content = strictSystemPrompt;
        }

        var result = await _qwenService.GetChatResponseAsync(request.Messages);
        return Ok(result);
    }

    /// <summary>
    /// AI Operational & Management Assistant for Staff/Admin (Flutter Mobile)
    /// </summary>
    [HttpPost("staff-chat")]
    public async Task<ActionResult<ChatResponseDto>> StaffChat([FromBody] ChatRequestDto request)
    {
        if (request.Messages == null || !request.Messages.Any())
        {
            return BadRequest(new { Message = "Nội dung hội thoại không được để trống." });
        }

        if (!request.Messages.Any(m => m.Role == "system"))
        {
            request.Messages.Insert(0, new ChatMessageDto
            {
                Role = "system",
                Content = "Bạn là Trợ lý Quản lý AI của nhà hàng. Nhiệm vụ của bạn là hỗ trợ Quản lý và Nhân viên tra cứu thông tin vận hành, tóm tắt báo cáo kinh doanh và đề xuất tối ưu dịch vụ."
            });
        }

        var result = await _qwenService.GetChatResponseAsync(request.Messages);
        return Ok(result);
    }

    /// <summary>
    /// AI Voice-to-Cart Order Parsing
    /// </summary>
    [HttpPost("parse-voice")]
    public async Task<ActionResult<VoiceOrderResponse>> ParseVoiceOrder([FromBody] VoiceOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VoiceText))
        {
            return BadRequest(new { Message = "Đoạn văn bản giọng nói không được để trống." });
        }

        var result = await _qwenService.ParseVoiceOrderAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// AI Sentiment Analysis & Executive Summary
    /// </summary>
    [HttpPost("sentiment")]
    public async Task<ActionResult<SentimentAnalysisResponse>> AnalyzeSentiment([FromBody] SentimentAnalysisRequest request)
    {
        if (request.Feedbacks == null || !request.Feedbacks.Any())
        {
            return BadRequest(new { Message = "Danh sách đánh giá không được để trống." });
        }

        var result = await _qwenService.AnalyzeSentimentAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Health Check endpoint for AI microservice
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "PRM.Services.AI",
            ModelProvider = "Qwen 2.5",
            Timestamp = DateTime.UtcNow
        });
    }
}
