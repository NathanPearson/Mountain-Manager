namespace MountainManager.Api.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiTestCollection
{
    public const string Name = "API integration tests";
}
