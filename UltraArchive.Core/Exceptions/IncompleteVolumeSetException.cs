using System.Text;

namespace UltraArchive.Core.Exceptions;

/// <summary>
/// Se lanza al intentar abrir/descomprimir un archivo dividido en volúmenes cuando falta al menos
/// una parte (o hay duplicados que impiden reconstruir la secuencia). <b>Nunca</b> se inicia una
/// descompresión incompleta: es preferible fallar con un mensaje claro que producir un resultado
/// aparentemente correcto con archivos faltantes.
/// </summary>
public sealed class IncompleteVolumeSetException : ArchiveException
{
    public IncompleteVolumeSetException(
        string baseName,
        IReadOnlyList<int> foundParts,
        IReadOnlyList<int> missingParts,
        IReadOnlyList<int> duplicateParts)
        : base(BuildMessage(baseName, foundParts, missingParts, duplicateParts), ArchiveErrorCategory.CorruptedArchive)
    {
        BaseName = baseName;
        FoundParts = foundParts;
        MissingParts = missingParts;
        DuplicateParts = duplicateParts;
    }

    public string BaseName { get; }
    public IReadOnlyList<int> FoundParts { get; }
    public IReadOnlyList<int> MissingParts { get; }
    public IReadOnlyList<int> DuplicateParts { get; }

    private static string BuildMessage(
        string baseName, IReadOnlyList<int> found, IReadOnlyList<int> missing, IReadOnlyList<int> duplicates)
    {
        var sb = new StringBuilder();
        sb.Append("No se puede descomprimir el archivo porque el conjunto de partes está incompleto.");
        sb.Append("\n\nPartes encontradas: ");
        sb.Append(found.Count == 0 ? "(ninguna)" : string.Join(", ", found.Select(n => n.ToString("D3"))));

        if (missing.Count > 0)
        {
            sb.Append("\nFalta: ");
            sb.Append(string.Join(", ", missing.Select(n => n.ToString("D3"))));
        }

        if (duplicates.Count > 0)
        {
            sb.Append("\nPartes duplicadas: ");
            sb.Append(string.Join(", ", duplicates.Select(n => n.ToString("D3"))));
        }

        sb.Append("\n\nColoca todas las partes (");
        sb.Append(baseName);
        sb.Append(".001, .002, …) en la misma carpeta y vuelve a intentarlo.");
        return sb.ToString();
    }
}
