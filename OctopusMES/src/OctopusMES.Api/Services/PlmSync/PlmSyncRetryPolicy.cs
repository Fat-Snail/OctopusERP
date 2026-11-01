namespace OctopusMES.Api.Services.PlmSync;

public static class PlmSyncRetryPolicy
{
    private static readonly int[] Minutes = [1, 5, 15, 30, 60];

    public static TimeSpan? GetDelay(int failedAttempt) =>
        failedAttempt is >= 1 and <= 5 ? TimeSpan.FromMinutes(Minutes[failedAttempt - 1]) : null;
}
