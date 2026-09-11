#nullable enable
using System;
using System.Runtime.InteropServices;

namespace UltraArchive.ShellExtension;

/// <summary>
/// Envoltorio COM sobre <see cref="ArchiveDetection.IsArchive"/>: recorre un
/// <see cref="IShellItemArray"/> (la selección del Explorador) y decide si contiene al menos un
/// archivo comprimido reconocido. Separado de <see cref="ArchiveDetection"/> (que es pura, sin COM)
/// para que esta última se pueda enlazar tal cual en los tests xUnit.
/// </summary>
internal static class ArchiveSelection
{
    public static bool HasArchive(IShellItemArray? psia)
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

                if (ArchiveDetection.IsArchive(path))
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
