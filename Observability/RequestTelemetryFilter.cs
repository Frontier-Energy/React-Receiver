namespace React_Receiver.Observability;

public static class RequestTelemetryFilter
{
    public static bool ShouldCollect(PathString path)
    {
        if (!path.HasValue)
        {
            return true;
        }

        return !path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
    }
}
