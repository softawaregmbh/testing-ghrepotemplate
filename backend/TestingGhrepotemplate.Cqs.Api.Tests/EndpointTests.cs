using TestingGhrepotemplate.Cqs.Items;

namespace TestingGhrepotemplate.Cqs.Api.Tests;

public class EndpointTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    [Fact]
    public async Task GetItemById_ReturnsSuccess()
    {
        var response = await fixture.Client.PostAsync("Items/GetItemByIdQuery", JsonContent.Create(new { Id = Guid.NewGuid() }), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SaveItem_ReturnsSuccess_WhenNameIsValid()
    {
        var response = await fixture.Client.PostAsync("Items/SaveItemCommand", JsonContent.Create(new { Item = new ItemDto(Guid.NewGuid(), "Test", DateTimeOffset.UtcNow) }), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("T")]
    public async Task SaveItem_ReturnsBadRequest_WhenNameIsInvalid(string? name)
    {
        var response = await fixture.Client.PostAsync("Items/SaveItemCommand", JsonContent.Create(new { Item = new ItemDto(Guid.NewGuid(), name!, DateTimeOffset.UtcNow) }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
