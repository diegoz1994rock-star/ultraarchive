#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension;

// IShellItemArray se pasa como nint (puntero COM crudo): el generador de [GeneratedComInterface]
// no sabe marshalar tipos definidos por OTRO generador. ExplorerCommandBase lo envuelve a
// IShellItemArray gestionado con ComWrappers.

[GeneratedComInterface]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IExplorerCommand
{
    [PreserveSig]
    int GetTitle(nint psiItemArray, out nint ppszName);

    [PreserveSig]
    int GetIcon(nint psiItemArray, out nint ppszIcon);

    [PreserveSig]
    int GetToolTip(nint psiItemArray, out nint ppszInfotip);

    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);

    [PreserveSig]
    int GetState(
        nint psiItemArray,
        [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow,
        out EXPCMDSTATE pCmdState);

    // El orden del vtable DEBE coincidir con el IExplorerCommand del SDK de Windows
    // (shobjidl_core.h): GetState, Invoke, GetFlags, EnumSubCommands. Reordenar estos
    // métodos desplaza los slots y Explorer llamaría al método equivocado.
    [PreserveSig]
    int Invoke(nint psiItemArray, nint pbc);

    [PreserveSig]
    int GetFlags(out EXPCMDFLAGS pFlags);

    [PreserveSig]
    int EnumSubCommands(out IEnumExplorerCommand? ppEnum);
}
