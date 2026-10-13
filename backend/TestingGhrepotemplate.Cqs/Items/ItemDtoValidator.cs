using FluentValidation;

namespace TestingGhrepotemplate.Cqs.Items;

public class ItemDtoValidator : AbstractValidator<ItemDto>
{
    public ItemDtoValidator()
    {
        this.RuleFor(dto => dto.Name).NotNull();
        this.RuleFor(dto => dto.Name).MinimumLength(3);
    }
}
