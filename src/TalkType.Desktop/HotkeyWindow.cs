namespace TalkType.Desktop;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x4C49;
    public event EventHandler? Pressed;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    public void Register(AppSettings settings)
    {
        if (!TryRegister(settings))
            throw new InvalidOperationException($"{settings.HotkeyLabel} is already in use.");
    }

    public bool TryRegister(AppSettings settings)
    {
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        return NativeMethods.RegisterHotKey(Handle, HotkeyId, settings.HotkeyModifiers, (uint)settings.HotkeyKey);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmHotkey && message.WParam == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref message);
    }

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        DestroyHandle();
    }
}
