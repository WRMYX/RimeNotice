using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace RimeNotice.Services;
public static class SettingsRowLayout
{
    public static void Apply(Grid row)
    {
        if (row.ActualWidth <= 0 || row.Children.Count < 2 || row.Children[1] is not FrameworkElement editor) return;
        var labels = (FrameworkElement)row.Children[0];
        double available = Math.Max(0, row.ActualWidth - row.Padding.Left - row.Padding.Right);
        double editorWidth = double.IsFinite(editor.Width) ? editor.Width : editor is ToggleSwitch ? 52 : 180;
        bool narrow = available < editorWidth + 160 + 12;
        row.RowSpacing = narrow ? 8 : 0;
        labels.VerticalAlignment = VerticalAlignment.Center;
        if (editor is ToggleSwitch toggle) { toggle.MinWidth = 0; toggle.Width = 52; }
        Grid.SetColumn(editor, narrow ? 0 : 1);
        Grid.SetRow(editor, narrow ? 1 : 0);
        Grid.SetColumnSpan(editor, narrow ? 2 : 1);
        Grid.SetColumnSpan(labels, narrow ? 2 : 1);
        editor.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        editor.VerticalAlignment = VerticalAlignment.Center;
        if (editor is Control control && editor is not ToggleSwitch)
            control.VerticalContentAlignment = VerticalAlignment.Center;
        editor.MaxWidth = available;
        editor.Margin = new Thickness(0);
    }
}
