namespace TalkType.Desktop;

internal static class ChatMicPlacement
{
    public const int Width = 34;
    public const int Height = 34;

    // Anchor to the measured toolbar, never to a guessed window-edge offset.
    public static Rectangle GetBounds(Rectangle composer, Rectangle anchor,
        Rectangle window, Rectangle[] occupied)
    {
        if (anchor.IsEmpty) return Rectangle.Empty;
        var candidate = new Rectangle(anchor.Left - Width - 6,
            anchor.Top + (anchor.Height - Height) / 2, Width, Height);
        if (!window.Contains(candidate) || candidate.Left < composer.Left ||
            candidate.Top < composer.Top - 8 || candidate.Bottom > composer.Bottom + 8)
            return Rectangle.Empty;
        var clearance = Rectangle.Inflate(candidate, 3, 3);
        return occupied.Any(rectangle => rectangle.IntersectsWith(clearance))
            ? Rectangle.Empty : candidate;
    }
}
