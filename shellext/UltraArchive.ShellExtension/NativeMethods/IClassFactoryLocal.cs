#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension;

// IClassFactory propia (la generada por otros toolkits suele venir sellada / no implementable).
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IClassFactoryLocal
{
    [PreserveSig]
    unsafe int CreateInstance(nint pUnkOuter, Guid* riid, out nint ppvObject);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}
