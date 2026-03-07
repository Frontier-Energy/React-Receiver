namespace React_Receiver.Application.Concurrency;

public static class OptimisticConcurrency
{
    public static void EnsureSatisfied(string? expectedETag, string? currentETag, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(currentETag))
        {
            if (!string.IsNullOrWhiteSpace(expectedETag))
            {
                throw new ConcurrencyConflictException(
                    $"The supplied If-Match precondition for {resourceName} could not be satisfied because the resource does not exist.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(expectedETag))
        {
            throw new PreconditionRequiredException(
                $"Updates to {resourceName} require an If-Match header.");
        }

        foreach (var candidate in expectedETag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(candidate, "*", StringComparison.Ordinal) ||
                string.Equals(candidate, currentETag, StringComparison.Ordinal))
            {
                return;
            }
        }

        throw new ConcurrencyConflictException(
            $"The supplied If-Match precondition for {resourceName} did not match the current version.");
    }
}
