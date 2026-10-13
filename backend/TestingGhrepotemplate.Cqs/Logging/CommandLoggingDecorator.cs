using System.Diagnostics;

namespace TestingGhrepotemplate.Cqs.Logging;

/// <summary>
/// Sample decorator implementation.
/// In this case all commands (as opposed to queries) are logged to the debug console
/// because of the generic constraint <code>where TRequest : ICommand&lt;TResult&gt;</code>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public class CommandLoggingDecorator<TRequest, TResult>(IRequestHandler<TRequest, TResult> decoratee) :
    IRequestHandler<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        object type = typeof(TRequest).Name;
        Debug.WriteLine("Executing command \"{0}\"", type);

        var result = await decoratee.HandleAsync(request, cancellationToken);

        Debug.WriteLine("Executed command \"{0}\"", type);
        return result;
    }
}
