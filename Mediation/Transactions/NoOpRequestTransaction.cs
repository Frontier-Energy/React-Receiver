namespace React_Receiver.Mediation.Transactions;

public sealed class NoOpRequestTransaction : IRequestTransaction
{
    public Task BeginAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
