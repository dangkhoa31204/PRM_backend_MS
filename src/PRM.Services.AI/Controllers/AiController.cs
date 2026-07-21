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

        // Enforce customer assistant system prompt with strict food & drink guardrails + real-time menu + language guardrail
        var systemMsg = request.Messages.FirstOrDefault(m => m.Role == "system");
        string strictSystemPrompt = "Bạn là AI Sommelier - Trợ lý tư vấn ẩm thực thân thiện của nhà hàng.\n" +
            menuContext + "\n" +
            "QUY TẮC BẮT BUỘC:\n" +
            "1. NGÔN NGỮ BẮT BUỘC: Bạn BẮT BUỘC CHỈ ĐƯỢC trả lời hoàn toàn 100% bằng TIẾNG VIỆT (Vietnamese). Tuyệt đối KHÔNG ĐƯỢC sử dụng tiếng Trung (Chinese / 中文), chữ Hán, hay bất kỳ ngôn ngữ nào khác trong bất kỳ trường hợp nào.\n" +
            "2. CHỦ ĐỀ CHỈ ĐỊNH: Bạn CHỈ ĐƯỢC PHÉP trả lời các câu hỏi liên quan đến ẩm thực, đồ ăn, thức uống, thực đơn, nguyên liệu, khẩu vị, dinh dưỡng và dịch vụ nhà hàng.\n" +
            "3. THỰC ĐƠN THỰC TẾ: Khi liệt kê hoặc gợi ý món ăn/thức uống, bạn BẮT BUỘC CHỈ ĐƯỢC DÙNG CÁC MÓN CÓ TRONG DANH SÁCH THỰC ĐƠN THỰC TẾ CỦA NHÀ HÀNG Ở TRÊN. Nêu chính xác tên món và giá tiền niêm yết.\n" +
            "4. KHÔNG BỊA MÓN: Tuyệt đối KHÔNG ĐƯỢC tự bịa ra các món ăn/thức uống không có trong danh sách thực đơn trên (như Phở bò, Bún chả, Cơm chiên...).\n" +
            "5. TỪ CHỐI NGOẠI LỀ: Tuyệt đối KHÔNG trả lời các chủ đề ngoài lề (như lập trình, toán học, thời tiết, chính trị, tin tức, công nghệ, game, câu hỏi chung...).\n" +
            "6. MẪU TỪ CHỐI: Nếu khách hàng hỏi bất kỳ chủ đề ngoài lề nào, bạn PHẢI lịch sự từ chối bằng câu: \"Xin lỗi bạn, mình là AI Sommelier của nhà hàng nên chỉ có thể hỗ trợ tư vấn các thông tin về thực đơn, đồ ăn và thức uống thôi ạ. Bạn có muốn mình gợi ý món ăn hay thức uống gì không?\"";

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
    /// AI Automatic Admin Dashboard Recommendations Generator (Read DB & Recommend)
    /// </summary>
    [HttpGet("dashboard-recommendations")]
    public async Task<ActionResult<DashboardRecommendationResponse>> GetDashboardRecommendations()
    {
        var result = await _qwenService.GenerateDashboardRecommendationsAsync();
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

