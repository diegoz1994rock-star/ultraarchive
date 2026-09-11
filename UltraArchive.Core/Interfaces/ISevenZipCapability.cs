namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Responde, en tiempo de ejecución, si UltraArchive puede crear <b>7Z cifrado</b> en esta
/// instalación. Devuelve true <b>únicamente</b> cuando existe un <c>7zr.exe</c> localizado y
/// verificado (hash + versión). Nunca se asume true por el simple hecho de que el código de
/// integración exista.
///
/// La implementación vive en <c>UltraArchive.Interop</c> (envuelve al localizador del binario); la
/// interfaz está aquí para que <c>EncryptionSupport</c> y la UI puedan consultarla sin depender de
/// la capa de interop.
/// </summary>
public interface ISevenZipCapability
{
    /// <summary>True solo si hay un 7zr.exe verificado disponible para crear 7Z cifrado.</summary>
    bool CanCreateEncryptedSevenZip { get; }

    /// <summary>Ruta absoluta al 7zr.exe verificado, o null si no está disponible.</summary>
    string? VerifiedExecutablePath { get; }

    /// <summary>Explicación legible del estado (para mensajes de UI y de error). Nunca contiene datos sensibles.</summary>
    string StatusExplanation { get; }
}
