using System.Runtime.InteropServices;

namespace TalkType.Desktop;

internal static partial class NativeMethods
{
    internal const int WmHotkey = 0x0312;
    internal const uint ModWin = 0x0008;
    internal const uint ModControl = 0x0002;
    internal const uint ModAlt = 0x0001;
    internal const uint ModShift = 0x0004;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr window, int id);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr window, out NativeRect rectangle);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    internal static partial void keybd_event(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
