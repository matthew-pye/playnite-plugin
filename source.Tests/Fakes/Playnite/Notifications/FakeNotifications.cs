using NSubstitute;
using Playnite;

namespace Graviton.Tests.Fakes.Playnite.Notifications;

public sealed class FakeNotifications
{
    private readonly Dictionary<string, NotificationMessage> _messages = new(StringComparer.Ordinal);

    public INotificationsAPI Api { get; } = Substitute.For<INotificationsAPI>();
    public IReadOnlyCollection<NotificationMessage> Messages => _messages.Values.ToList();

    public FakeNotifications()
    {
        Api.When(api =>
        {
            api.Add(message: Arg.Any<NotificationMessage>());
        }).Do(call =>
        {
            var message = call.ArgAt<NotificationMessage>(0);
            _messages[message.Id] = message;
        });

        Api.When(api =>
        {
            api.Remove(id: Arg.Any<string>());
        }).Do(call =>
        {
            var id = call.ArgAt<string>(0);
            _messages.Remove(id);
        });
    }
}
