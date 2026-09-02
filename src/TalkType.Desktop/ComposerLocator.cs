using System.Windows.Automation;

namespace TalkType.Desktop;

internal static class ComposerLocator
{
    public static bool TryFindMicBounds(IntPtr window, bool isDiscord, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        try
        {
            var root = AutomationElement.FromHandle(window);
            var windowRectangle = root.Current.BoundingRectangle;
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            var elements = root.FindAll(TreeScope.Descendants, condition);
            var bestScore = double.MinValue;
            AutomationElement? composerElement = null;

            foreach (AutomationElement element in elements)
            {
                var current = element.Current;
                if (current.IsOffscreen || !current.IsEnabled) continue;
                var rectangle = current.BoundingRectangle;
                if (rectangle.Width < 180 || rectangle.Height is < 20 or > 220) continue;
                if (rectangle.Top < windowRectangle.Top + windowRectangle.Height * 0.55) continue;

                var name = current.Name ?? string.Empty;
                if (!IsMessageComposerName(name, isDiscord)) continue;
                if (IsInsideDialog(element, root)) continue;
                var score = rectangle.Bottom + rectangle.Width / 100;
                if (score <= bestScore) continue;
                bestScore = score;
                composerElement = element;
                bounds = Rectangle.FromLTRB(
                    (int)Math.Round(rectangle.Left),
                    (int)Math.Round(rectangle.Top),
                    (int)Math.Round(rectangle.Right),
                    (int)Math.Round(rectangle.Bottom));
            }
            if (composerElement is null) return false;
            var composer = bounds;
            bounds = Rectangle.Empty;
            var occupied = new List<Rectangle>();
            var toolbar = new List<Rectangle>();
            var buttons = root.FindAll(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox)));
            foreach (AutomationElement button in buttons)
            {
                var current = button.Current;
                if (current.IsOffscreen) continue;
                var rect = ToRectangle(current.BoundingRectangle);
                if (rect.IsEmpty) continue;
                occupied.Add(rect); // Disabled buttons also need to remain visible.
                var centerY = rect.Top + rect.Height / 2;
                if (rect.Width is >= 16 and <= 100 && rect.Height is >= 16 and <= 80 &&
                    rect.Left >= composer.Left + composer.Width / 2 &&
                    centerY >= composer.Top && centerY <= composer.Bottom &&
                    rect.Left <= composer.Right + 350 && !IsInsideDialog(button, root))
                    toolbar.Add(rect);
            }
            if (toolbar.Count == 0) return false;

            // The mic may sit in unused input space, but never on written text.
            // If the provider cannot tell us where text is, don't guess.
            if (composerElement.TryGetCurrentPattern(TextPattern.Pattern, out var pattern))
            {
                var range = ((TextPattern)pattern).DocumentRange;
                if (!string.IsNullOrWhiteSpace(range.GetText(-1)))
                {
                    var rectangles = range.GetBoundingRectangles();
                    if (rectangles.Length == 0) return false;
                    foreach (var rectangle in rectangles)
                        occupied.Add(ToRectangle(rectangle));
                }
            }
            else if (!composerElement.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ||
                !string.IsNullOrWhiteSpace(((ValuePattern)value).Current.Value)) return false;

            bounds = ChatMicPlacement.GetBounds(composer, toolbar.OrderBy(rect => rect.Left).First(),
                ToRectangle(windowRectangle), occupied.ToArray());
            return !bounds.IsEmpty;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static Rectangle ToRectangle(System.Windows.Rect rectangle) => rectangle.IsEmpty
        ? Rectangle.Empty
        : Rectangle.FromLTRB((int)Math.Floor(rectangle.Left), (int)Math.Floor(rectangle.Top),
            (int)Math.Ceiling(rectangle.Right), (int)Math.Ceiling(rectangle.Bottom));

    private static bool IsMessageComposerName(string name, bool isDiscord)
    {
        var normalized = name.Trim();
        if (isDiscord)
        {
            // Discord exposes chat inputs as “Message #channel” or
            // “Message @person”. Search and settings fields use other names.
            return normalized.StartsWith("Message #", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Message @", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Message", StringComparison.OrdinalIgnoreCase);
        }

        return normalized.Equals("Type a message", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Message", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsideDialog(AutomationElement element, AutomationElement root)
    {
        var parent = TreeWalker.ControlViewWalker.GetParent(element);
        for (var depth = 0; parent is not null && !parent.Equals(root) && depth < 40; depth++)
        {
            var current = parent.Current;
            // Profile cards also expose "Message @name" inputs. They are not
            // the channel composer and must not win the bottommost-field match.
            if (current.ControlType == ControlType.Window ||
                current.LocalizedControlType.Equals("dialog", StringComparison.OrdinalIgnoreCase)) return true;
            parent = TreeWalker.ControlViewWalker.GetParent(parent);
        }
        return false;
    }
}
