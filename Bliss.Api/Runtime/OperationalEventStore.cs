using System.Collections.Concurrent;

namespace Bliss.Api.Runtime;

public sealed record OperationalEvent(
    DateTime OccurredAt,
    string Kind,
    string Method,
    string Path,
    int StatusCode,
    string RequestId);

public sealed class OperationalEventStore
{
    private readonly ConcurrentQueue<OperationalEvent> _events = new();
    private volatile string? _lastRequestId;
    private const int Capacity = 40;

    public string? LastRequestId => _lastRequestId;

    public void RememberRequest(string requestId) => _lastRequestId = requestId;

    public void Record(OperationalEvent entry)
    {
        _lastRequestId = entry.RequestId;
        _events.Enqueue(entry);
        while (_events.Count > Capacity && _events.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<OperationalEvent> Recent() =>
        _events.Reverse().Take(20).ToArray();
}
