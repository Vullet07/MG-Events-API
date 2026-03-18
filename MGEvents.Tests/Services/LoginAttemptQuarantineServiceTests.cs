using Microsoft.Extensions.Caching.Memory;
using WebAPI.Services.Security;

namespace MGEvents.Tests.Services;

public class LoginAttemptQuarantineServiceTests
{
    private static LoginAttemptQuarantineService CreateService()
    {
        return new LoginAttemptQuarantineService(new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public void RegisterFailure_QuarantinesAfterSixthFailureWithinWindow()
    {
        var service = CreateService();

        for (var index = 0; index < 5; index++)
        {
            var state = service.RegisterFailure("127.0.0.1");
            Assert.False(state.IsQuarantined);
            Assert.Equal(index + 1, state.FailedAttempts);
        }

        var quarantine = service.RegisterFailure("127.0.0.1");

        Assert.True(quarantine.IsQuarantined);
        Assert.Equal(6, quarantine.FailedAttempts);
        Assert.NotNull(quarantine.ExpiresAtUtc);
    }

    [Fact]
    public void ClearFailures_RemovesAccumulatedAttempts()
    {
        var service = CreateService();

        service.RegisterFailure("127.0.0.2");
        service.RegisterFailure("127.0.0.2");
        Assert.Equal(2, service.GetState("127.0.0.2").FailedAttempts);

        service.ClearFailures("127.0.0.2");

        var state = service.GetState("127.0.0.2");
        Assert.False(state.IsQuarantined);
        Assert.Equal(0, state.FailedAttempts);
    }
}
