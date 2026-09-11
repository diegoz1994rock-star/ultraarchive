namespace UltraArchive.Core.Interfaces;

/// <summary>
/// Integración de UltraArchive con el Explorador de Windows: "Abrir con UltraArchive" y los verbos
/// de menú contextual "Extraer aquí" / "Extraer en…".
///
/// La implementación (en <c>UltraArchive.Shell</c>) escribe <b>solo bajo HKCU</b> (por usuario, sin
/// privilegios de administrador, reversible), nunca cambia la aplicación predeterminada de ningún
/// formato ni toca configuraciones de otros programas.
/// </summary>
public interface IShellIntegrationService
{
    /// <summary>True si la integración ya está instalada para el usuario actual.</summary>
    bool IsInstalled { get; }

    /// <summary>Instala/actualiza las entradas de registro bajo HKCU. Idempotente.</summary>
    void Install();

    /// <summary>Elimina todas las entradas creadas por <see cref="Install"/>. Idempotente. No toca nada ajeno.</summary>
    void Uninstall();
}
