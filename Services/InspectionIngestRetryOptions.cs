namespace React_Receiver.Services;

public sealed class InspectionIngestRetryOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int BatchSize { get; set; } = 100;
    public int MaxConcurrentSessions { get; set; } = 8;
}
