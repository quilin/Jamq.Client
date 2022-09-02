namespace Jamq.Client;

internal static class CombinationExtensions
{
    public static IEnumerable<IEnumerable<T>> GetCombinations<T>(this ICollection<T> input, int size)
    {
        if (size == 1)
        {
            return input.Select(i => new[] {i});
        }

        return input.SelectMany(i =>
        {
            var value = new[] {i};
            return GetCombinations(input.Except(value).ToArray(), size - 1).Select(value.Concat);
        });
    }
}