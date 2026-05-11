using FluentAssertions;
using Moq;
using TicketShop.Application.Auth;
using TicketShop.Core.Entities;
using TicketShop.Infrastructure.Auth;
using TicketShop.Tests.Infrastructure;

namespace TicketShop.Tests;

public class AuthServiceTests
{
    private static SqliteTestDbFactory CreateFactoryWithAdmin()
    {
        var factory = new SqliteTestDbFactory();
        factory.Seed(users: new[]
        {
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                Role = "Admin"
            }
        });
        return factory;
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_DelegatesTokenCreationToTokenService()
    {
        using var factory = CreateFactoryWithAdmin();
        using var ctx = factory.CreateContext();

        var tokenServiceMock = new Mock<ITokenService>(MockBehavior.Strict);
        tokenServiceMock
            .Setup(t => t.CreateToken(It.Is<User>(u => u.Username == "admin" && u.Role == "Admin")))
            .Returns("signed.jwt.token")
            .Verifiable();

        var sut = new AuthService(ctx, tokenServiceMock.Object);

        var response = await sut.LoginAsync(new LoginRequest("admin", "admin"));

        response.Should().NotBeNull();
        response!.Token.Should().Be("signed.jwt.token");
        response.Username.Should().Be("admin");
        response.Role.Should().Be("Admin");

        tokenServiceMock.Verify();
        tokenServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUser_ReturnsNullAndNeverCallsTokenService()
    {
        using var factory = CreateFactoryWithAdmin();
        using var ctx = factory.CreateContext();

        var tokenServiceMock = new Mock<ITokenService>(MockBehavior.Strict);
        var sut = new AuthService(ctx, tokenServiceMock.Object);

        var response = await sut.LoginAsync(new LoginRequest("ghost", "whatever"));

        response.Should().BeNull();
        tokenServiceMock.Verify(t => t.CreateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNullAndNeverCallsTokenService()
    {
        using var factory = CreateFactoryWithAdmin();
        using var ctx = factory.CreateContext();

        var tokenServiceMock = new Mock<ITokenService>(MockBehavior.Strict);
        var sut = new AuthService(ctx, tokenServiceMock.Object);

        var response = await sut.LoginAsync(new LoginRequest("admin", "wrong-password"));

        response.Should().BeNull();
        tokenServiceMock.Verify(t => t.CreateToken(It.IsAny<User>()), Times.Never);
    }
}
