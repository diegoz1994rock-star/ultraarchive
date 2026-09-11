#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension;

[GeneratedComClass]
internal abstract partial class ExplorerCommandBase : IExplorerCommand
{
    private static readonly StrategyBasedComWrappers s_comWrappers = new();

    // ---- Puntos de extensión para las subclases ----

    protected virtual string? GetStaticTitle() => null;
    protected virtual string? GetDynamicTitle(IShellItemArray? psia) => null;
    protected virtual bool HasDynamicTitle => false;
    protected virtual EXPCMDFLAGS GetCommandFlags() => EXPCMDFLAGS.ECF_DEFAULT;
    protected virtual EXPCMDSTATE GetCommandState(IShellItemArray? psia) => EXPCMDSTATE.ECS_ENABLED;
    protected virtual int OnInvoke(IShellItemArray? psia) => unchecked((int)0x80004001); // E_NOTIMPL

    /// <summary>Icono del menú: el del propio UltraArchive.exe (índice 0). Null si no se localiza.</summary>
    protected virtual string? GetIconResource()
    {
        string? exe = UltraArchiveShellLocator.FindExecutable();
        return exe is null ? null : exe + ",0";
    }

    // ---- IExplorerCommand ----

    public int GetTitle(nint psiItemArray, out nint ppszName)
    {
        try
        {
            IShellItemArray? psia = WrapShellItemArray(psiItemArray);
            string? title = HasDynamicTitle ? GetDynamicTitle(psia) : GetStaticTitle();
            ppszName = Marshal.StringToCoTaskMemUni(title ?? string.Empty);
            return 0;
        }
        catch
        {
            ppszName = 0;
            return unchecked((int)0x80004005); // E_FAIL
        }
    }

    public int GetIcon(nint psiItemArray, out nint ppszIcon)
    {
        try
        {
            string? icon = GetIconResource();
            if (icon is not null)
            {
                ppszIcon = Marshal.StringToCoTaskMemUni(icon);
                return 0;
            }

            ppszIcon = 0;
            return unchecked((int)0x80004001); // E_NOTIMPL → sin icono
        }
        catch
        {
            ppszIcon = 0;
            return unchecked((int)0x80004005);
        }
    }

    public int GetToolTip(nint psiItemArray, out nint ppszInfotip)
    {
        ppszInfotip = 0;
        return unchecked((int)0x80004001); // E_NOTIMPL
    }

