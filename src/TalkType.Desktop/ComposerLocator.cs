using System.Windows.Automation;

namespace TalkType.Desktop;

internal static class ComposerLocator
{
    public static bool TryFind(IntPtr window, bool isDiscord, out Rectangle bounds)
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
                bounds = Rectangle.FromLTRB(
                    (int)Math.Round(rectangle.Left),
                    (int)Math.Round(rectangle.Top),
                    (int)Math.Round(rectangle.Right),
                    (int)Math.Round(rectangle.Bottom));
            }
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
    }

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
