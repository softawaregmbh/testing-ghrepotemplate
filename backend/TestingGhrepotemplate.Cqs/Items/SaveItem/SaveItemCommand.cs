namespace TestingGhrepotemplate.Cqs.Items.SaveItem;

public record SaveItemCommand(ItemDto Item) : ICommand;
