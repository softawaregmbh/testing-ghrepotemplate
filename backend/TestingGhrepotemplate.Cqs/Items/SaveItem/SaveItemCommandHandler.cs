using Microsoft.EntityFrameworkCore;
using TestingGhrepotemplate.EntityFramework;

namespace TestingGhrepotemplate.Cqs.Items.SaveItem;

public class SaveItemCommandHandler(TestingGhrepotemplateContext context) : IRequestHandler<SaveItemCommand, NoResult>
{
    public async Task<NoResult> HandleAsync(SaveItemCommand request, CancellationToken cancellationToken)
    {
        // TODO: add code to save item to database
        await context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

        return NoResult.Value;
    }
}
