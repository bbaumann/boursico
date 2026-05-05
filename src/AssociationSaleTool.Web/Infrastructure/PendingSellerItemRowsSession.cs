using System.Text.Json;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http;

namespace AssociationSaleTool.Web.Infrastructure;

public static class PendingSellerItemRowsSession
{
    public const string ExcelDuplicateAlertKey = "ExcelDuplicate";

    private const string SessionKey = "PendingSellerItemRows";

    public static void Store(ISession session, PendingSellerItemRows pendingRows)
    {
        var payload = new PendingSellerItemRowsPayload(
            pendingRows.SellerId,
            pendingRows.IntakeMode,
            pendingRows.Rows.ToList(),
            pendingRows.AlertMessageKey);
        session.SetString(SessionKey, JsonSerializer.Serialize(payload));
    }

    public static PendingSellerItemRows? Consume(ISession session, long sellerId)
    {
        var rawValue = session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<PendingSellerItemRowsPayload>(rawValue);
        if (payload is null || payload.SellerId != sellerId)
        {
            return null;
        }

        session.Remove(SessionKey);
        return new PendingSellerItemRows(
            payload.SellerId,
            payload.IntakeMode,
            payload.Rows,
            payload.AlertMessageKey);
    }

    private sealed record PendingSellerItemRowsPayload(
        long SellerId,
        IntakeMode IntakeMode,
        List<CreateItemRowRequest> Rows,
        string AlertMessageKey);
}

public sealed record PendingSellerItemRows(
    long SellerId,
    IntakeMode IntakeMode,
    IReadOnlyList<CreateItemRowRequest> Rows,
    string AlertMessageKey);
