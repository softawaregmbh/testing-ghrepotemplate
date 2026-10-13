using System.Diagnostics;

namespace TestingGhrepotemplate.Cqs.Logging;

/// <summary>
/// Sample decorator implementation.
/// In this case all requests (queries and commands) are logged to the debug console.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public class RequestLoggingDecorator<TRequest, TResult>(IRequestHandler<TRequest, TResult> decoratee) : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public async Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        object type = typeof(TRequest).Name;
        Debug.WriteLine("Executing request \"{0}\"", type);

        var result = await decoratee.HandleAsync(request, cancellationToken);

        Debug.WriteLine("Executed request \"{0}\"", type);
        return result;
    }
}
