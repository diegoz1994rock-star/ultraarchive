namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Argumentos construidos para invocar 7zr.exe. <see cref="ArgumentList"/> se copia tal cual a
/// <c>ProcessStartInfo.ArgumentList</c> (cada elemento es un argumento independiente; .NET se encarga
/// del escapado). Contiene la contraseña en <c>-p&lt;...&gt;</c>.
///
/// El único formato "de línea de comandos" que se expone es <see cref="Redacted"/>, ya enmascarado.
/// </summary>
public sealed class SevenZipArguments
{
    internal SevenZipArguments(IReadOnlyList<string> argumentList) => ArgumentList = argumentList;

    /// <summary>Argumentos exactos para <c>ProcessStartInfo.ArgumentList</c>.</summary>
    public IReadOnlyList<string> ArgumentList { get; }

    /// <summary>Representación redactada (sin contraseña) para logs/diagnóstico.</summary>
    public string Redacted => SevenZipArgRedactor.Redact(ArgumentList);

    /// <summary><see cref="ToString"/> devuelve la versión redactada, no la real.</summary>
    public override string ToString() => Redacted;
}
