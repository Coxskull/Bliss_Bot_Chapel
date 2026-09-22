using System.Collections.Concurrent;

namespace Bliss.Api.Runtime;

public sealed record OperationalEvent(
    DateTime OccurredAt,
    string Kind,
    string Method,
    string Path,
    int StatusCode,
    string RequestId,
    string? Detail = null);

public sealed class OperationalEventStore
{
    private readonly ConcurrentQueue<OperationalEvent> _events = new();
    private readonly ConcurrentQueue<ExportVerificationDto> _verifications = new();
    private volatile string? _lastRequestId;
    private volatile ExportVerificationDto? _lastVerification;
    private const int Capacity = 40;
    public const int VerificationHistoryLimit = 10;

    public string? LastRequestId => _lastRequestId;
    public ExportVerificationDto? LastVerification => _lastVerification;

    public void RememberRequest(string requestId) => _lastRequestId = requestId;

    public void RememberVerification(ExportVerificationDto receipt)
    {
        _lastVerification = receipt;
        _verifications.Enqueue(receipt);
        while (_verifications.Count > VerificationHistoryLimit && _verifications.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<ExportVerificationDto> RecentVerifications() =>
        _verifications.Reverse().Take(VerificationHistoryLimit).ToArray();

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
