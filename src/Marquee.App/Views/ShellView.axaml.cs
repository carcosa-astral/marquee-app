using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Marquee.App.Views;

public partial class ShellView : UserControl
{
    private TopLevel? _topLevel;

    public ShellView() => InitializeComponent();

    // Ctrl K (Cmd K on macOS) jumps to the search box, as the top bar hint says.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command && e.Key == Key.K)
        {
            TopSearch.Focus();
            TopSearch.SelectAll();
            e.Handled = true;
        }
    }
}
