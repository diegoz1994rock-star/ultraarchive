#nullable enable
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension.SubCommands;

/// <summary>Separador visual entre el grupo de compresión y el de extracción.</summary>
[GeneratedComClass]
internal partial class CmdSeparator : ExplorerCommandBase
{
    protected override EXPCMDFLAGS GetCommandFlags() => EXPCMDFLAGS.ECF_ISSEPARATOR;
}
