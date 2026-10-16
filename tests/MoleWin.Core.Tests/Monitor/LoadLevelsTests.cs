using MoleWin.Core.Monitor;

namespace MoleWin.Core.Tests.Monitor;

public sealed class LoadLevelsTests
{
    [Theory]
    [InlineData(10, LoadLevel.Normal)]
    [InlineData(69.9, LoadLevel.Normal)]
    [InlineData(70, LoadLevel.High)]
    [InlineData(90, LoadLevel.Critical)]
    [InlineData(130, LoadLevel.Critical)]
    public void Cpu_levels_follow_load(double percent, LoadLevel expected) =>
        Assert.Equal(expected, LoadLevels.Cpu(percent));

    [Theory]
    [InlineData(79, LoadLevel.Normal)]
    [InlineData(80, LoadLevel.High)]
    [InlineData(95, LoadLevel.Critical)]
    public void Space_levels_follow_fill(double percent, LoadLevel expected) =>
        Assert.Equal(expected, LoadLevels.Fill(percent));

    [Theory]
    [InlineData(100, LoadLevel.Normal)]
    [InlineData(75, LoadLevel.Normal)]
    [InlineData(74, LoadLevel.High)]
    [InlineData(49, LoadLevel.Critical)]
    public void Health_levels_fall_as_the_score_drops(int score, LoadLevel expected) =>
        Assert.Equal(expected, LoadLevels.Health(score));
}
