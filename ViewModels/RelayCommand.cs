using System.Windows.Input;

namespace CameraScriptManager.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task>? _execute;
    private readonly Func<object?, Task>? _executeWithParam;
    private readonly Func<bool>? _canExecute;
    private readonly Func<object?, bool>? _canExecuteWithParam;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _executeWithParam = execute;
        _canExecuteWithParam = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) 
    {
        if (_isExecuting) return false;
        
        if (_canExecuteWithParam != null)
            return _canExecuteWithParam(parameter);
            
        return _canExecute?.Invoke() ?? true;
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            if (_executeWithParam != null)
                await _executeWithParam(parameter);
            else if (_execute != null)
                await _execute();
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
