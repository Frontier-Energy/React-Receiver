namespace React_Receiver.Services;

public sealed class StorageInfrastructureOptions
{
    public bool EnableOnStartup { get; set; }
    public bool ValidateDependenciesOnStartup { get; set; } = true;
}
