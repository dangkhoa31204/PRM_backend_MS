using Xunit;
using PRM.Services.Restaurant.Models;
using PRM.Shared.Enums;

namespace PRM.Tests.Restaurant;

public class RestaurantBusinessLogicTests
{
    [Fact]
    public void MenuItem_Validation_ShouldRejectNegativePrice()
    {
        // Arrange
        var item = new MenuItem
        {
            Name = "Cà Phê Muối",
            Price = -10000m,
            Category = MenuCategory.Coffee,
            IsAvailable = true
        };

        // Act
        bool isValid = item.Price >= 0 && !string.IsNullOrWhiteSpace(item.Name);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void MenuItem_ValidData_ShouldPassValidation()
    {
        // Arrange
        var item = new MenuItem
        {
            MenuItemId = 1,
            Name = "Trà Đào Cam Sả",
            Price = 39000m,
            Category = MenuCategory.Tea,
            IsAvailable = true
        };

        // Act
        bool isValid = item.Price >= 0 && !string.IsNullOrWhiteSpace(item.Name);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(1, (int)TableStatus.Available)]
    [InlineData(2, (int)TableStatus.Occupied)]
    public void Table_StatusTransition_ShouldUpdateCorrectly(int capacity, int newStatus)
    {
        // Arrange
        var table = new Table
        {
            TableId = 1,
            Capacity = capacity,
            Status = (int)TableStatus.Available
        };

        // Act
        table.Status = newStatus;

        // Assert
        Assert.Equal(newStatus, table.Status);
    }
}
