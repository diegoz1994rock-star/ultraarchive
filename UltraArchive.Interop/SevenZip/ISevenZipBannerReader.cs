namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Obtiene el texto del banner de un ejecutable de 7-Zip. Es el <b>único</b> punto de la Fase 6A que
/// lanza un proceso, y solo para leer la versión — no ejecuta ninguna operación de archivo.
/// Se aísla tras esta interfaz para que <see cref="SevenZipLocator"/> y sus pruebas no dependan de
/// que haya un 7zr.exe real en la máquina.
/// </summary>
public interface ISevenZipBannerReader
{
    /// <summary>
    /// Ejecuta <paramref name="executablePath"/> sin argumentos de operación y devuelve lo que
    /// escribe (stdout+stderr), o null si no arranca / no responde a tiempo. No debe lanzar.
    /// </summary>
    string? ReadBanner(string executablePath);
}
