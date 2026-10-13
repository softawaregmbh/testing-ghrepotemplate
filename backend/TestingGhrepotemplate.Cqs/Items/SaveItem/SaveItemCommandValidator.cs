using FluentValidation;

namespace TestingGhrepotemplate.Cqs.Items.SaveItem;

public class SaveItemCommandValidator : AbstractValidator<SaveItemCommand>
{
    public SaveItemCommandValidator(IValidator<ItemDto> itemDtoValidator)
    {
        this.RuleFor(command => command.Item).NotNull();
        this.RuleFor(command => command.Item).SetValidator(itemDtoValidator);
    }
}
