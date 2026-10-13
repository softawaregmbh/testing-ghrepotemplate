using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TestingGhrepotemplate.Cqs.Api.Generator;

internal class RequestSymbolVisitor : SymbolVisitor
{
    private readonly List<Request> requests = [];

    public IReadOnlyCollection<Request> Requests => this.requests;

    public override void VisitNamespace(INamespaceSymbol @namespace)
    {
        foreach (var member in @namespace.GetMembers())
        {
            member.Accept(this);
        }
    }

    public override void VisitNamedType(INamedTypeSymbol type)
    {
        var request = GetRequest(type);
        if (request != null)
        {
            this.requests.Add(request);
        }
    }

    private static Request? GetRequest(INamedTypeSymbol type)
    {
        var requestInterfaceType = GetRequestInterface(type);
        if (requestInterfaceType == null)
        {
            return null;
        }

        if (IsIgnored(type))
        {
            return null;
        }

        return new Request(
            type.Name,
            type.ToDisplayString(),
            HasProperties(type),
            HasNoResultReturn(requestInterfaceType));

        static INamedTypeSymbol? GetRequestInterface(INamedTypeSymbol type)
        {
            return type.AllInterfaces.FirstOrDefault(i => i.Name == "IRequest" && i.IsGenericType);
        }

        static bool IsIgnored(ITypeSymbol type)
        {
            return type.GetAttributes().Any(a => a.AttributeClass?.Name == "IgnoredByGeneratorAttribute");
        }

        static bool HasProperties(ITypeSymbol type)
        {
            do
            {
                if (type.GetMembers().Any(m => m.Kind == SymbolKind.Property && !m.IsStatic && m.Name != "EqualityContract"))
                {
                    return true;
                }

                type = type.BaseType!;
            }
            while (type != null);

            return false;
        }

        static bool HasNoResultReturn(INamedTypeSymbol type)
        {
            return type.TypeArguments.First().ToDisplayString() == "softaware.Cqs.NoResult";
        }
    }
}
