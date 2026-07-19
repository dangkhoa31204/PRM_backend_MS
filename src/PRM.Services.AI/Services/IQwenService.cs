using PRM.Services.AI.Models;

namespace PRM.Services.AI.Services;

public interface IQwenService
{
    Task<ChatResponseDto> GetChatResponseAsync(List<ChatMessageDto> messages);
    Task<VoiceOrderResponse> ParseVoiceOrderAsync(VoiceOrderRequest request);
    Task<SentimentAnalysisResponse> AnalyzeSentimentAsync(SentimentAnalysisRequest request);
}
