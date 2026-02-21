using System.Runtime.InteropServices;

namespace YourCyber.Platform;

public static class ServiceLauncherFactory
{
    public static IServiceLauncher Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsServiceLauncher();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacServiceLauncher();

        return new UnsupportedServiceLauncher();
    }
}
