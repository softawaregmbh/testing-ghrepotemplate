using System;

namespace TestingGhrepotemplate.Cqs.Api.Generator;

internal class Request(string name, string type, bool hasProperties, bool returnsNoResult)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public string Type { get; } = type ?? throw new ArgumentNullException(nameof(type));
    public bool HasProperties { get; } = hasProperties;
    public bool ReturnsNoResult { get; } = returnsNoResult;
}
