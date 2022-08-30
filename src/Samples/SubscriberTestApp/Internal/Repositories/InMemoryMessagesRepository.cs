using System.Collections.Concurrent;
using TestAppCommon;

namespace SubscriberTestApp.Internal.Repositories;

public interface IMessagesRepository
{
    Task<IEnumerable<MqMessageBase>> GetMessages();
    Task AddMessage(MqMessageBase message);
    Task<MqMessageBase?> GetMessage(string messageId);
    Task<int> GetCountOfMessages();
    Task<IEnumerable<MqMessageBase>> GetMessagesLike(string idPrefix);
    Task Clear();
}


public class InMemoryMessagesRepository : IMessagesRepository
{

    private readonly ConcurrentDictionary<string, MqMessageBase> _messages = new();

    public Task<IEnumerable<MqMessageBase>> GetMessages()
        => Task.FromResult((IEnumerable<MqMessageBase>)_messages.Values);

    public Task AddMessage(MqMessageBase message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        _ = _messages.GetOrAdd(message.Attributes.MessageId, message);
        return Task.CompletedTask;
    }

    public Task<MqMessageBase?> GetMessage(string messageId)
    {
        if (_messages.TryGetValue(messageId, out var msg))
            return Task.FromResult(msg);

        return Task.FromResult((MqMessageBase) null);
    }

    public Task<IEnumerable<MqMessageBase>> GetMessagesLike(string idPrefix)
    {
        var messages = _messages.Where(p => p.Key.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).ToArray();
        return Task.FromResult((IEnumerable<MqMessageBase>) messages);
    }

    public Task<int> GetCountOfMessages()
        => Task.FromResult(_messages.Count);

    public Task Clear()
    {
        _messages.Clear();
        return Task.CompletedTask;
    }
}