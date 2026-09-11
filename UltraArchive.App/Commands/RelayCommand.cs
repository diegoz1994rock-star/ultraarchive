using System.Windows.Input;

namespace UltraArchive.App.Commands;

/// <summary>Implementación estándar de <see cref="ICommand"/> para comandos síncronos sin argumento.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    /// <summary>Fuerza una reevaluación de <see cref="CanExecute"/> en la UI (p. ej. tras cambiar la selección).</summary>
    public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
