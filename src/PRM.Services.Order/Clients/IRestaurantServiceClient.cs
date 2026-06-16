using PRM.Shared.Enums;

namespace PRM.Services.Order.Clients;

/// <summary>
/// Client interface để giao tiếp với Restaurant Service.
/// Order Service dùng interface này thay vì gọi DB trực tiếp.
/// </summary>
public interface IRestaurantServiceClient
{
    /// <summary>Kiểm tra danh sách món ăn hợp lệ và lấy thông tin giá/tên.</summary>
    Task<Dictionary<int, MenuItemInfo>?> ValidateAndGetItemsAsync(IEnumerable<int> menuItemIds);

    /// <summary>Kiểm tra bàn có tồn tại không (trả về status).</summary>
    Task<int?> GetTableStatusAsync(int tableId);

    /// <summary>Giải phóng bàn về Available sau khi đơn hoàn thành/hủy.</summary>
    Task<bool> ReleaseTableAsync(int tableId);

    /// <summary>Đặt bàn sang Occupied khi có đơn mới.</summary>
    Task<bool> OccupyTableAsync(int tableId);
}

public record MenuItemInfo(int MenuItemId, string Name, decimal Price, MenuCategory Category);
