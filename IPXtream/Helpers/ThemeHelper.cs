using System;
using System.Windows;
using System.Windows.Media;

namespace IPXtream.Helpers;

public static class ThemeHelper
{
    private class ThemeColors
    {
        public string BgDeep { get; set; } = "#E50F0F1A";
        public string BgSidebar { get; set; } = "#B312122A";
        public string BgCard { get; set; } = "#1F1A1A2E";
        public string BgHover { get; set; } = "#334F8EF7";
        public string AccentBlue { get; set; } = "#4F8EF7";
        public string AccentPurple { get; set; } = "#7C5CBF";
        public string TextPrimary { get; set; } = "#E8E8F0";
        public string TextMuted { get; set; } = "#888899";
        public string InputBg { get; set; } = "#4D12122A";
        public string InputBorder { get; set; } = "#337C5CBF";
        public string DividerColor { get; set; } = "#22FFFFFF";
    }

    public static void ApplyTheme(string themeName, Window? targetWindow = null)
    {
        var colors = themeName switch
        {
            "Midnight Black" => new ThemeColors
            {
                BgDeep = "#E5060608",
                BgSidebar = "#B30B0B0E",
                BgCard = "#12FFFFFF",
                BgHover = "#22FFFFFF",
                AccentBlue = "#3B82F6",
                AccentPurple = "#8B5CF6",
                TextPrimary = "#F3F4F6",
                TextMuted = "#9CA3AF",
                InputBg = "#33000000",
                InputBorder = "#44FFFFFF",
                DividerColor = "#22FFFFFF"
            },
            "Cyberpunk Neon" => new ThemeColors
            {
                BgDeep = "#E50A0015",
                BgSidebar = "#B314002A",
                BgCard = "#18FF007F",
                BgHover = "#4DFF007F",
                AccentBlue = "#00F0FF",
                AccentPurple = "#FF007F",
                TextPrimary = "#FFFFFF",
                TextMuted = "#B57CFF",
                InputBg = "#3314002A",
                InputBorder = "#FF007F",
                DividerColor = "#FF007F"
            },
            "Forest Green" => new ThemeColors
            {
                BgDeep = "#E50A140F",
                BgSidebar = "#B30E1E16",
                BgCard = "#12FFFFFF",
                BgHover = "#334EC994",
                AccentBlue = "#4EC994",
                AccentPurple = "#2E7D32",
                TextPrimary = "#EAF5F0",
                TextMuted = "#8CA89C",
                InputBg = "#330E1E16",
                InputBorder = "#2E5A44",
                DividerColor = "#22FFFFFF"
            },
            "Light Ocean" => new ThemeColors
            {
                BgDeep = "#E5F0F4F8",
                BgSidebar = "#B3FFFFFF",
                BgCard = "#4DFFFFFF",
                BgHover = "#80FFFFFF",
                AccentBlue = "#1E40AF",
                AccentPurple = "#6D28D9",
                TextPrimary = "#1E293B",
                TextMuted = "#64748B",
                InputBg = "#4DFFFFFF",
                InputBorder = "#B0CBD5E1",
                DividerColor = "#40CBD5E1"
            },
            _ => new ThemeColors() // Default "Dark Purple"
        };

        ApplyToDictionary(Application.Current.Resources, colors);
        
        if (targetWindow != null)
        {
            ApplyToDictionary(targetWindow.Resources, colors);
        }

        foreach (Window window in Application.Current.Windows)
        {
            ApplyToDictionary(window.Resources, colors);
        }
    }

    private static void ApplyToDictionary(ResourceDictionary dict, ThemeColors colors)
    {
        dict["BgDeep"] = CreateBrush(colors.BgDeep);
        dict["BgSidebar"] = CreateBrush(colors.BgSidebar);
        dict["BgCard"] = CreateBrush(colors.BgCard);
        dict["BgHover"] = CreateBrush(colors.BgHover);
        dict["AccentBlue"] = CreateBrush(colors.AccentBlue);
        dict["AccentPurple"] = CreateBrush(colors.AccentPurple);
        dict["TextPrimary"] = CreateBrush(colors.TextPrimary);
        dict["TextMuted"] = CreateBrush(colors.TextMuted);
        dict["InputBg"] = CreateBrush(colors.InputBg);
        dict["InputBorder"] = CreateBrush(colors.InputBorder);
        dict["DividerColor"] = CreateBrush(colors.DividerColor);
        
        // Dynamic Accent Colors (needed for effects like DropShadowEffect)
        dict["AccentBlueColor"] = ConvertColor(colors.AccentBlue);
        dict["AccentPurpleColor"] = ConvertColor(colors.AccentPurple);

        // Dynamic Accent Gradient Brush
        dict["PrimaryAccentGradient"] = CreateGradientBrush(colors.AccentBlue, colors.AccentPurple);
    }

    private static SolidColorBrush CreateBrush(string hexColor)
    {
        var brush = new SolidColorBrush(ConvertColor(hexColor));
        brush.Freeze();
        return brush;
    }

    private static Color ConvertColor(string hexColor)
    {
        return (Color)ColorConverter.ConvertFromString(hexColor);
    }

    private static LinearGradientBrush CreateGradientBrush(string startHex, string endHex)
    {
        var startColor = ConvertColor(startHex);
        var endColor = ConvertColor(endHex);
        var brush = new LinearGradientBrush(startColor, endColor, new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }
}
