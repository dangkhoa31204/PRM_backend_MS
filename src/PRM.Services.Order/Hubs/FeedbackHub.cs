using Microsoft.AspNetCore.SignalR;

namespace PRM.Services.Order.Hubs;

public class FeedbackHub : Hub
{
    public const string Route = "/hubs/feedback";
}
