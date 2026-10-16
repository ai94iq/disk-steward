using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace MoleWin.App.Common;

// Motion that explains a state change. Both honor the Windows "show animations" setting.
//   Motion.Value="{Binding X}" on a ProgressBar: the bar glides to each new value (250 ms ease-out).
//   Motion.PulseOn="{Binding Counter}": the element dims and returns whenever the counter changes.
public static class Motion
{
    private static readonly Duration Glide = TimeSpan.FromMilliseconds(250);
    private static readonly IEasingFunction EaseOut = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };

    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached(
        "Value", typeof(double), typeof(Motion), new PropertyMetadata(double.NaN, OnValueChanged));

    public static readonly DependencyProperty PulseOnProperty = DependencyProperty.RegisterAttached(
        "PulseOn", typeof(object), typeof(Motion), new PropertyMetadata(null, OnPulse));

    // Motion.FadeIn="True": the element fades in when it first appears (results arriving mid-scan).
    public static readonly DependencyProperty FadeInProperty = DependencyProperty.RegisterAttached(
        "FadeIn", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnFadeInChanged));

    public static bool GetFadeIn(DependencyObject d) => (bool)d.GetValue(FadeInProperty);
    public static void SetFadeIn(DependencyObject d, bool value) => d.SetValue(FadeInProperty, value);

    public static double GetValue(DependencyObject d) => (double)d.GetValue(ValueProperty);
    public static void SetValue(DependencyObject d, double value) => d.SetValue(ValueProperty, value);
    public static object? GetPulseOn(DependencyObject d) => d.GetValue(PulseOnProperty);
    public static void SetPulseOn(DependencyObject d, object? value) => d.SetValue(PulseOnProperty, value);

    private static bool Animate => SystemParameters.ClientAreaAnimation;

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RangeBase bar || e.NewValue is not double target) return;

        // The first value lands directly: rebuilt rows must not refill from zero every update.
        if (!Animate || double.IsNaN((double)e.OldValue))
        {
            bar.BeginAnimation(RangeBase.ValueProperty, null);
            bar.Value = target;
            return;
        }
        bar.BeginAnimation(RangeBase.ValueProperty, new DoubleAnimation(target, Glide) { EasingFunction = EaseOut });
    }

    private static void OnFadeInChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true) return;

        element.Loaded += (_, _) =>
        {
            if (!Animate) return;
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = EaseOut });
        };
    }

    private static void OnPulse(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || e.OldValue is null || !Animate) return;

        var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(600) };
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600)), EaseOut));
        element.BeginAnimation(UIElement.OpacityProperty, pulse);
    }
}
