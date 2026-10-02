using System;
using System.Threading.Tasks;
using AutonomiaSaaS.Modules.BusinessCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;

namespace AutonomiaSaaS.Modules.BusinessCore.Tests;

internal sealed class FakeContentDefinitionManager : IContentDefinitionManager
{
    public Task AlterPartDefinitionAsync(string partName, Action<ContentPartDefinitionBuilder> alteration)
    {
        // No-op for unit test
        return Task.CompletedTask;
    }

    public Task AlterTypeDefinitionAsync(string typeName, Action<ContentTypeDefinitionBuilder> alteration)
    {
        return Task.CompletedTask;
    }

    // The interface has more members; implement minimal explicit members used by Migrations
    public ContentTypeDefinition GetTypeDefinition(string name) => throw new NotImplementedException();
    public ContentPartDefinition GetPartDefinition(string name) => throw new NotImplementedException();
    public Task<IEnumerable<ContentTypeDefinition>> ListTypeDefinitionsAsync() => throw new NotImplementedException();
    public Task<IEnumerable<ContentPartDefinition>> ListPartDefinitionsAsync() => throw new NotImplementedException();

    public Task<IEnumerable<ContentTypeDefinition>> LoadTypeDefinitionsAsync()
    {
        return Task.FromResult<IEnumerable<ContentTypeDefinition>>(Array.Empty<ContentTypeDefinition>());
    }

    public Task<IEnumerable<ContentPartDefinition>> LoadPartDefinitionsAsync()
    {
        return Task.FromResult<IEnumerable<ContentPartDefinition>>(Array.Empty<ContentPartDefinition>());
    }

    public Task<ContentTypeDefinition> LoadTypeDefinitionAsync(string name)
    {
        return Task.FromResult<ContentTypeDefinition>(null);
    }

    public Task<ContentTypeDefinition> GetTypeDefinitionAsync(string name)
    {
        return Task.FromResult<ContentTypeDefinition>(null);
    }

    public Task<ContentPartDefinition> LoadPartDefinitionAsync(string name)
    {
        return Task.FromResult<ContentPartDefinition>(null);
    }

    public Task<ContentPartDefinition> GetPartDefinitionAsync(string name)
    {
        return Task.FromResult<ContentPartDefinition>(null);
    }

    public Task DeleteTypeDefinitionAsync(string name)
    {
        return Task.CompletedTask;
    }

    public Task DeletePartDefinitionAsync(string name)
    {
        return Task.CompletedTask;
    }

    public Task StoreTypeDefinitionAsync(ContentTypeDefinition contentTypeDefinition)
    {
        return Task.CompletedTask;
    }

    public Task StorePartDefinitionAsync(ContentPartDefinition contentPartDefinition)
    {
        return Task.CompletedTask;
    }

    public Task<string> GetIdentifierAsync()
    {
        return Task.FromResult(string.Empty);
    }
}

public class FeatureActivationTests
{
    [Fact]
    public async Task Enabling_BusinessCore_Without_OrchardCore_Data_Should_Fail_Migrations()
    {
        // Arrange: construct the Migrations class with a fake IContentDefinitionManager but
        // without the OrchardCore schema/YesSql infrastructure that SchemaBuilder requires.
        var fakeManager = new FakeContentDefinitionManager();
        var migrations = new Migrations(fakeManager);

        // Act: calling CreateAsync should fail because SchemaBuilder (inherited) is not wired
        // by the OrchardCore host in this unit test. In production this would make the
        // feature activation hang while migrations are attempted against an unavailable DB.

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => migrations.CreateAsync());

        // Assert: provide a clear diagnostic message so CI/DEV sees the root cause quickly.
        Assert.True(
            ex is NullReferenceException || ex is InvalidOperationException || ex is NotSupportedException,
            "Expected migration to fail due to missing OrchardCore Data infrastructure (SchemaBuilder/YesSql); actual: " + ex.GetType().FullName + ", message: " + ex.Message);
    }
}
