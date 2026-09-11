#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace UltraArchive.ShellExtension;

/// <summary>
/// Reconocimiento de archivos comprimidos por extensión. La lista es la misma que
/// <c>RegistryShellIntegrationService.OpenWithExtensions</c> / el <c>AppliesTo</c> del MSI:
/// las 8 extensiones que UltraArchive sabe abrir. Se usa para ocultar los verbos de extracción
/// cuando la selección no contiene ningún archivo.
///
/// Lógica pura (sin COM): por eso vive en su propio fichero, para que
/// <c>UltraArchive.ShellExtension.Tests</c> pueda enlazarlo con <c>&lt;Compile Include&gt;</c> sin
/// arrastrar los tipos COM de <see cref="ArchiveSelection"/>.
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
}
