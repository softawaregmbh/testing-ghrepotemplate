using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Scriban;

namespace TestingGhrepotemplate.Cqs.Api.Generator;

[Generator]
public partial class EndpointGenerator : IIncrementalGenerator
{
    private static readonly Template CqsEndpointsTemplate = GetTemplate("CqsEndpoints");
    private static readonly Template EndpointGroupTemplate = GetTemplate("EndpointGroup");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, this.GenerateEndpoints);
    }

    public void GenerateEndpoints(SourceProductionContext context, Compilation compilation)
    {
        var cqsNamespace = GetCqsNamespace(compilation);

        var entityNamespacesWithRequests =
            from entityNamespace in cqsNamespace.GetNamespaceMembers()
            where GenerateEntityEndpoints(context, entityNamespace)
            select entityNamespace.Name;

        GenerateCqsEndpoints(context, entityNamespacesWithRequests);
    }

    private static INamespaceSymbol GetCqsNamespace(Compilation compilation)
    {
        // https://stackoverflow.com/questions/68055210/generate-source-based-on-other-assembly-classes-c-source-generator
        var cqsAssemblyName = "TestingGhrepotemplate.Cqs";

        var cqsAssembly = compilation.SourceModule.ReferencedAssemblySymbols.Single(s => s.Name == cqsAssemblyName);
        var cqsNamespaceParts = cqsAssemblyName.Split('.');
        var cqsNamespace = cqsAssembly.GlobalNamespace;

        foreach (var cqsNamespacePart in cqsNamespaceParts)
        {
            cqsNamespace = cqsNamespace.GetNamespaceMembers().Single(ns => ns.Name == cqsNamespacePart);
        }

        return cqsNamespace;
    }

    private static void GenerateCqsEndpoints(SourceProductionContext context, IEnumerable<string> entityNamespacesWithRequests)
    {
        var cqsEndpointsSource = CqsEndpointsTemplate.Render(
            new { Groups = entityNamespacesWithRequests },
            memberRenamer: member => member.Name);

        context.AddSource("CqsEndpoints.g.cs", SourceText.From(cqsEndpointsSource, Encoding.Default));
    }

    private static bool GenerateEntityEndpoints(SourceProductionContext context, INamespaceSymbol entityNamespace)
    {
        var visitor = new RequestSymbolVisitor();
        visitor.Visit(entityNamespace);

        if (visitor.Requests.Any())
        {
            var group = new EndpointGroup(entityNamespace.Name, visitor.Requests);
            var source = EndpointGroupTemplate.Render(group, memberRenamer: member => member.Name);
            context.AddSource(entityNamespace.Name + "Endpoints.g.cs", SourceText.From(source, Encoding.Default));

            return true;
        }

        return false;
    }

    private static Template GetTemplate(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var reader = new StreamReader(
            assembly.GetManifestResourceStream(
                $"TestingGhrepotemplate.Cqs.Api.Generator.Templates.{name}.scriban"));

        var template = Template.Parse(reader.ReadToEnd());
        if (template.HasErrors)
        {
            var errors = string.Join(" | ", template.Messages.Select(x => x.Message));
            throw new InvalidOperationException($"Template parse error: {template.Messages}");
        }

        return template;
    }
}
