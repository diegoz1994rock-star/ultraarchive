#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension;

[GeneratedComInterface]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IEnumExplorerCommand
{
    // pceltFetched es uint* (no `out uint`): el contrato de IEnumXxx::Next permite que el
    // llamante pase NULL cuando celt == 1. Con `out uint` el stub generado lo desreferenciaría
    // siempre y provocaría una violación de acceso; la implementación comprueba NULL antes.
    [PreserveSig]
    unsafe int Next(
        uint celt,
        [MarshalUsing(CountElementName = nameof(celt))][Out]
        IExplorerCommand?[] pUICommand,
        uint* pceltFetched);

    [PreserveSig]
    int Skip(uint celt);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out IEnumExplorerCommand? ppEnum);
}
