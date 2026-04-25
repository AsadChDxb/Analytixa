using AdHocReporting.Infrastructure.Security;
using FluentAssertions;

namespace AdHocReporting.Tests.Services;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashAndVerify_ShouldWork()
    {
        var hasher = new PasswordHasher();
        const string password = "Admin@12345";

        var hash = hasher.HashPassword(password);

        hash.Should().NotBeNullOrWhiteSpace();
        hasher.VerifyPassword(password, hash).Should().BeTrue();
        hasher.VerifyPassword("wrong", hash).Should().BeFalse();
    }
}
