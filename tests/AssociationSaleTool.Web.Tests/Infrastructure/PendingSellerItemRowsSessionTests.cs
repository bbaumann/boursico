using System.Text;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http;

namespace AssociationSaleTool.Web.Tests.Infrastructure;

public sealed class PendingSellerItemRowsSessionTests
{
    [Fact]
    public void Consume_ReturnsStoredRowsForMatchingSellerAndClearsSession()
    {
        var session = new TestSession();
        PendingSellerItemRowsSession.Store(
            session,
            new PendingSellerItemRows(
                42,
                IntakeMode.EmailPreregistration,
                [new CreateItemRowRequest("Puzzle", 500)],
                PendingSellerItemRowsSession.ExcelDuplicateAlertKey));

        var pendingRows = PendingSellerItemRowsSession.Consume(session, 42);
        var secondRead = PendingSellerItemRowsSession.Consume(session, 42);

        Assert.NotNull(pendingRows);
        Assert.Equal(42, pendingRows.SellerId);
        Assert.Equal(IntakeMode.EmailPreregistration, pendingRows.IntakeMode);
        Assert.Equal(PendingSellerItemRowsSession.ExcelDuplicateAlertKey, pendingRows.AlertMessageKey);
        var row = Assert.Single(pendingRows.Rows);
        Assert.Equal("Puzzle", row.Title);
        Assert.Equal(500, row.SellerPriceCents);
        Assert.Null(secondRead);
    }

    [Fact]
    public void Consume_KeepsRowsForDifferentSeller()
    {
        var session = new TestSession();
        PendingSellerItemRowsSession.Store(
            session,
            new PendingSellerItemRows(
                42,
                IntakeMode.InPerson,
                [new CreateItemRowRequest("Puzzle", 500)],
                PendingSellerItemRowsSession.ExcelDuplicateAlertKey));

        var wrongSellerRows = PendingSellerItemRowsSession.Consume(session, 99);
        var matchingSellerRows = PendingSellerItemRowsSession.Consume(session, 42);

        Assert.Null(wrongSellerRows);
        Assert.NotNull(matchingSellerRows);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public IEnumerable<string> Keys => _values.Keys;

        public string Id { get; } = Guid.NewGuid().ToString("N");

        public bool IsAvailable => true;

        public void Clear()
        {
            _values.Clear();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _values.Remove(key);
        }

        public void Set(string key, byte[] value)
        {
            _values[key] = value;
        }

        public bool TryGetValue(string key, out byte[] value)
        {
            return _values.TryGetValue(key, out value!);
        }

        public string? GetString(string key)
        {
            return _values.TryGetValue(key, out var value) ? Encoding.UTF8.GetString(value) : null;
        }
    }
}
