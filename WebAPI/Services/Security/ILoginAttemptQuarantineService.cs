namespace WebAPI.Services.Security
{
    public sealed record LoginQuarantineState(
        bool IsQuarantined,
        int FailedAttempts,
        DateTimeOffset? ExpiresAtUtc);

    public interface ILoginAttemptQuarantineService
    {
        LoginQuarantineState GetState(string? remoteAddress);
        LoginQuarantineState RegisterFailure(string? remoteAddress);
        void ClearFailures(string? remoteAddress);
    }
}
