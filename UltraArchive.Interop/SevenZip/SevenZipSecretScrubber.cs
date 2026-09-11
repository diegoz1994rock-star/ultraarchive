using System.Text;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Defensa en profundidad: elimina de cualquier texto de diagnóstico (stderr/stdout de 7zr, mensajes
/// de error) toda aparición literal de la contraseña, y recorta la longitud.
///
/// La contraseña de UltraArchive nunca debería llegar a la salida de 7zr, pero un nombre de fichero
/// podría contener por casualidad la misma cadena; este scrubber garantiza que no acabe en el
/// resultado ni en los logs.
/// </summary>
internal static class SevenZipSecretScrubber
{
    private const string Mask = "***";
    private const int MinSecretLengthToScrub = 3;

    public static string? Scrub(string? text, string? secret, int maxLength = SevenZipExecutionDefaults.MaxDiagnosticChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var result = text;

        if (!string.IsNullOrEmpty(secret) && secret.Length >= MinSecretLengthToScrub)
        {
            result = result.Replace(secret, Mask, StringComparison.Ordinal);
        }

        if (result.Length > maxLength)
        {
            result = result[^maxLength..];
        }

        return result;
    }

    /// <summary>Acumulador acotado de líneas de diagnóstico, ya limpiadas de la contraseña.</summary>
    public sealed class BoundedBuffer
    {
        private readonly StringBuilder _builder = new();
        private readonly string? _secret;
        private readonly int _maxLength;

        public BoundedBuffer(string? secret, int maxLength = SevenZipExecutionDefaults.MaxDiagnosticChars)
        {
            _secret = secret;
            _maxLength = maxLength;
        }

        public void Append(string? line)
        {
            var safe = Scrub(line, _secret, _maxLength);
            if (string.IsNullOrEmpty(safe))
            {
                return;
            }

            if (_builder.Length > 0)
            {
                _builder.Append('\n');
            }

            _builder.Append(safe);

            if (_builder.Length > _maxLength)
            {
                _builder.Remove(0, _builder.Length - _maxLength);
            }
        }

        public bool HasContent => _builder.Length > 0;

        public override string ToString() => _builder.ToString();
    }
}
