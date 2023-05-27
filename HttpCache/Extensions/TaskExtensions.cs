namespace HttpCache.Extensions;

public static class TaskExtensions
{
    public static Task AsTask(this CancellationToken cancellationToken)
    {
        var cts = new TaskCompletionSource();
        cancellationToken.Register(
            () => cts.TrySetCanceled(),
            useSynchronizationContext: false
        );

        return cts.Task;
    }
}