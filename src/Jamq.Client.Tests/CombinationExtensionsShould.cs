using FluentAssertions;

namespace Jamq.Client.Tests;

public class CombinationExtensionsShould
{
    [Fact]
    public void CreateTrivialCombinations()
    {
        var list = new[] {1, 2, 3};
        list.GetCombinations(1).Should().BeEquivalentTo(new[]
        {
            new[] {1},
            new[] {2},
            new[] {3},
        });
    }

    [Fact]
    public void CreateTwoOutOfThreeCombinations()
    {
        var list = new[] {1, 2, 3};
        list.GetCombinations(2).Should().BeEquivalentTo(new[]
        {
            new[] {1, 2},
            new[] {1, 3},
            new[] {2, 1},
            new[] {2, 3},
            new[] {3, 1},
            new[] {3, 2},
        });
    }

    [Fact]
    public void CreateThreeOutOfThreeCombinations()
    {
        var list = new[] {typeof(int), typeof(string), typeof(DateTime)};
        list.GetCombinations(3).Should().BeEquivalentTo(new[]
        {
            new[] {typeof(int), typeof(string), typeof(DateTime)},
            new[] {typeof(int), typeof(DateTime), typeof(string)},
            new[] {typeof(string), typeof(int), typeof(DateTime)},
            new[] {typeof(string), typeof(DateTime), typeof(int)},
            new[] {typeof(DateTime), typeof(int), typeof(string)},
            new[] {typeof(DateTime), typeof(string), typeof(int)},
        });
    }

    [Fact]
    public void CreateCombinationsWithSameValues()
    {
        var list = new[] { typeof(string), typeof(string), typeof(int) };
        list.GetCombinations(2).Should().BeEquivalentTo(new[]
        {
            new[] { typeof(string), typeof(string) },
            new[] { typeof(string), typeof(string) },
            new[] { typeof(string), typeof(int) },
            new[] { typeof(int), typeof(string) },
            new[] { typeof(string), typeof(int) },
            new[] { typeof(int), typeof(string) },
        });
    }
}