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

        // Add customer assistant context if system prompt is missing
        if (!request.Messages.Any(m => m.Role == "system"))
        {
            request.Messages.Insert(0, new ChatMessageDto
            {
                Role = "system",
                Content = "Bạn là AI Sommelier - Trợ lý tư vấn món ăn thân thiện của nhà hàng. Nhiệm vụ của bạn là tư vấn món ăn, thức uống, giải đáp thắc mắc về khẩu vị, thành phần nguyên liệu và chế độ ăn uống cho khách hàng."
            });
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
