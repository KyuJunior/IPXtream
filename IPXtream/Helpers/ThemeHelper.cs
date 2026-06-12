using System;
using System.Windows;
using System.Windows.Media;

namespace IPXtream.Helpers;

public static class ThemeHelper
{
    private class ThemeColors
    {
        public string BgDeep { get; set; } = "#0F0F1A";
        public string BgSidebar { get; set; } = "#12122A";
        public string BgCard { get; set; } = "#1A1A2E";
        public string BgHover { get; set; } = "#22224A";
        public string AccentBlue { get; set; } = "#4F8EF7";
        public string AccentPurple { get; set; } = "#7C5CBF";
        public string TextPrimary { get; set; } = "#E8E8F0";
        public string TextMuted { get; set; } = "#888899";
        public string InputBg { get; set; } = "#12122A";
        public string InputBorder { get; set; } = "#2E2E50";
        public string DividerColor { get; set; } = "#1E1E3A";
    }

    public static void ApplyTheme(string themeName)
    {
        var colors = themeName switch
        {
            "Midnight Black" => new ThemeColors
            {
                BgDeep = "#060608",
                BgSidebar = "#0B0B0E",
                BgCard = "#121216",
                BgHover = "#1A1A22",
                AccentBlue = "#3B82F6",
                AccentPurple = "#8B5CF6",
                TextPrimary = "#F3F4F6",
                TextMuted = "#9CA3AF",
                InputBg = "#0B0B0E",
                InputBorder = "#1F2937",
                DividerColor = "#111827"
            },
            "Cyberpunk Neon" => new ThemeColors
            {
                BgDeep = "#0A0015",
                BgSidebar = "#14002A",
                BgCard = "#1F003F",
                BgHover = "#3D007A",
                AccentBlue = "#00F0FF",
                AccentPurple = "#FF007F",
                TextPrimary = "#FFFFFF",
                TextMuted = "#B57CFF",
                InputBg = "#14002A",
                InputBorder = "#FF007F",
                DividerColor = "#3D007A"
            },
            "Forest Green" => new ThemeColors
            {
                BgDeep = "#0A140F",
                BgSidebar = "#0E1E16",
                BgCard = "#162E22",
                BgHover = "#204432",
                AccentBlue = "#4EC994",
                AccentPurple = "#2E7D32",
                TextPrimary = "#EAF5F0",
                TextMuted = "#8CA89C",
                InputBg = "#0E1E16",
                InputBorder = "#2E5A44",
                DividerColor = "#183628"
            },
            "Light Ocean" => new ThemeColors
            {
                BgDeep = "#F0F4F8",
                BgSidebar = "#E1E8F0",
                BgCard = "#FFFFFF",
                BgHover = "#D3E2F2",
                AccentBlue = "#1E40AF",
                AccentPurple = "#6D28D9",
                TextPrimary = "#1E293B",
                TextMuted = "#64748B",
                InputBg = "#FFFFFF",
                InputBorder = "#CBD5E1",
                DividerColor = "#E2E8F0"
            },
            _ => new ThemeColors() // Default "Dark Purple"
        };

        ApplyToDictionary(Application.Current.Resources, colors);
        
        if (Application.Current.MainWindow != null)
        {
            ApplyToDictionary(Application.Current.MainWindow.Resources, colors);
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
    }

    private static SolidColorBrush CreateBrush(string hexColor)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        return brush;
    }
}
