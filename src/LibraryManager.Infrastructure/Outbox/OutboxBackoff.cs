namespace LibraryManager.Infrastructure.Outbox;

internal static class OutboxBackoff
{
    public static TimeSpan Compute(int attemptCount, int maxSeconds)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 8);
        var seconds = Math.Min(maxSeconds, Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(seconds);
    }
}
