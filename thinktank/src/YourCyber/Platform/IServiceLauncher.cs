namespace YourCyber.Platform;

public interface IServiceLauncher
{
    bool IsSupported { get; }
    Task<bool> TryLaunchAsync();
}
