using RabbitMqClient;
using SubscriberTestApp.Internal.Repositories;
using TestAppCommon;

namespace SubscriberTestApp.Internal.Services;


public interface IMessageService
{
    Task<IEnumerable<MqMessageBase>> GetAllMessages();
    Task<MqMessageBase?> GetMessage(string id);
    Task<IEnumerable<MqMessageBase>> FilterMessages(string idPrefix);
    Task<int> GetCountOfMessages();
    Task ClearMessages();
    Task<IEnumerable<string>> GetActiveWorkers();
}
public class MessagesService : IMessageService
{
    private readonly IMessagesRepository _messagesRepository;

    public MessagesService(IMessagesRepository messagesRepository)
    {
        _messagesRepository = messagesRepository;
    }

    public Task<IEnumerable<MqMessageBase>> GetAllMessages()
        => _messagesRepository.GetMessages();

    public Task<MqMessageBase?> GetMessage(string id)
        => _messagesRepository.GetMessage(id);

    public Task<IEnumerable<MqMessageBase>> FilterMessages(string idPrefix)
        => _messagesRepository.GetMessagesLike(idPrefix);

    public Task<int> GetCountOfMessages()
        => _messagesRepository.GetCountOfMessages();

    public Task ClearMessages()
        => _messagesRepository.Clear();

    public Task<IEnumerable<string>> GetActiveWorkers()
        => Task.FromResult(ActiveTasksRepository.GetActiveWorkers());
}