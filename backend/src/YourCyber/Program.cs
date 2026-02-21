using System.Runtime.InteropServices;
using Avalonia;

namespace YourCyber;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            HideDockIconOnMac();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void HideDockIconOnMac()
    {
        try
        {
            var nsApp = objc_getClass("NSApplication");
            var sharedApp = objc_msgSend(nsApp, sel_registerName("sharedApplication"));
            objc_msgSend(sharedApp, sel_registerName("setActivationPolicy:"), 1);
        }
        catch
        {
            // Best-effort
        }
    }

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string className);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName(string selectorName);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, long arg1);
}
