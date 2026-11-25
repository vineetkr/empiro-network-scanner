using System.Windows;
using System.Windows.Media;
namespace Empiro.App;
public static class ThemeManager
{
    public static void Apply(bool light)
    {
        var colors = light ? new[] { "#F3F6FA", "#FFFFFF", "#FFFFFF", "#E8EEF8", "#15243A", "#55647A", "#D7DFEA", "#376FEA", "#157653", "#956000" }
            : new[] { "#0B101B", "#101724", "#141D2D", "#1B273A", "#EDF2FA", "#97A7BE", "#263248", "#538BFF", "#6DDEB6", "#F2BF78" };
        var keys = new[] { "BackgroundBrush", "SidebarBrush", "CardBrush", "InputBrush", "TextBrush", "MutedBrush", "BorderBrush", "AccentBrush", "GreenBrush", "AmberBrush" };
        for (var i = 0; i < keys.Length; i++) Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
}
