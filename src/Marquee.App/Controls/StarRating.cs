using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Marquee.Core.Rules;

namespace Marquee.App.Controls;

/// <summary>
/// Five stars with half steps. Click the left half of a star for a half rating.
/// Clicking the current rating again clears it. Arrow keys change it by half a star.
/// </summary>
public sealed class StarRating : Control
{
    public static readonly StyledProperty<double?> ValueProperty =
        AvaloniaProperty.Register<StarRating, double?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<StarRating, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> EmptyProperty =
        AvaloniaProperty.Register<StarRating, IBrush?>(nameof(Empty));

    public static readonly StyledProperty<double> StarSizeProperty =
        AvaloniaProperty.Register<StarRating, double>(nameof(StarSize), 26);

    private const double Gap = 4;
    private static readonly Geometry Star = StreamGeometry.Parse(
        "M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z");

    private double? _hover;

    static StarRating()
    {
        AffectsRender<StarRating>(ValueProperty, FillProperty, EmptyProperty, StarSizeProperty);
        AffectsMeasure<StarRating>(StarSizeProperty);
        FocusableProperty.OverrideDefaultValue<StarRating>(true);
        CursorProperty.OverrideDefaultValue<StarRating>(new Cursor(StandardCursorType.Hand));
    }

    public double? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }
    public double StarSize { get => GetValue(StarSizeProperty); set => SetValue(StarSizeProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(StarSize * 5 + Gap * 4, StarSize);

    public override void Render(DrawingContext context)
    {
        var shown = _hover ?? Value ?? 0;
        var scale = StarSize / 24;
        for (var i = 0; i < 5; i++)
        {
            var x = i * (StarSize + Gap);
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, 0)))
                context.DrawGeometry(Empty, null, Star);

            var portion = Math.Clamp(shown - i, 0, 1);
            if (portion <= 0)
                continue;
            using (context.PushClip(new Rect(x, 0, StarSize * portion, StarSize)))
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, 0)))
                context.DrawGeometry(Fill, null, Star);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _hover = ValueAt(e.GetPosition(this).X);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var picked = ValueAt(e.GetPosition(this).X);
        Value = Value == picked ? null : picked;
        _hover = null;
        Focus();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Right or Key.Up:
                Value = Rating.Snap((Value ?? 0) + 0.5);
                e.Handled = true;
                break;
            case Key.Left or Key.Down:
                Value = Value is null or <= Rating.Min ? null : Rating.Snap(Value.Value - 0.5);
                e.Handled = true;
                break;
            case Key.Delete or Key.Back:
                Value = null;
                e.Handled = true;
                break;
        }
    }

    private double ValueAt(double x)
    {
        var step = StarSize + Gap;
        var star = Math.Clamp((int)(x / step), 0, 4);
        var within = x - star * step;
        return star + (within < StarSize / 2 ? 0.5 : 1);
    }
}
