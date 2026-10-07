using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SpotlightNative.Controls;

public sealed class HighlightTextBlock : TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(HighlightTextBlock),
        new PropertyMetadata("", OnHighlightChanged));
    public static readonly DependencyProperty MatchedIndicesProperty = DependencyProperty.Register(
        nameof(MatchedIndices), typeof(IReadOnlyList<int>), typeof(HighlightTextBlock),
        new PropertyMetadata(Array.Empty<int>(), OnHighlightChanged));

    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IReadOnlyList<int> MatchedIndices
    {
        get => (IReadOnlyList<int>)GetValue(MatchedIndicesProperty);
        set => SetValue(MatchedIndicesProperty, value);
    }

    private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HighlightTextBlock)d;
        control.Inlines.Clear();
        var indices = control.MatchedIndices.ToHashSet();
        for (var i = 0; i < control.Value.Length;)
        {
            var highlighted = indices.Contains(i);
            var start = i;
            while (i < control.Value.Length && indices.Contains(i) == highlighted) i++;
            control.Inlines.Add(new Run(control.Value[start..i])
            {
                Foreground = highlighted ? System.Windows.Media.Brushes.DeepSkyBlue : control.Foreground,
                FontWeight = highlighted ? FontWeights.Bold : control.FontWeight
            });
        }
    }
}
