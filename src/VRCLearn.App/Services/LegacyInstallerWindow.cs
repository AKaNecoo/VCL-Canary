using System.Windows.Interop;

namespace VRCLearn.App.Services;

// Published official installers find the launcher by its original window title.
internal sealed class LegacyInstallerWindow : IDisposable
{
    private const int WmClose = 0x0010;
    private const int WsExToolWindow = 0x0080;
    private readonly HwndSource _source;
    private readonly Action _closeLauncher;

    public LegacyInstallerWindow(Action closeLauncher)
    {
        _closeLauncher = closeLauncher;
        _source = new HwndSource(new HwndSourceParameters("VRCLearn Launcher")
        {
            WindowStyle = 0, // A hidden top-level window that FindWindow can locate.
            ExtendedWindowStyle = WsExToolWindow,
        });
        _source.AddHook(HandleMessage);
    }

    private IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmClose)
        {
            handled = true;
            _ = _source.Dispatcher.BeginInvoke(_closeLauncher);
        }

        return IntPtr.Zero;
    }

    public void Dispose() => _source.Dispose();
}
