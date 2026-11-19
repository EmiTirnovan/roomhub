namespace MyApp.Application.Common.Cqrs;

using MyApp.Domain.Common;

/// <summary>
/// Unit type representing the absence of a value result for commands.
/// Replaces System.Void in generic type arguments.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}

/// <summary>
/// Marker contract for a Command that produces a value of type TResult.
/// </summary>
public interface ICommand<TResult>;

/// <summary>
/// Marker contract for a Command with no return value (returns Unit).
/// </summary>
public interface ICommand : ICommand<Unit>;

/// <summary>
/// Contract for custom Command Handlers.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The expected result payload.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Shortcut contract for Command Handlers returning Unit.
/// </summary>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand;
