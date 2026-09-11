#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension;

[GeneratedComClass]
internal partial class UltraArchiveClassFactory : IClassFactoryLocal
{
    private static readonly StrategyBasedComWrappers s_comWrappers = new();

    public unsafe int CreateInstance(nint pUnkOuter, Guid* riid, out nint ppvObject)
    {
        ppvObject = 0;

        if (pUnkOuter != 0)
            return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION

        try
        {
            var command = new UltraArchiveRootCommand();
            nint comPtr = s_comWrappers.GetOrCreateComInterfaceForObject(command, CreateComInterfaceFlags.None);

            Guid iid = *riid;
            int hr = Marshal.QueryInterface(comPtr, in iid, out nint result);
            Marshal.Release(comPtr);

            if (hr < 0)
                return hr;

            ppvObject = result;
            return 0; // S_OK
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    // El módulo nunca se descarga (ver DllExports.DllCanUnloadNow): no hay contador de bloqueo
    // que mantener; solo se acusa recibo.
    public int LockServer(bool fLock) => 0; // S_OK
}
