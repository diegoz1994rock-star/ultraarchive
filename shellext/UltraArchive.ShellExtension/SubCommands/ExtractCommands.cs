#nullable enable
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension.SubCommands;

// Verbos de extracción / apertura. Se OCULTAN (ECS_HIDDEN) si la selección no contiene ningún
// archivo comprimido reconocido (mismas 8 extensiones que el submenú clásico). Son verbos de un
// solo elemento; si hay varios seleccionados, UltraArchive.exe recibe todas las rutas y actúa
// sobre la primera (igual que hoy con "%1").

[GeneratedComClass]
internal partial class CmdOpen : ExplorerCommandBase
{
    protected override string? GetStaticTitle() => "Abrir con Ultra Archive";
    protected override EXPCMDSTATE GetCommandState(IShellItemArray? psia) =>
        ArchiveSelection.HasArchive(psia) ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchivePerItem(null, psia);
}

[GeneratedComClass]
internal partial class CmdExtractFiles : ExplorerCommandBase
{
    protected override string? GetStaticTitle() => "Extraer ficheros…";
    protected override EXPCMDSTATE GetCommandState(IShellItemArray? psia) =>
        ArchiveSelection.HasArchive(psia) ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchivePerItem("--extract-to", psia);
}

[GeneratedComClass]
internal partial class CmdExtractHereFlat : ExplorerCommandBase
{
    protected override string? GetStaticTitle() => "Extraer aquí";
    protected override EXPCMDSTATE GetCommandState(IShellItemArray? psia) =>
        ArchiveSelection.HasArchive(psia) ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchivePerItem("--extract-here-flat", psia);
}

[GeneratedComClass]
internal partial class CmdExtractToSubfolder : ExplorerCommandBase
{
    protected override bool HasDynamicTitle => true;
    protected override string? GetDynamicTitle(IShellItemArray? psia) =>
        $"Extraer en \"{GetSelectionStem(psia)}\\\"";
    protected override EXPCMDSTATE GetCommandState(IShellItemArray? psia) =>
        ArchiveSelection.HasArchive(psia) ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchivePerItem("--extract-here", psia);
}
