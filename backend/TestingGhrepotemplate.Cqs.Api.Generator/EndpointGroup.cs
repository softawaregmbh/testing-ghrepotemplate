using System;
using System.Collections.Generic;

namespace TestingGhrepotemplate.Cqs.Api.Generator;

internal class EndpointGroup(
    string groupName,
    IReadOnlyCollection<Request> requests)
{
    public string GroupName { get; } = groupName ?? throw new ArgumentNullException(nameof(groupName));
    public IReadOnlyCollection<Request> Requests { get; } = requests ?? throw new ArgumentNullException(nameof(requests));
}
