using System.Windows;
using System.Windows.Controls;

namespace DiscoBox.Ui.Controls;

public class StatusChip : Control
{
    private static readonly DependencyPropertyKey DisplayTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DisplayText), typeof(string), typeof(StatusChip),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(string), typeof(StatusChip),
            new PropertyMetadata("unknown", OnVisualPropertyChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusChip),
            new PropertyMetadata(null, OnVisualPropertyChanged));

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? DisplayText => (string?)GetValue(DisplayTextProperty);

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chip = (StatusChip)d;
        chip.SetValue(DisplayTextPropertyKey, chip.Text ?? chip.Status);
    }
}
