#nullable enable
using System;
using System.IO;
using Microsoft.Win32;

namespace UltraArchive.ShellExtension;

/// <summary>
/// Localiza <c>UltraArchive.exe</c> en el equipo. Estrategia (solo HKLM y Program Files — nunca
/// HKCU ni PATH, que el usuario sin privilegios podría manipular para secuestrar el menú):
///
///   1. Entrada de "Agregar o quitar programas" del MSI: <c>...\Uninstall\*</c> cuyo
///      <c>DisplayName</c> sea "UltraArchive" → <c>InstallLocation\UltraArchive.exe</c>.
///   2. Respaldo fijo: <c>%ProgramFiles%\UltraArchive\UltraArchive.exe</c> y su variante x86.
///
/// El resultado se cachea durante la vida del proceso surrogate.
/// </summary>
internal static class UltraArchiveShellLocator
{
    private static string? s_cached;
    private static bool s_resolved;

    public static string? FindExecutable()
    {
        if (s_resolved)
            return s_cached;

        s_resolved = true;
        s_cached = ResolveFromUninstallEntry() ?? ResolveFromProgramFiles();
        return s_cached;
    }

    private static string? ResolveFromUninstallEntry()
    {
        // 64 y 32 bits (WOW6432Node), por si el MSI se registrara en la vista de 32.
        string[] roots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        foreach (string rootPath in roots)
        {
            try
            {
                using RegistryKey? root = Registry.LocalMachine.OpenSubKey(rootPath);
                if (root is null)
                    continue;

                foreach (string subName in root.GetSubKeyNames())
                {
                    try
                    {
                        using RegistryKey? sub = root.OpenSubKey(subName);
                        if (sub?.GetValue("DisplayName") as string is not { } displayName)
                            continue;
                        if (!displayName.Equals("UltraArchive", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (sub.GetValue("InstallLocation") as string is { Length: > 0 } installLocation)
                        {
                            string candidate = Path.Combine(installLocation.TrimEnd('"'), "UltraArchive.exe");
                            if (File.Exists(candidate))
                                return candidate;
                        }
                    }
                    catch
                    {
                        // clave suelta mal formada: seguir
                    }
                }
            }
            catch
            {
                // vista de registro no disponible: seguir
            }
        }

        return null;
    }

    private static string? ResolveFromProgramFiles()
    {
        string[] bases =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        foreach (string b in bases)
        {
            if (string.IsNullOrEmpty(b))
                continue;

            string candidate = Path.Combine(b, "UltraArchive", "UltraArchive.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
