namespace Jamq.Client;

internal static class CombinationExtensions
{
    private static IEnumerable<int[]> Combinations(int combinationSize, int listSize)
    {
        var result = new int[combinationSize];
        var stack = new Stack<int>(combinationSize);
        stack.Push(0);
        while (stack.Count > 0)
        {
            var index = stack.Count - 1;
            var value = stack.Pop();
            while (value < listSize)
            {
                result[index++] = value++;
                stack.Push(value);
                if (index != combinationSize) continue;
                yield return (int[])result.Clone();
                break;
            }
        }
    }

    private static IEnumerable<T[]> Permutations<T>(IReadOnlyCollection<T> input, int? size = null)
    {
        size ??= input.Count;
        if (size == 1) return input.Select(t => new[] { t });

        return Permutations(input, size - 1)
            .SelectMany(input.Except,
                (t1, t2) => t1.Concat(new[] {t2}).ToArray());
    }

    public static IEnumerable<IEnumerable<T>> GetCombinations<T>(this T[] input, int size)
    {
        var result = new T[size];
        foreach (var indexes in Combinations(size, input.Length).SelectMany(x => Permutations(x)))
        {
            for (var i = 0; i < size; i++)
            {
                result[i] = input[indexes[i]];
            }

            yield return (T[])result.Clone();
        }
    }
}