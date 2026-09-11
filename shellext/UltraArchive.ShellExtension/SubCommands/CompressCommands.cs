#nullable enable
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension.SubCommands;

// Verbos de compresión. Siempre visibles (archivos y carpetas). Cada uno lanza UltraArchive.exe
// con el mismo flag que usa el submenú clásico. Multi-selección: LaunchUltraArchive pasa TODAS
// las rutas en un solo proceso.

[GeneratedComClass]
internal partial class CmdCompress : ExplorerCommandBase
{
    protected override string? GetStaticTitle() => "Comprimir…";
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchive("--compress", psia);
}

[GeneratedComClass]
internal partial class CmdCompressHere : ExplorerCommandBase
{
    protected override string? GetStaticTitle() => "Comprimir aquí";
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchive("--compress-here", psia);
}

[GeneratedComClass]
internal partial class CmdCompressZip : ExplorerCommandBase
{
    protected override bool HasDynamicTitle => true;
    protected override string? GetDynamicTitle(IShellItemArray? psia) =>
        $"Comprimir en \"{GetSelectionStem(psia)}.zip\"";
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchive("--compress-zip", psia);
}

[GeneratedComClass]
internal partial class CmdCompress7z : ExplorerCommandBase
{
    protected override bool HasDynamicTitle => true;
    protected override string? GetDynamicTitle(IShellItemArray? psia) =>
        $"Comprimir en \"{GetSelectionStem(psia)}.7z\"";
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchive("--compress-7z", psia);
}

[GeneratedComClass]
internal partial class CmdCompressSplit : ExplorerCommandBase
{
    protected override string? GetStaticTitle() => "Comprimir y dividir…";
    protected override int OnInvoke(IShellItemArray? psia) => LaunchUltraArchive("--compress-split", psia);
}
