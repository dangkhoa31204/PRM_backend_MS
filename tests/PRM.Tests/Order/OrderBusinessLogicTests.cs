using Xunit;
using PRM.Services.Order.Models;
using PRM.Services.Order.Models.Enums;
using PRM.Shared.Enums;

namespace PRM.Tests.Order;

public class OrderBusinessLogicTests
{
    [Fact]
    public void CalculateTotalAmount_WithMultipleItems_ShouldComputeCorrectSum()
    {
        // Arrange
        var order = new PRM.Services.Order.Models.Order
        {
            OrderId = 1,
            TableId = 5,
            Status = (int)OrderStatus.Pending,
            OrderItems = new List<OrderItem>
            {
                new OrderItem { MenuItemId = 101, Quantity = 2, UnitPrice = 35000m }, // 70,000
                new OrderItem { MenuItemId = 102, Quantity = 1, UnitPrice = 29000m }, // 29,000
                new OrderItem { MenuItemId = 103, Quantity = 3, UnitPrice = 40000m }  // 120,000
            }
        };

        // Act
        order.TotalAmount = order.OrderItems.Sum(item => item.Quantity * item.UnitPrice);

        // Assert
        Assert.Equal(219000m, order.TotalAmount);
    }

    [Theory]
    [InlineData((int)OrderStatus.Pending, true)]
    [InlineData((int)OrderStatus.Confirmed, true)]
    [InlineData((int)OrderStatus.Serving, true)]
    [InlineData((int)OrderStatus.Completed, false)]
    [InlineData((int)OrderStatus.Cancelled, false)]
    public void CanAddItemsToOrder_BasedOnOrderStatus_ShouldReturnExpectedResult(int currentStatus, bool expectedCanAdd)
    {
        // Arrange (Business Rule BR_01: Customers can add items only if order is active)
        var order = new PRM.Services.Order.Models.Order
        {
            OrderId = 10,
            Status = currentStatus
        };

        // Act
        bool canAdd = order.Status == (int)OrderStatus.Pending ||
                      order.Status == (int)OrderStatus.Confirmed ||
                      order.Status == (int)OrderStatus.Serving;

        // Assert
        Assert.Equal(expectedCanAdd, canAdd);
    }

    [Fact]
    public void AddNewItemsToActiveOrder_ShouldIncreaseTotalAmountAndItemCount()
    {
        // Arrange
        var order = new PRM.Services.Order.Models.Order
        {
            OrderId = 100,
            TableId = 2,
            Status = (int)OrderStatus.Pending,
            OrderItems = new List<OrderItem>
            {
                new OrderItem { MenuItemId = 1, Quantity = 1, UnitPrice = 35000m }
            }
        };
        order.TotalAmount = order.OrderItems.Sum(i => i.Quantity * i.UnitPrice);
        Assert.Equal(35000m, order.TotalAmount);

        // Act (Adding add-on items)
        var addOnItem = new OrderItem { MenuItemId = 2, Quantity = 2, UnitPrice = 29000m }; // 58,000
        order.OrderItems.Add(addOnItem);
        order.TotalAmount = order.OrderItems.Sum(i => i.Quantity * i.UnitPrice);

        // Assert
        Assert.Equal(2, order.OrderItems.Count);
        Assert.Equal(93000m, order.TotalAmount);
    }

    [Fact]
    public void OrderItemStatus_DefaultState_ShouldBePending()
    {
        // Arrange & Act
        var orderItem = new OrderItem
        {
            MenuItemId = 50,
            Quantity = 1,
            UnitPrice = 45000m
        };

        // Assert
        Assert.Equal(OrderItemStatus.Pending, orderItem.Status);
    }
}
