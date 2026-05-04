using System.Globalization;
using System.Text;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SellerDuplicateService(AppDbContext dbContext)
{
    public async Task<IReadOnlyList<SellerDuplicateMatch>> FindDuplicateSellersAsync(
        SellerDuplicateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var submittedFirstName = NormalizeName(request.FirstName);
        var submittedLastName = NormalizeName(request.LastName);
        var submittedPhone = NormalizePhone(request.Phone);
        var hasSubmittedPhone = !string.IsNullOrWhiteSpace(submittedPhone);

        var sellers = await dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => request.ExcludedSellerId == null || x.Id != request.ExcludedSellerId.Value)
            .ToListAsync(cancellationToken);

        return sellers
            .Where(seller =>
                NormalizeName(seller.FirstName) == submittedFirstName
                && NormalizeName(seller.LastName) == submittedLastName
                && (!hasSubmittedPhone || NormalizePhone(seller.Phone) == submittedPhone))
            .OrderBy(seller => seller.SellerCode)
            .Select(seller => new SellerDuplicateMatch(
                seller.Id,
                seller.SellerCode,
                seller.FirstName,
                seller.LastName,
                seller.Phone,
                seller.Email,
                seller.Items.Count,
                hasSubmittedPhone
                    ? SellerDuplicateMatchReason.NameAndPhone
                    : SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing))
            .ToList();
    }

    private static string NormalizePhone(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeName(string value)
    {
        var collapsedWhitespace = string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var upper = collapsedWhitespace.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(upper.Length);

        foreach (var character in upper)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
