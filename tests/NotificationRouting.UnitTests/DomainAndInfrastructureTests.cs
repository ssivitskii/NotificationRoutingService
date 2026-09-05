using NotificationRouting.Domain;
using NotificationRouting.Infrastructure;

namespace NotificationRouting.UnitTests;

public sealed class DomainAndInfrastructureTests
{
    [Fact]
    public void MarkReadWithUnknownMessageReturnsResultError()
    {
        var user = new User("Alice");

        OperationResult result = user.MarkRead(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorKind.NotFound, result.ErrorKind);
        Assert.Contains("not found", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepeatedMarkReadReturnsConflict()
    {
        var user = new User("Alice");
        var message = new Message("Status", "Body", Importance.Normal);
        user.Receive(message);

        Assert.True(user.MarkRead(message.Id).IsSuccess);
        OperationResult repeated = user.MarkRead(message.Id);

        Assert.False(repeated.IsSuccess);
        Assert.Equal(OperationErrorKind.Conflict, repeated.ErrorKind);
        Assert.Contains("already", repeated.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchivePreservesEveryPublishedMessageInOrder()
    {
        var archive = new InMemoryMessageArchive();
        var first = new Message("First", "Body", Importance.Low);
        var second = new Message("Second", "Body", Importance.High);

        archive.Save(first);
        archive.Save(second);

        Assert.Equal(new[] { first.Id, second.Id }, archive.GetAll().Select(message => message.Id));
    }

    [Fact]
    public void MarkdownFormatterProducesStableOutput()
    {
        var message = new Message("Maintenance", "At 22:00 UTC", Importance.High);

        string output = new MarkdownMessageFormatter().Format(message);

        Assert.Equal(
            $"# Maintenance{Environment.NewLine}> Importance: **High**{Environment.NewLine}{Environment.NewLine}At 22:00 UTC",
            output);
    }
}
