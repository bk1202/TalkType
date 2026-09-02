namespace TalkType.Desktop;

internal static class ChatMicPlacement
{
    public const int Width = 34;
    public const int Height = 34;

    // Restore the original in-bar overlay offsets, without a gap requirement.
    public static Rectangle GetBounds(Rectangle composer, Rectangle window, bool isDiscord)
    {
        var x = Math.Min(composer.Right + 8, window.Right - (isDiscord ? 277 : 126));
        var y = composer.Top + Math.Max(0, (composer.Height - Height) / 2);
        return new Rectangle(x, y, Width, Height);
    }
}
