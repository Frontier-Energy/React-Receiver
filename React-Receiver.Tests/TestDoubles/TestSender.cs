using MediatR;

namespace React_Receiver.Tests.TestDoubles;

public sealed class TestSender : ISender
{
    private readonly Func<object, CancellationToken, Task<object?>> _handler;

    public TestSender(Func<object, CancellationToken, Task<object?>> handler)
    {
        _handler = handler;
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        return (TResponse)(await _handler(request, cancellationToken))!;
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        return _handler(request, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        return EmptyStream<TResponse>();
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        return EmptyStream<object?>();
    }

    private static async IAsyncEnumerable<TResponse> EmptyStream<TResponse>()
    {
        await Task.CompletedTask;
        yield break;
    }

    async Task ISender.Send<TRequest>(TRequest request, CancellationToken cancellationToken)
    {
        await _handler(request!, cancellationToken);
    }

    IAsyncEnumerable<TResponse> ISender.CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken)
    {
        return CreateStream(request, cancellationToken);
    }

    IAsyncEnumerable<object?> ISender.CreateStream(object request, CancellationToken cancellationToken)
    {
        return CreateStream(request, cancellationToken);
    }
}
