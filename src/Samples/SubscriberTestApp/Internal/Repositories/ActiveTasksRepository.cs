using System.Collections.Concurrent;
using SubscriberTestApp.Internal.Subscribers;

namespace SubscriberTestApp.Internal.Repositories;

public class ActiveTasksRepository : IDisposable
{
    private static readonly List<string> _activeWorkers = new();
    private readonly string _workerName;
    private static object _lock = new object();
    private ActiveTasksRepository(IDelayedWorkMessageHandler worker)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(worker.WorkerName))
                throw new ArgumentNullException(nameof(worker.WorkerName));

            _workerName = worker.WorkerName;
            _activeWorkers.Add(worker.WorkerName);
        }
    }

    public static ActiveTasksRepository EnqueueActiveWorker(IDelayedWorkMessageHandler worker)
        => new(worker);

    public static IEnumerable<string> GetActiveWorkers()
        => _activeWorkers;

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
            {
                _activeWorkers.Remove(_workerName);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}