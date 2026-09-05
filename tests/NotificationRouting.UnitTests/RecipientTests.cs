using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;
using NotificationRouting.Domain.Recipients;

namespace NotificationRouting.UnitTests;

public sealed class RecipientTests
{
    [Theory]
    [InlineData(Importance.Normal, Importance.High, false)]
    [InlineData(Importance.High, Importance.High, true)]
    [InlineData(Importance.Critical, Importance.High, true)]
    public async Task ImportanceFilterUsesInclusiveBoundary(
        Importance messageImportance,
        Importance minimumImportance,
        bool expectedDelivery)
    {
        var recipient = new SpyRecipient();
        var filter = new ImportanceFilterRecipient(recipient, minimumImportance);

        await filter.DeliverAsync(
            CreateContext(new Message("Status", "Body", messageImportance)),
            CancellationToken.None);

        Assert.Equal(expectedDelivery ? 1 : 0, recipient.Deliveries);
    }

    [Fact]
    public async Task KeywordAlertWithNoKeywordsDoesNotNotify()
    {
        var sink = new SpyAlertSink();
        var recipient = new KeywordAlertRecipient(Guid.NewGuid(), sink, []);

        await recipient.DeliverAsync(
            CreateContext(new Message("Security incident", "Body", Importance.Critical)),
            CancellationToken.None);

        Assert.Equal(0, sink.Notifications);
    }

    [Fact]
    public async Task GroupRecipientDeliversToEveryChild()
    {
        var first = new SpyRecipient();
        var second = new SpyRecipient();
        var group = new GroupRecipient([first, second]);

        await group.DeliverAsync(
            CreateContext(new Message("Status", "Body", Importance.Normal)),
            CancellationToken.None);

        Assert.Equal(1, first.Deliveries);
        Assert.Equal(1, second.Deliveries);
    }

    [Fact]
    public async Task FilteredMessageIsNotLoggedAsDelivered()
    {
        var deliveryLog = new SpyDeliveryLog();
        var recipient = new SpyRecipient();
        var logged = new LoggingRecipient(recipient, deliveryLog, "Alice");
        var filtered = new ImportanceFilterRecipient(logged, Importance.High);

        await filtered.DeliverAsync(
            CreateContext(new Message("Routine", "Body", Importance.Normal)),
            CancellationToken.None);

        Assert.Equal(0, deliveryLog.Deliveries);
        Assert.Equal(0, recipient.Deliveries);
    }

    [Fact]
    public async Task FailedDeliveryIsNotLoggedAsSuccessful()
    {
        var deliveryLog = new SpyDeliveryLog();
        var logged = new LoggingRecipient(new FailingRecipient(), deliveryLog, "webhook");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await logged.DeliverAsync(
                CreateContext(new Message("Status", "Body", Importance.High)),
                CancellationToken.None));

        Assert.Equal(0, deliveryLog.Deliveries);
    }

    private static DeliveryContext CreateContext(Message message)
    {
        return new DeliveryContext(Guid.NewGuid(), Guid.NewGuid(), message);
    }

    private sealed class SpyRecipient : IRecipient
    {
        public int Deliveries { get; private set; }

        public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Deliveries++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingRecipient : IRecipient
    {
        public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated failure.");
        }
    }

    private sealed class SpyAlertSink : IAlertSink
    {
        public int Notifications { get; private set; }

        public void Notify(Guid userId, Message message, string keyword)
        {
            Notifications++;
        }
    }

    private sealed class SpyDeliveryLog : IDeliveryLog
    {
        public int Deliveries { get; private set; }

        public void Delivered(Message message, string recipientName)
        {
            Deliveries++;
        }
    }
}
