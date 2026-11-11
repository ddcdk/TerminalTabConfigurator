using System.IO;
using System.Runtime.InteropServices;

namespace TerminalTabConfigurator.Services;

/// <summary>Creates desktop shortcuts (.lnk) that run wt.exe with a layout's arguments.</summary>
public static class ShortcutService
{
    /// <summary>The wt.exe execution alias installed with Windows Terminal, or null.</summary>
    public static string? FindWtExe()
    {
        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        return File.Exists(alias) ? alias : null;
    }

    public static string GetPath(string layoutName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Sanitize(layoutName) + ".lnk");

    public static bool Exists(string layoutName) => File.Exists(GetPath(layoutName));

    /// <param name="runAsAdministrator">Sets the shortcut's "Run as administrator" flag (needed for elevated layouts).</param>
    /// <param name="iconFile">Shortcut icon; only .ico, .exe and .dll files are used (otherwise the Windows Terminal icon).</param>
    public static string Create(string layoutName, string arguments, bool runAsAdministrator = false, string? iconFile = null)
    {
        var wt = FindWtExe() ?? throw new InvalidOperationException("wt.exe est introuvable : Windows Terminal est-il installé ?");
        var path = GetPath(layoutName);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
                        ?? throw new InvalidOperationException("WScript.Shell n'est pas disponible.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            try
            {
                link.TargetPath = wt;
                link.Arguments = arguments;
                link.Description = $"Ouvre la disposition « {layoutName} » dans Windows Terminal";
                if (iconFile is not null && Path.GetExtension(iconFile).ToLowerInvariant() is ".ico" or ".exe" or ".dll")
                    link.IconLocation = iconFile + ",0";
                link.Save();
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }

        if (runAsAdministrator) SetRunAsAdministrator(path);
        return path;
    }

    /// <summary>
    /// WScript.Shell cannot set "Run as administrator": it is flag 0x2000 (SLDF_RUNAS_USER) of the
    /// little-endian LinkFlags field at offset 0x14 of the .lnk header, i.e. bit 0x20 of byte 0x15.
    /// </summary>
    private static void SetRunAsAdministrator(string path)
    {
        var bytes = File.ReadAllBytes(path);
        bytes[0x15] |= 0x20;
        File.WriteAllBytes(path, bytes);
    }

    public static void Delete(string layoutName)
    {
        var path = GetPath(layoutName);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length > 0 ? clean : "Disposition";
    }
}
