using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TerminalTabConfigurator.Core.Settings;

namespace TerminalTabConfigurator;

/// <summary>Profiles of the current settings.json, for display lookups.</summary>
public static class ProfileCatalog
{
    public static IReadOnlyList<TerminalProfile> Profiles { get; set; } = [];

    public static string Describe(string? reference)
    {
        if (reference is null) return "Profil par défaut";
        var profile = Profiles.FirstOrDefault(p => p.Matches(reference));
        return profile is null ? reference : Label(profile);
    }

    /// <summary>Profile name, flagged " (admin)" when it runs elevated and its name does not already say so.</summary>
    public static string Label(TerminalProfile profile) =>
        profile.Elevate && !profile.Name.Contains("admin", StringComparison.OrdinalIgnoreCase)
            ? $"{profile.Name} (admin)"
            : profile.Name;
}

/// <summary>Pane number (1-based) → its identifying color.</summary>
public sealed class PaneBrushConverter : IValueConverter
{
    private static readonly Brush[] Palette =
        new[] { "#60CDFF", "#F7B955", "#C18FFF", "#6CCB5F", "#FF8C8C", "#4FD1C5", "#F4A3D6", "#B5B5B5" }
            .Select(c => (Brush)Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(c))))
            .ToArray();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int n && n > 0 ? Palette[(n - 1) % Palette.Length] : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Freezable Freeze(Freezable f)
    {
        f.Freeze();
        return f;
    }
}

/// <summary>Profile reference (GUID or name) → profile name.</summary>
public sealed class ProfileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ProfileCatalog.Describe(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>"#RRGGBB" → brush (transparent when empty or invalid).</summary>
public sealed class HexBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));
            }
            catch (FormatException)
            {
            }
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true → Collapsed, false → Visible.</summary>
public sealed class InverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>null → Collapsed, anything else → Visible (reversed with ConverterParameter=invert).</summary>
public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value is not null && value is not string { Length: 0 };
        if (parameter is "invert") hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>null ↔ "" (lets a combo box item with an empty value stand for "none").</summary>
public sealed class NullToEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value ?? string.Empty;

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } s ? s : null;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>Windows Terminal icon value: an image path, or text (emoji, Segoe glyph).</summary>
public static class IconValue
{
    private static readonly string[] PreviewableExtensions = [".png", ".jpg", ".jpeg", ".ico", ".bmp", ".gif"];

    public static bool IsPath(string icon) => icon.Contains('\\') || icon.Contains('/') || icon.Contains('.');

    /// <summary>The icon as an existing local file (environment variables expanded), or null.</summary>
    public static string? LocalFile(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon) || !IsPath(icon)) return null;
        var path = Environment.ExpandEnvironmentVariables(icon.Trim());
        return File.Exists(path) ? path : null;
    }

    public static ImageSource? LoadPreview(string? icon)
    {
        var path = LocalFile(icon);
        if (path is null || !PreviewableExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // do not keep the file locked
            image.DecodePixelWidth = 40;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>Icon value → image preview, or null.</summary>
public sealed class IconImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => IconValue.LoadPreview(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Icon value → text to show: the emoji or glyph itself, a generic picture for a path that cannot be previewed.</summary>
public sealed class IconGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string { Length: > 0 } icon) return string.Empty;
        if (!IconValue.IsPath(icon)) return icon;
        return IconValue.LoadPreview(icon) is null ? "\uE8B9" : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
