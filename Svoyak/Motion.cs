using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Svoyak;

internal static class Motion
{
    public static void Enter(FrameworkElement element, double distance = 8, int milliseconds = 220)
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        var offset = new TranslateTransform();
        element.RenderTransform = offset;
        double opacity = element.GetAnimationBaseValue(UIElement.OpacityProperty) is double value ? value : 1;
        element.BeginAnimation(UIElement.OpacityProperty, Animate(opacity * 0.35, opacity, milliseconds));
        offset.BeginAnimation(TranslateTransform.YProperty, Animate(distance, 0, milliseconds));
    }

    public static void Pulse(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;
        var animation = Animate(1.13, 1, 240);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static DoubleAnimation Animate(double from, double to, int milliseconds) => new()
    {
        From = from,
        To = to,
        Duration = TimeSpan.FromMilliseconds(milliseconds),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };
}
