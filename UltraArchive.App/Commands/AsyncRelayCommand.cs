using System.Windows.Input;

namespace UltraArchive.App.Commands;

/// <summary>
/// <see cref="ICommand"/> para operaciones asíncronas (abrir, extraer, comprimir...). Evita reentrancia
/// mientras la operación anterior sigue en curso y nunca deja una excepción sin observar: cualquier
/// error se entrega a <paramref name="onError"/> para que la capa de presentación lo traduzca a un
/// mensaje claro (ver <see cref="Core.Exceptions.ArchiveException"/>).
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _executeAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (_onError is not null)
        {
            _onError(ex);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
