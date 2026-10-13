namespace TestingGhrepotemplate.Cqs.Items.GetItemById;

public record GetItemByIdQuery(Guid Id) : IQuery<ItemDto>;
