using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using React_Receiver.Application.Inspections;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;
using React_Receiver.Services;
using React_Receiver.Tests.TestDoubles;
using Xunit;

namespace React_Receiver.Tests;

public sealed class InspectionIngestRetryHostedServiceTests
{
    [Fact]
    public async Task HostedService_ProcessesPendingSessionsConcurrently_AndKeepsGoingAfterPerSessionFailure()
    {
        var repository = new StubInspectionRepository(
            ["session-a", "session-b", "session-c"],
            []);
        var attemptedSessions = new HashSet<string>(StringComparer.Ordinal);
        var allAttemptsCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var atLeastTwoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = 0;
        var maxConcurrency = 0;
        var attempts = 0;

        var sender = new TestSender(async (request, cancellationToken) =>
        {
            var command = Assert.IsType<ProcessInspectionIngestCommand>(request);
            lock (attemptedSessions)
            {
                attemptedSessions.Add(command.SessionId);
            }

            var currentConcurrency = Interlocked.Increment(ref inFlight);
            UpdateMax(ref maxConcurrency, currentConcurrency);
            if (currentConcurrency >= 2)
            {
                atLeastTwoStarted.TrySetResult();
            }

            try
            {
                await atLeastTwoStarted.Task.WaitAsync(cancellationToken);

                if (string.Equals(command.SessionId, "session-b", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("simulated failure");
                }

                return false;
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
                if (Interlocked.Increment(ref attempts) == 3)
                {
                    allAttemptsCompleted.TrySetResult();
                }
            }
        });

        using var service = CreateHostedService(
            repository,
            sender,
            new InspectionIngestRetryOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(25),
                BatchSize = 3,
                MaxConcurrentSessions = 3
            });

        await service.StartAsync(CancellationToken.None);
        await allAttemptsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        Assert.True(maxConcurrency >= 2, "Expected at least two sessions to be processed concurrently.");
        Assert.Equal(3, attemptedSessions.Count);
        Assert.Contains("session-a", attemptedSessions);
        Assert.Contains("session-b", attemptedSessions);
        Assert.Contains("session-c", attemptedSessions);
    }

    [Fact]
    public async Task HostedService_RePollsImmediately_WhenBatchIsFull()
    {
        var repository = new StubInspectionRepository(
            ["session-a", "session-b"],
            ["session-c"],
            []);
        var backlogDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sender = new TestSender((request, cancellationToken) =>
        {
            var command = Assert.IsType<ProcessInspectionIngestCommand>(request);
            if (string.Equals(command.SessionId, "session-c", StringComparison.Ordinal))
            {
                backlogDrained.TrySetResult();
            }

            return Task.FromResult<object?>(false);
        });

        using var service = CreateHostedService(
            repository,
            sender,
            new InspectionIngestRetryOptions
            {
                PollInterval = TimeSpan.FromSeconds(5),
                BatchSize = 2,
                MaxConcurrentSessions = 2
            });

        await service.StartAsync(CancellationToken.None);
        await backlogDrained.Task.WaitAsync(TimeSpan.FromMilliseconds(750));
        await service.StopAsync(CancellationToken.None);
    }

    private static InspectionIngestRetryHostedService CreateHostedService(
        IInspectionRepository repository,
        ISender sender,
        InspectionIngestRetryOptions options)
    {
        var scopeFactory = new TestScopeFactory(() =>
        {
            var services = new ServiceCollection();
            services.AddSingleton(repository);
            services.AddSingleton(sender);
            return services.BuildServiceProvider();
        });

        return new InspectionIngestRetryHostedService(
            scopeFactory,
            NullLogger<InspectionIngestRetryHostedService>.Instance,
            Options.Create(options));
    }

    private static void UpdateMax(ref int currentMax, int candidate)
    {
        while (true)
        {
            var snapshot = currentMax;
            if (candidate <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref currentMax, candidate, snapshot) == snapshot)
            {
                return;
            }
        }
    }

    private sealed class StubInspectionRepository : IInspectionRepository
    {
        private readonly Queue<IReadOnlyCollection<string>> _batches;

        public StubInspectionRepository(params IReadOnlyCollection<string>[] batches)
        {
            _batches = new Queue<IReadOnlyCollection<string>>(batches);
        }

        public Task<IReadOnlyCollection<string>> GetPendingSessionIdsAsync(int maxResults, CancellationToken cancellationToken)
        {
            lock (_batches)
            {
                if (_batches.Count == 0)
                {
                    return Task.FromResult<IReadOnlyCollection<string>>([]);
                }

                return Task.FromResult(_batches.Dequeue());
            }
        }

        public Task<ReceiveInspectionResponse> PrepareAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ProcessPendingAsync(string sessionId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GetInspectionResponse?> GetAsync(string sessionId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<InspectionFileStreamResult?> GetFileAsync(string sessionId, string fileName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly Func<IServiceProvider> _serviceProviderFactory;

        public TestScopeFactory(Func<IServiceProvider> serviceProviderFactory)
        {
            _serviceProviderFactory = serviceProviderFactory;
        }

        public IServiceScope CreateScope()
        {
            return new TestScope(_serviceProviderFactory());
        }
    }

    private sealed class TestScope : IServiceScope, IAsyncDisposable
    {
        public TestScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public ValueTask DisposeAsync()
        {
            if (ServiceProvider is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
