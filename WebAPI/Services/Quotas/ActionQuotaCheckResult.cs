namespace WebAPI.Services.Quotas
{
    public sealed record ActionQuotaCheckResult(
        bool Allowed,
        string Message,
        TimeSpan RetryAfter)
    {
        public static ActionQuotaCheckResult AllowedResult { get; } = new(true, string.Empty, TimeSpan.Zero);
    }
}
