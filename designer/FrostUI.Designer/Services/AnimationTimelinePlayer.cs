using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

internal static class AnimationTimelinePlayer
{
    public static bool TryApply(FrameworkElement visual, DesignControl control)
    {
        var keyframes = control.Timeline
            .Where(frame => frame.TimeMs >= 0)
            .OrderBy(frame => frame.TimeMs)
            .ToArray();
        if (keyframes.Length == 0)
        {
            return false;
        }

        visual.Loaded += (_, _) => ApplyCore(visual, control, keyframes);
        return true;
    }

    private static void ApplyCore(
        FrameworkElement visual,
        DesignControl control,
        IReadOnlyList<AnimationKeyframe> keyframes)
    {
        var totalMs = Math.Max(1, keyframes.Max(frame => frame.TimeMs));
        var duration = TimeSpan.FromMilliseconds(totalMs);
        var repeat = control.AnimationRepeat
            ? RepeatBehavior.Forever
            : new RepeatBehavior(1);
        var translate = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var rotate = new RotateTransform();
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(rotate);
        group.Children.Add(translate);
        visual.RenderTransform = group;
        visual.RenderTransformOrigin = new Point(0.5, 0.5);

        var groups = keyframes
            .GroupBy(frame => frame.Property, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        if (groups.TryGetValue("Opacity", out var opacityFrames))
        {
            visual.BeginAnimation(
                UIElement.OpacityProperty,
                BuildAnimation(opacityFrames, Math.Clamp(control.Opacity, 0, 1), duration, repeat,
                    value => Math.Clamp(value, 0, 1)),
                HandoffBehavior.SnapshotAndReplace);
        }

        if (groups.TryGetValue("X", out var xFrames))
        {
            translate.BeginAnimation(
                TranslateTransform.XProperty,
                BuildAnimation(xFrames, 0, duration, repeat, value => value - control.X));
        }

        if (groups.TryGetValue("Y", out var yFrames))
        {
            translate.BeginAnimation(
                TranslateTransform.YProperty,
                BuildAnimation(yFrames, 0, duration, repeat, value => value - control.Y));
        }

        if (groups.TryGetValue("Scale", out var scaleFrames))
        {
            var animation = BuildAnimation(scaleFrames, 1, duration, repeat,
                value => Math.Clamp(value, 0.05, 4));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        if (groups.TryGetValue("Rotation", out var rotationFrames))
        {
            rotate.BeginAnimation(
                RotateTransform.AngleProperty,
                BuildAnimation(rotationFrames, 0, duration, repeat, value => value));
        }
    }

    private static DoubleAnimationUsingKeyFrames BuildAnimation(
        IReadOnlyList<AnimationKeyframe> frames,
        double baseValue,
        TimeSpan duration,
        RepeatBehavior repeat,
        Func<double, double> transform)
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            FillBehavior = FillBehavior.HoldEnd,
            RepeatBehavior = repeat,
        };
        if (frames[0].TimeMs > 0)
        {
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(baseValue, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        }

        foreach (var frame in frames)
        {
            var time = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(frame.TimeMs));
            var value = transform(frame.Value);
            if (string.Equals(frame.Easing, "Linear", StringComparison.OrdinalIgnoreCase))
            {
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(value, time));
            }
            else
            {
                animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, time)
                {
                    EasingFunction = frame.Easing switch
                    {
                        "BackOut" => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 },
                        "BounceOut" => new BounceEase { EasingMode = EasingMode.EaseOut },
                        _ => new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    },
                });
            }
        }

        return animation;
    }
}
