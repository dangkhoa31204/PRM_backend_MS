using Microsoft.AspNetCore.SignalR;

namespace PRM.Services.Order.Hubs;

public class StaffNotificationHub : Hub
{
    public const string Route = "/hubs/staff";
    public const string StaffGroup = "staff";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, StaffGroup);
        await base.OnDisconnectedAsync(exception);
    }
}
