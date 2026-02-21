namespace YourCyber.Platform;

public class UnsupportedServiceLauncher : IServiceLauncher
{
    public bool IsSupported => false;

    public Task<bool> TryLaunchAsync()
    {
        return Task.FromResult(false);
    }
}
