using System.Runtime.InteropServices;
using Microsoft.Win32;
using UltraArchive.Core.Interfaces;

namespace UltraArchive.Shell;

/// <summary>
/// Integración con el menú contextual del Explorador de Windows mediante <b>solo el registro</b>
/// (sin DLL de extensión de shell, sin servicios, sin tareas).
///
/// Estructura que crea (bajo <c>Software\Classes</c> del <b>árbol indicado</b> — HKCU por defecto,
/// HKLM para "todos los usuarios" desde el instalador):
///
///   • <c>*\shell\UltraArchive</c>  y  <c>Directory\shell\UltraArchive</c>
///        → submenú desplegable <b>"Ultra Archive"</b> (<c>SubCommands=""</c> + <c>\shell</c> anidado).
///        Sub-verbos de <b>compresión</b> (siempre visibles) y de <b>extracción</b> (con
///        <c>AppliesTo</c> filtrado a las extensiones de archivo que UltraArchive sabe abrir, así
///        solo aparecen sobre archivos comprimidos).
///   • <c>Applications\UltraArchive.exe</c> + <c>&lt;.ext&gt;\OpenWithList\UltraArchive.exe</c>
///        → "Abrir con UltraArchive" (candidato, nunca predeterminado). Sin cambios respecto a antes.
///
/// <see cref="Uninstall"/> borra <b>exactamente</b> lo que crea <see cref="Install"/> (y las entradas
/// del esquema antiguo, para que actualizar desde una versión previa deje todo limpio), y nada ajeno.
/// </summary>
public sealed class RegistryShellIntegrationService : IShellIntegrationService
{
    /// <summary>Extensiones para "Abrir con UltraArchive" y para los verbos de extracción.</summary>
    public static readonly IReadOnlyList<string> OpenWithExtensions = new[]
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".iso",
    };

    /// <summary>Extensiones que activan los verbos de extracción del submenú (mismas que arriba).</summary>
    public static readonly IReadOnlyList<string> ExtractableExtensions = OpenWithExtensions;

    private const string AppRegistrationName = "UltraArchive.exe";
    private const string MenuKeyName = "UltraArchive";          // clave del submenú "Ultra Archive"
    private const string MenuLabel = "Ultra Archive";

    // Verbos del esquema ANTERIOR (Fase 6B), que Uninstall también limpia.
    private const string LegacyExtractHereVerb = "UltraArchive.ExtractHere";
    private const string LegacyExtractToVerb = "UltraArchive.ExtractTo";

    private readonly string _executablePath;
    private readonly RegistryKey _baseKey;      // HKCU (por defecto) o HKLM
    private readonly string _classesRoot;       // relativa al baseKey, normalmente "Software\Classes"
    private readonly bool _ownsBaseKey;

    public RegistryShellIntegrationService(string executablePath, string classesRoot = @"Software\Classes")
        : this(executablePath, Registry.CurrentUser, classesRoot, ownsBaseKey: false)
    {
    }

    private RegistryShellIntegrationService(string executablePath, RegistryKey baseKey, string classesRoot, bool ownsBaseKey)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("La ruta del ejecutable no puede estar vacía.", nameof(executablePath));
        }

        _executablePath = executablePath;
        _baseKey = baseKey;
        _classesRoot = classesRoot;
        _ownsBaseKey = ownsBaseKey;
    }

    /// <summary>Servicio para el usuario actual (HKCU). No requiere administrador. Usado por el botón "Explorador" de la app.</summary>
    public static RegistryShellIntegrationService ForCurrentProcess() =>
        new(Environment.ProcessPath ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable."));

    /// <summary>Servicio para todos los usuarios (HKLM). Requiere administrador. Usado por el instalador.</summary>
    public static RegistryShellIntegrationService ForAllUsers() =>
        new(Environment.ProcessPath ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable."),
            Registry.LocalMachine, @"Software\Classes", ownsBaseKey: false);

    private string ClassesRoot => _classesRoot;

    public bool IsInstalled
    {
        get
        {
            using var key = _baseKey.OpenSubKey($@"{ClassesRoot}\*\shell\{MenuKeyName}\shell\30_compress\command");
            return key?.GetValue(null) is string command
                   && command.Contains(_executablePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ==================================================================================
    //  INSTALAR
    // ==================================================================================
    public void Install()
    {
        var quotedExe = $"\"{_executablePath}\"";
        var iconValue = $"\"{_executablePath}\",0";
        var archiveAppliesTo = BuildAppliesToArchives();

        // --- Submenú "Ultra Archive" sobre cualquier archivo y sobre carpetas ---
        InstallMenu($@"{ClassesRoot}\*\shell\{MenuKeyName}", iconValue, quotedExe, archiveAppliesTo, includeExtract: true);
        InstallMenu($@"{ClassesRoot}\Directory\shell\{MenuKeyName}", iconValue, quotedExe, archiveAppliesTo, includeExtract: false);

        // --- "Abrir con UltraArchive" (candidato, sin tocar el predeterminado) ---
        using (var app = _baseKey.CreateSubKey($@"{ClassesRoot}\Applications\{AppRegistrationName}"))
        {
            app.SetValue("FriendlyAppName", "UltraArchive");
            using (var command = app.CreateSubKey(@"shell\open\command"))
            {
                command.SetValue(null, $"{quotedExe} \"%1\"");
            }

            using var supported = app.CreateSubKey("SupportedTypes");
            foreach (var ext in OpenWithExtensions)
            {
                supported.SetValue(ext, string.Empty);
            }
        }

        foreach (var ext in OpenWithExtensions)
        {
            using var openWith = _baseKey.CreateSubKey($@"{ClassesRoot}\{ext}\OpenWithList\{AppRegistrationName}");
            // La mera existencia de la subclave añade la app al submenú "Abrir con".
        }

        NotifyShellAssociationsChanged();
    }

    private void InstallMenu(string menuKeyPath, string iconValue, string quotedExe, string archiveAppliesTo, bool includeExtract)
    {
        using (var menu = _baseKey.CreateSubKey(menuKeyPath))
        {
            menu.SetValue("MUIVerb", MenuLabel);
            menu.SetValue("Icon", iconValue);
            // Cadena vacía -> el submenú se construye a partir de la subclave "shell" anidada.
            menu.SetValue("SubCommands", string.Empty);
        }

        // Sub-verbos. El prefijo numérico fija el orden en el menú.
        if (includeExtract)
        {
            AddSubVerb(menuKeyPath, "10_open", "Abrir con Ultra Archive", $"{quotedExe} \"%1\"", iconValue,
                appliesTo: archiveAppliesTo, multiSelect: false);
            AddSubVerb(menuKeyPath, "11_extractfiles", "Extraer ficheros…", $"{quotedExe} --extract-to \"%1\"", iconValue,
                appliesTo: archiveAppliesTo, multiSelect: false);
            AddSubVerb(menuKeyPath, "12_extracthere", "Extraer aquí", $"{quotedExe} --extract-here-flat \"%1\"", iconValue,
                appliesTo: archiveAppliesTo, multiSelect: false);
            AddSubVerb(menuKeyPath, "13_extractsub", "Extraer a una subcarpeta con el nombre del archivo",
                $"{quotedExe} --extract-here \"%1\"", iconValue, appliesTo: archiveAppliesTo, multiSelect: false);
        }

        AddSubVerb(menuKeyPath, "30_compress", "Comprimir…", $"{quotedExe} --compress \"%1\"", iconValue,
            appliesTo: null, multiSelect: true);
        AddSubVerb(menuKeyPath, "31_compresshere", "Comprimir aquí", $"{quotedExe} --compress-here \"%1\"", iconValue,
            appliesTo: null, multiSelect: true);
        AddSubVerb(menuKeyPath, "32_zip", "Comprimir como .ZIP", $"{quotedExe} --compress-zip \"%1\"", iconValue,
            appliesTo: null, multiSelect: true);
        AddSubVerb(menuKeyPath, "33_7z", "Comprimir como .7Z", $"{quotedExe} --compress-7z \"%1\"", iconValue,
            appliesTo: null, multiSelect: true);
        AddSubVerb(menuKeyPath, "34_split", "Comprimir y dividir…", $"{quotedExe} --compress-split \"%1\"", iconValue,
            appliesTo: null, multiSelect: true);
    }

    private void AddSubVerb(string menuKeyPath, string keyName, string label, string command, string iconValue,
        string? appliesTo, bool multiSelect)
    {
        using var verb = _baseKey.CreateSubKey($@"{menuKeyPath}\shell\{keyName}");
        verb.SetValue("MUIVerb", label);
        verb.SetValue("Icon", iconValue);

        if (!string.IsNullOrEmpty(appliesTo))
        {
            verb.SetValue("AppliesTo", appliesTo);
        }

        if (multiSelect)
        {
            // "Player": el Explorador lanza UN proceso con toda la selección (varios ficheros -> un archivo).
            verb.SetValue("MultiSelectModel", "Player");
        }

        using var commandKey = verb.CreateSubKey("command");
        commandKey.SetValue(null, command);
    }

    private static string BuildAppliesToArchives() =>
        string.Join(" OR ", OpenWithExtensions.Select(ext => $"System.FileExtension:=\"{ext}\""));

    // ==================================================================================
    //  DESINSTALAR
    // ==================================================================================
    public void Uninstall()
    {
        // Esquema actual.
        DeleteSubKeyTree($@"{ClassesRoot}\*\shell\{MenuKeyName}");
        DeleteSubKeyTree($@"{ClassesRoot}\Directory\shell\{MenuKeyName}");
        DeleteSubKeyTree($@"{ClassesRoot}\Applications\{AppRegistrationName}");

        foreach (var ext in OpenWithExtensions)
        {
            DeleteSubKeyTree($@"{ClassesRoot}\{ext}\OpenWithList\{AppRegistrationName}");
            DeleteKeyIfEmpty($@"{ClassesRoot}\{ext}\OpenWithList");

            // Esquema ANTERIOR (Fase 6B): verbos planos de extracción.
            DeleteSubKeyTree($@"{ClassesRoot}\SystemFileAssociations\{ext}\shell\{LegacyExtractHereVerb}");
            DeleteSubKeyTree($@"{ClassesRoot}\SystemFileAssociations\{ext}\shell\{LegacyExtractToVerb}");
            DeleteKeyIfEmpty($@"{ClassesRoot}\SystemFileAssociations\{ext}\shell");
            DeleteKeyIfEmpty($@"{ClassesRoot}\SystemFileAssociations\{ext}");
        }

        NotifyShellAssociationsChanged();
    }

    // ==================================================================================
    //  Utilidades de registro
    // ==================================================================================
    private void DeleteSubKeyTree(string subKey)
    {
        try
        {
            _baseKey.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Best-effort: una desinstalación no debe fallar por un permiso puntual.
        }
    }

    private void DeleteKeyIfEmpty(string subKey)
    {
        try
        {
            using var key = _baseKey.OpenSubKey(subKey);
            if (key is { SubKeyCount: 0, ValueCount: 0 })
            {
                _baseKey.DeleteSubKey(subKey, throwOnMissingSubKey: false);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static void NotifyShellAssociationsChanged()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, nint.Zero, nint.Zero);
        }
        catch
        {
            // Si no está disponible, la integración funcionará igualmente tras reiniciar el Explorador.
        }
    }

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll", SetLastError = false)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, nint dwItem1, nint dwItem2);
}
