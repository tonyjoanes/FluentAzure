using System;

namespace FluentAzure.Tests.Binding
{
    public record TestRecord(
        string Name,
        string Version,
        string Environment,
        int MaxConnections,
        bool EnableFeature
    );
}
