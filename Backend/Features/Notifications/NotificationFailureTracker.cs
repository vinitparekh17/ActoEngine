using System.Threading;

namespace ActoEngine.WebApi.Features.Notifications;

public interface INotificationFailureTracker
{
    int RecordFailure();
    void Reset();
    int FailureThreshold { get; }
    int CurrentFailureCount { get; }
}

public class NotificationFailureTracker : INotificationFailureTracker
{
    private int _consecutiveFailures;
    public int FailureThreshold => 5;
    public int CurrentFailureCount => Volatile.Read(ref _consecutiveFailures);

    public int RecordFailure()
    {
        return Interlocked.Increment(ref _consecutiveFailures);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }
}
