using System.Diagnostics.CodeAnalysis;

namespace TestingGhrepotemplate.Cqs.Exceptions;

public class NotFoundException(string message) : Exception(message)
{
    [DoesNotReturn]
    public static void Throw<TEntityType>(object identifier) =>
        throw new NotFoundException($"{typeof(TEntityType).Name} with id {identifier} not found.");
}
