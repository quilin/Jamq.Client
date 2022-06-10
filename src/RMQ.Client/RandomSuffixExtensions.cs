namespace RMQ.Client;

internal static class RandomSuffixExtensions
{
    public static string WithRandomSuffix(this string input) =>
        $"{input}-{Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..6]}";
}