namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Datos <b>documentales</b> del binario <c>7zr.exe</c> que UltraArchive espera encontrar bundleado
/// para la creación de archivos 7Z cifrados (Fase 6A).
///
/// El binario <b>está incorporado</b> en <c>tools/7zr.exe</c> (LZMA SDK 26.02, build x86, dominio
/// público) y <see cref="ExpectedSha256"/> fija su SHA-256; <see cref="SevenZipLocator"/> lo verifica
/// (hash + banner) y reporta <see cref="SevenZipToolStatus.Available"/>. Si <see cref="ExpectedSha256"/>
/// se vaciara, la capacidad vuelve a <see cref="SevenZipToolStatus.NotConfigured"/> sin ejecutar nada.
///
/// Fuente / licencia (ver también THIRD-PARTY-NOTICES.txt en la raíz del repo):
///   - Binario:  7zr.exe (versión reducida de 7z.exe, solo formato 7z), incluido en el "LZMA SDK".
///   - Origen:   https://www.7-zip.org/sdk.html
///   - Licencia: dominio público. La página del SDK lo declara literalmente:
///               "LZMA SDK is placed in the public domain. Anyone is free to copy, modify, publish,
///                use, compile, sell, or distribute the original LZMA SDK code, either in source code
///                form or as a compiled binary, for any purpose, commercial or non-commercial, and by
///                any means."
///               Referencia SPDX: LZMA-SDK-9.22 (https://spdx.org/licenses/LZMA-SDK-9.22.html).
/// </summary>
public static class SevenZipToolReference
{
    /// <summary>Nombre del ejecutable (solo el nombre; la ruta la fija <see cref="SevenZipLocator"/>).</summary>
    public const string ExecutableFileName = "7zr.exe";

    /// <summary>Subcarpeta, relativa al directorio de la aplicación, donde debe estar el binario.</summary>
    public const string ToolsDirectoryName = "tools";

    /// <summary>Versión del LZMA SDK del que se tomó el binario bundleado.</summary>
    public const string SdkVersion = "26.02";

    /// <summary>Fecha de publicación de esa versión del SDK (documental).</summary>
    public const string SdkReleaseDate = "2026-06-25";

    /// <summary>URL canónica del origen del binario.</summary>
    public const string SourceUrl = "https://www.7-zip.org/sdk.html";

    /// <summary>Identificador de licencia.</summary>
    public const string LicenseIdentifier = "LZMA-SDK-9.22 (dominio público)";

    /// <summary>
    /// SHA-256 en hex (mayúsculas) del <c>7zr.exe</c> exacto bundleado en <c>tools/7zr.exe</c>
    /// (LZMA SDK 26.02, build x86, 602 112 bytes). <see cref="SevenZipLocator"/> se niega a ejecutar
    /// cualquier binario cuyo hash no coincida. Si se vacía, la capacidad de 7Z cifrado se desactiva.
    /// </summary>
    public const string ExpectedSha256 = "56B8CC9F4971CEF253644FAFE54063ED7FDCA551D4DEE0F8C6BAA81B855ACD72";

    /// <summary>
    /// Versión mínima de 7-Zip aceptada. Se exige una razonablemente moderna para poder confiar en
    /// <c>-bsp1</c> (progreso por stdout), <c>-scsUTF-8</c> y el cifrado AES-256 de cabecera
    /// (<c>-mhe=on</c>), todos presentes y estables desde hace años.
    /// </summary>
    public static readonly Version MinimumVersion = new(19, 0);
}
