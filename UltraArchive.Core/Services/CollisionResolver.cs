using UltraArchive.Core.Models;

namespace UltraArchive.Core.Services;

/// <summary>
/// Decide, para cada fichero que ya existe en el destino, qué ruta usar al extraer (o si saltárselo).
/// Centraliza la política de colisiones que antes estaba duplicada en cada motor.
///
/// Reglas:
///   - <see cref="CollisionPolicy.Overwrite"/> / <see cref="CollisionPolicy.Skip"/> /
///     <see cref="CollisionPolicy.RenameAutomatically"/>: acción fija, sin preguntar.
///   - <see cref="CollisionPolicy.Ask"/>: se invoca el manejador <c>onCollision</c> (típicamente un
///     diálogo). Si no hay manejador, se sobrescribe (comportamiento histórico de UltraArchive).
///   - Si una respuesta trae <see cref="CollisionResolution.ApplyToAll"/>, esa acción se recuerda y
///     se aplica al resto de conflictos de la operación sin volver a preguntar.
///   - <see cref="CollisionAction.Cancel"/> lanza <see cref="OperationCanceledException"/>: el motor
///     la deja propagar igual que una cancelación por <see cref="CancellationToken"/>.
///
/// No es thread-safe: cada operación de extracción usa su propia instancia, de forma secuencial.
/// </summary>
public sealed class CollisionResolver
{
    private readonly CollisionPolicy _policy;
    private readonly Func<string, CollisionResolution>? _onCollision;
    private CollisionAction? _stickyAction;

    public CollisionResolver(ExtractOptions options)
        : this(options.CollisionPolicy, options.OnCollision)
    {
    }

    public CollisionResolver(CollisionPolicy policy, Func<string, CollisionResolution>? onCollision)
    {
        _policy = policy;
        _onCollision = onCollision;
    }

    /// <summary>
    /// Dada la ruta de destino prevista, devuelve la ruta a la que escribir realmente, o
    /// <c>null</c> si esta entrada debe saltarse. Lanza <see cref="OperationCanceledException"/> si
    /// la resolución es <see cref="CollisionAction.Cancel"/>.
    /// </summary>
    public string? Resolve(string destinationPath)
    {
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var action = _stickyAction ?? Decide(destinationPath);

        return action switch
        {
            CollisionAction.Overwrite => destinationPath,
            CollisionAction.Rename => UniquePath.NextAvailable(destinationPath),
            CollisionAction.Skip => null,
            CollisionAction.Cancel => throw new OperationCanceledException(
                "Extracción cancelada por el usuario ante un conflicto de ficheros."),
            _ => destinationPath,
        };
    }

    private CollisionAction Decide(string destinationPath)
    {
        switch (_policy)
        {
            case CollisionPolicy.Overwrite:
                return CollisionAction.Overwrite;
            case CollisionPolicy.Skip:
                return CollisionAction.Skip;
            case CollisionPolicy.RenameAutomatically:
                return CollisionAction.Rename;
            case CollisionPolicy.Ask:
            default:
                if (_onCollision is null)
                {
                    return CollisionAction.Overwrite;
                }

                var resolution = _onCollision(destinationPath) ?? CollisionResolution.Overwrite;
                if (resolution.ApplyToAll && resolution.Action != CollisionAction.Cancel)
                {
                    _stickyAction = resolution.Action;
                }

                return resolution.Action;
        }
    }
}