    public virtual int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = Guid.Empty;
        return 0;
    }

    public int GetState(nint psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        try
        {
            IShellItemArray? psia = WrapShellItemArray(psiItemArray);
            pCmdState = GetCommandState(psia);
            return 0;
        }
        catch
        {
            pCmdState = EXPCMDSTATE.ECS_HIDDEN;
            return unchecked((int)0x80004005);
        }
    }

    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        pFlags = GetCommandFlags();
        return 0;
    }

    public virtual int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        ppEnum = null;
        return unchecked((int)0x80004001); // E_NOTIMPL
    }

    public int Invoke(nint psiItemArray, nint pbc)
    {
        try
        {
            IShellItemArray? psia = WrapShellItemArray(psiItemArray);
            return OnInvoke(psia);
        }
        catch
        {
            return unchecked((int)0x80004005);
        }
    }

    // ---- Utilidades compartidas ----

    private static IShellItemArray? WrapShellItemArray(nint ptr)
    {
        if (ptr == 0)
            return null;
        return (IShellItemArray)s_comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
    }

    internal static string? GetFirstFilePath(IShellItemArray? psia)
    {
        if (psia is null)
            return null;

        psia.GetCount(out uint count);
        if (count == 0)
            return null;

        psia.GetItemAt(0, out IShellItem item);
        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out nint namePtr);
        string result = Marshal.PtrToStringUni(namePtr) ?? string.Empty;
        Marshal.FreeCoTaskMem(namePtr);
        return result;
    }

    internal static List<string> GetAllFilePaths(IShellItemArray? psia)
    {
        var paths = new List<string>();
        if (psia is null)
            return paths;

        psia.GetCount(out uint count);
        for (uint i = 0; i < count; i++)
        {
            psia.GetItemAt(i, out IShellItem item);
            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out nint namePtr);
            string p = Marshal.PtrToStringUni(namePtr) ?? string.Empty;
            Marshal.FreeCoTaskMem(namePtr);
            if (p.Length > 0)
                paths.Add(p);
        }

        return paths;
    }

    /// <summary>Nombre base para los títulos dinámicos: quita la extensión (y la doble <c>.tar.gz</c>).</summary>
    internal static string GetSelectionStem(IShellItemArray? psia)
    {
        string? path = GetFirstFilePath(psia);
        if (string.IsNullOrEmpty(path))
            return "archivo";

        string name = Path.GetFileNameWithoutExtension(path);
        if (Path.GetExtension(name).Equals(".tar", StringComparison.OrdinalIgnoreCase))
            name = Path.GetFileNameWithoutExtension(name);
        return name.Length == 0 ? "archivo" : name;
    }

    /// <summary>
    /// Lanza <c>UltraArchive.exe</c> con <paramref name="flag"/> (o sin flag si es null) seguido de
    /// TODAS las rutas seleccionadas — un único proceso, equivale a <c>MultiSelectModel="Player"</c>.
    /// Cada argumento va por <see cref="ProcessStartInfo.ArgumentList"/> (el runtime lo entrecomilla),
    /// nunca una línea concatenada: así un nombre de fichero malicioso no puede inyectar switches.
    /// </summary>
    protected static int LaunchUltraArchive(string? flag, IShellItemArray? psia)
    {
        string? exe = UltraArchiveShellLocator.FindExecutable();
        if (exe is null)
            return unchecked((int)0x80004005); // E_FAIL — sin ejecutable, no hacemos nada

        List<string> paths = GetAllFilePaths(psia);
        if (paths.Count == 0)
            return unchecked((int)0x80004005);

        try
        {
            var psi = MakeStartInfo(exe);
            if (!string.IsNullOrEmpty(flag))
                psi.ArgumentList.Add(flag);
            foreach (string p in paths)
                psi.ArgumentList.Add(p);

            Process.Start(psi);
            return 0; // S_OK
        }
        catch
        {
            return unchecked((int)0x80004005);
        }
    }

    /// <summary>
    /// Verbos de un solo elemento (abrir / extraer): <c>ShellCommandLineParser</c> exige
    /// exactamente una ruta, así que se lanza <b>un proceso por cada elemento seleccionado</b>
    /// (la instancia única de UltraArchive los encola por el named pipe). Equivale a cómo el
    /// Explorador lanza los verbos clásicos sin <c>MultiSelectModel</c>.
    /// </summary>
    protected static int LaunchUltraArchivePerItem(string? flag, IShellItemArray? psia)
    {
        string? exe = UltraArchiveShellLocator.FindExecutable();
        if (exe is null)
            return unchecked((int)0x80004005);

        List<string> paths = GetAllFilePaths(psia);
        if (paths.Count == 0)
            return unchecked((int)0x80004005);

        try
        {
            foreach (string p in paths)
            {
                var psi = MakeStartInfo(exe);
                if (!string.IsNullOrEmpty(flag))
                    psi.ArgumentList.Add(flag);
                psi.ArgumentList.Add(p);
                Process.Start(psi);
            }
            return 0;
        }
        catch
        {
            return unchecked((int)0x80004005);
        }
    }

    private static ProcessStartInfo MakeStartInfo(string exe) => new()
    {
        FileName = exe,
        // Carpeta del propio ejecutable (de confianza), nunca la del archivo seleccionado
        // (podría contener una DLL plantada que se precargara en el proceso).
        WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
        UseShellExecute = false,
    };
}
