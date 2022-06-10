namespace RMQ.Client.Abstractions;

public sealed class ParallelProcessing
{
    public PrefetchCount WithMaximumDegree(ushort maximumDegree)
    {
        if (maximumDegree <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDegree), maximumDegree,
                $"Minimum parallelism degree is 2. If you need it to be 1, use {nameof(ProcessingOrder.Sequential)} processing order instead");
        }

        return new PrefetchCount(maximumDegree);
    }
}