using Microsoft.AspNetCore.SignalR;
using PRM.Services.Order.Hubs;

namespace PRM.Services.Order.Notifications;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<StaffNotificationHub> _hub;

    public SignalRNotificationService(IHubContext<StaffNotificationHub> hub) => _hub = hub;

    // Flutter lắng nghe "OrderCreated"
    public Task NotifyOrderCreatedAsync(object order) =>
        _hub.Clients.Group(StaffNotificationHub.StaffGroup).SendAsync("OrderCreated", order);

    // Flutter lắng nghe "OrderUpdated"
    public Task NotifyOrderUpdatedAsync(object order) =>
        _hub.Clients.Group(StaffNotificationHub.StaffGroup).SendAsync("OrderUpdated", order);

    // Flutter lắng nghe "OrderStatusUpdated" → đọc data['status'] để detect Completed=4
    public Task NotifyOrderStatusUpdatedAsync(object order) =>
        _hub.Clients.Group(StaffNotificationHub.StaffGroup).SendAsync("OrderStatusUpdated", order);
}
