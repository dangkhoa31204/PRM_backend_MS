namespace PRM.Services.Order.Notifications;

public interface INotificationService
{
    Task NotifyOrderCreatedAsync(object order);
    Task NotifyOrderUpdatedAsync(object order);
    Task NotifyOrderStatusUpdatedAsync(object order);
    Task NotifyPaymentUpdatedAsync(object payment);
    Task NotifyFeedbackCreatedAsync(object feedback);
    Task NotifyFeedbackVisibilityChangedAsync(object feedback);
}
