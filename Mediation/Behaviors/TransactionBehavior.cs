using MediatR;
using React_Receiver.Mediation.Transactions;

namespace React_Receiver.Mediation.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRequestTransaction _transaction;

    public TransactionBehavior(IRequestTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
        {
            return await next();
        }

        await _transaction.BeginAsync(cancellationToken);

        try
        {
            var response = await next();
            await _transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
