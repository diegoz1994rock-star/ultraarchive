using System.Diagnostics;
using System.Text;

namespace UltraArchive.Interop.SevenZip;

/// <summary>
/// Implementación real de <see cref="ISevenZipBannerReader"/>: lanza el ejecutable sin argumentos de
/// operación (7-Zip imprime su banner y su ayuda y termina) y captura las primeras líneas de salida.
/// Proceso efímero, sin ventana, con timeout corto; si sigue vivo se mata. Nunca lanza.
/// </summary>
internal sealed class ProcessBannerReader : ISevenZipBannerReader
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const int MaxCharsToCapture = 4096;

    public string? ReadBanner(string executablePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            try
            {
                process.StandardInput.Close();

                var output = new StringBuilder();
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
                {
                    KillTree(process);
                    return null;
                }

                Task.WaitAll(new Task[] { stdout, stderr }, Timeout);
                Append(output, stdout);
                Append(output, stderr);

                var text = output.ToString();
                return text.Length > MaxCharsToCapture ? text[..MaxCharsToCapture] : text;
            }
            finally
            {
                KillTree(process);
            }
        }
        catch
        {
            // "no responde como un 7-Zip válido" — el llamador lo traduce a InvalidExecutable.
            return null;
        }
    }

    private static void Append(StringBuilder builder, Task<string> completed)
    {
        if (completed is { IsCompletedSuccessfully: true, Result.Length: > 0 })
        {
            builder.Append(completed.Result);
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
