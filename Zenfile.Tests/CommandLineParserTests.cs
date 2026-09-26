using Zenfile.Models;
using Zenfile.Services;
using Xunit;

namespace Zenfile.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void Parse_EmptyArgs_ReturnsDefault()
    {
        var result = CommandLineParser.Parse([]);
        Assert.False(result.IsPickerMode);
        Assert.Null(result.TargetDirectory);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Parse_DirectTargetWithFiles_ParsesDirectTransfer()
    {
        var args = new[] { "--target", @"C:\Projects\Task1", @"C:\Files\file1.txt", @"C:\Files\file2.txt" };
        var result = CommandLineParser.Parse(args);

        Assert.Equal(@"C:\Projects\Task1", result.TargetDirectory);
        Assert.Equal(2, result.Files.Count);
        Assert.True(result.IsDirectTransfer);
        Assert.False(result.IsPickerMode);
    }

    [Fact]
    public void Parse_PickerFlagWithFiles_SetsPickerMode()
    {
        var args = new[] { "--picker", @"C:\Files\file1.txt" };
        var result = CommandLineParser.Parse(args);

        Assert.True(result.IsPickerMode);
        Assert.Single(result.Files);
        Assert.Equal(@"C:\Files\file1.txt", result.Files[0]);
        Assert.False(result.IsDirectTransfer);
    }

    [Fact]
    public void Parse_ModeAndConflictFlags_ParsesCorrectly()
    {
        var args = new[] { "--mode", "copy", "--conflict", "overwrite", "--target", @"C:\Target", @"C:\Files\a.txt" };
        var result = CommandLineParser.Parse(args);

        Assert.Equal(TransferMode.Copy, result.Mode);
        Assert.Equal(ConflictResolution.Overwrite, result.ConflictResolution);
        Assert.Equal(@"C:\Target", result.TargetDirectory);
    }
}
