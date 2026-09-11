#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace UltraArchive.ShellExtension;

/// <summary>
/// Reconocimiento de archivos comprimidos por extensión. La lista es la misma que
/// <c>RegistryShellIntegrationService.OpenWithExtensions</c> / el <c>AppliesTo</c> del MSI:
/// las 8 extensiones que UltraArchive sabe abrir. Se usa para ocultar los verbos de extracción
/// cuando la selección no contiene ningún archivo.
/// </summary>
internal static class ArchiveDetection
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".iso",
    };

    public static bool IsArchive(string path)
    {
        string ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && ArchiveExtensions.Contains(ext);
    }

    public static bool SelectionHasArchive(IShellItemArray? psia)
    {
        if (psia is null)
            return false;

        try
        {
            psia.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                psia.GetItemAt(i, out IShellItem item);
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out nint namePtr);
                string path = Marshal.PtrToStringUni(namePtr) ?? string.Empty;
                Marshal.FreeCoTaskMem(namePtr);

                if (IsArchive(path))
                    return true;
            }
        }
        catch
        {
            // ante cualquier fallo, ser conservador: no mostrar extracción
        }

        return false;
    }
}
