using Xunit;
using PRM.Services.Identity.Models;

namespace PRM.Tests.Identity;

public class IdentityBusinessLogicTests
{
    [Fact]
    public void Account_DefaultStatus_ShouldBeActive()
    {
        // Arrange & Act
        var account = new Account
        {
            Username = "staff_user",
            Email = "staff@aroma.com",
            PasswordHash = "hashed_pw",
            FullName = "Staff Member",
            Role = 2, // Staff
            IsActive = true
        };

        // Assert
        Assert.True(account.IsActive);
        Assert.Equal(2, account.Role);
    }

    [Theory]
    [InlineData(1, "Admin")]
    [InlineData(2, "Staff")]
    [InlineData(0, "Customer")]
    public void Account_RoleMapping_ShouldIdentifyCorrectRoleName(int roleId, string expectedRoleName)
    {
        // Arrange
        var account = new Account { Role = roleId };

        // Act
        string roleName = account.Role switch
        {
            1 => "Admin",
            2 => "Staff",
            _ => "Customer"
        };

        // Assert
        Assert.Equal(expectedRoleName, roleName);
    }
}
