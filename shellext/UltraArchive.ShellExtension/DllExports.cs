#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace UltraArchive.ShellExtension;

internal static class DllExports
{
    private static readonly StrategyBasedComWrappers s_comWrappers = new();

    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    public static unsafe int DllGetClassObject(Guid* rclsid, Guid* riid, nint* ppv)
    {
        *ppv = 0;

        if (*rclsid != UltraArchiveRootCommand.CLSID)
            return unchecked((int)0x80040111); // CLASS_E_CLASSNOTAVAILABLE

        try
        {
            var factory = new UltraArchiveClassFactory();
            nint comPtr = s_comWrappers.GetOrCreateComInterfaceForObject(factory, CreateComInterfaceFlags.None);

            Guid iid = *riid;
            int hr = Marshal.QueryInterface(comPtr, in iid, out nint result);
            Marshal.Release(comPtr);

            if (hr < 0)
                return hr;

            *ppv = result;
            return 0; // S_OK
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    // Servidor COM in-proc de Native AOT: el runtime .NET está enlazado estáticamente en esta DLL
    // y no soporta ser descargado (sus hilos de GC y finalizador siguen vivos). Dejar que el host
    // haga FreeLibrary desmapearía un runtime en marcha y provocaría un crash, así que siempre
    // devolvemos S_FALSE ("no descargar"). El módulo queda residente mientras viva el proceso host,
    // que es la política estándar y segura para un servidor COM .NET in-proc.
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    public static int DllCanUnloadNow() => 1; // S_FALSE
}
