using PRM.Shared.Enums;
using System.Net.Http.Json;

namespace PRM.Services.Order.Clients;

public class RestaurantServiceClient : IRestaurantServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestaurantServiceClient> _logger;

    public RestaurantServiceClient(HttpClient httpClient, ILogger<RestaurantServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<int, MenuItemInfo>?> ValidateAndGetItemsAsync(IEnumerable<int> menuItemIds)
    {
        try
        {
            var ids = string.Join(",", menuItemIds);
            var response = await _httpClient.GetAsync($"api/menu/validate-items?ids={ids}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Restaurant Service returned {StatusCode} for validate-items", response.StatusCode);
                return null;
            }

            var raw = await response.Content.ReadFromJsonAsync<Dictionary<int, MenuItemDto>>();
            if (raw == null) return null;

            return raw.ToDictionary(
                kv => kv.Key,
                kv => new MenuItemInfo(kv.Value.MenuItemId, kv.Value.Name, kv.Value.Price, kv.Value.Category));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Restaurant Service validate-items");
            return null;
        }
    }

    public async Task<int?> GetTableStatusAsync(int tableId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/tables/{tableId}/status");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<int>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Restaurant Service table status for tableId={TableId}", tableId);
            return null;
        }
    }

    public async Task<bool> ReleaseTableAsync(int tableId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/tables/{tableId}/release", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing table {TableId}", tableId);
            return false;
        }
    }

    public async Task<bool> OccupyTableAsync(int tableId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/tables/{tableId}/occupy", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occupying table {TableId}", tableId);
            return false;
        }
    }

    // Internal DTO matching Restaurant Service response shape
    private record MenuItemDto(int MenuItemId, string Name, string? Description, decimal Price, MenuCategory Category, string? ImageUrl, bool IsAvailable, DateTime CreatedAt, DateTime? UpdatedAt);
}
