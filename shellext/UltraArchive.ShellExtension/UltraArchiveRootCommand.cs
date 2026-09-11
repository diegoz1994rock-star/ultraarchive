#nullable enable
using System;
using System.Runtime.InteropServices.Marshalling;
using UltraArchive.ShellExtension.SubCommands;

namespace UltraArchive.ShellExtension;

/// <summary>
/// Verbo raíz "Ultra Archive" del menú contextual moderno. Es un desplegable
/// (<see cref="EXPCMDFLAGS.ECF_HASSUBCOMMANDS"/>) cuyos sub-verbos lanzan <c>UltraArchive.exe</c>
/// con los mismos flags que el submenú clásico (<c>RegistryShellIntegrationService</c> / MSI).
/// </summary>
[GeneratedComClass]
internal partial class UltraArchiveRootCommand : ExplorerCommandBase
{
    /// <summary>CLSID fijo — también en <c>manifest/AppxManifest.xml</c> (com:Class y Verb).</summary>
    public static readonly Guid CLSID = new("A99F6CA6-1436-4063-BA8A-156692A771A8");

    protected override string? GetStaticTitle() => "Ultra Archive";

    protected override EXPCMDFLAGS GetCommandFlags() => EXPCMDFLAGS.ECF_HASSUBCOMMANDS;

    protected override EXPCMDSTATE GetCommandState(IShellItemArray? psia) =>
        // Si no se localiza UltraArchive.exe, ocultar el menú entero en vez de ofrecer verbos muertos.
        UltraArchiveShellLocator.FindExecutable() is null
            ? EXPCMDSTATE.ECS_HIDDEN
            : EXPCMDSTATE.ECS_ENABLED;

    public override int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = CLSID;
        return 0;
    }

    public override int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        var e = new CommandEnumerator();

        // --- Compresión (siempre visible, sobre archivos y carpetas) ---
        e.AddCommand(new CmdCompress());
        e.AddCommand(new CmdCompressHere());
        e.AddCommand(new CmdCompressZip());
        e.AddCommand(new CmdCompress7z());
        e.AddCommand(new CmdCompressSplit());

        // --- Extracción (los sub-verbos se ocultan solos si la selección no es un archivo) ---
        e.AddCommand(new CmdSeparator());
        e.AddCommand(new CmdOpen());
        e.AddCommand(new CmdExtractFiles());
        e.AddCommand(new CmdExtractHereFlat());
        e.AddCommand(new CmdExtractToSubfolder());

        ppEnum = e;
        return 0;
    }
}
