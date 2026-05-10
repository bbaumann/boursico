using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Services;

public sealed class InvoiceNumberService
{
    private readonly AppDbContext _dbContext;
    private readonly PrintingOptions _printingOptions;

    public InvoiceNumberService(AppDbContext dbContext, IOptions<PrintingOptions>? printingOptions = null)
    {
        _dbContext = dbContext;
        _printingOptions = printingOptions?.Value ?? new PrintingOptions();
    }

    public async Task<string> CreateNextInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = _printingOptions.InvoiceNumberPrefix;
        var lastInvoiceNumber = await _dbContext.Sales
            .AsNoTracking()
            .Where(x => x.InvoiceNumber != null && x.InvoiceNumber.StartsWith(prefix))
            .Select(x => x.InvoiceNumber!)
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (lastInvoiceNumber is not null
            && lastInvoiceNumber.Length > prefix.Length
            && int.TryParse(lastInvoiceNumber[prefix.Length..], out var lastSequence))
        {
            nextSequence = lastSequence + 1;
        }

        return $"{prefix}{nextSequence:0000}";
    }
}
