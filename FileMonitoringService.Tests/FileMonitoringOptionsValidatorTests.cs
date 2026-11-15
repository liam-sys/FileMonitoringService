// DAY 9: tests for the settings checker (Day 2, requirement FR-05).
using FileMonitoringService.Options;
using Xunit;

namespace FileMonitoringService.Tests;

public class FileMonitoringOptionsValidatorTests
{
    private readonly FileMonitoringOptionsValidator _validator = new();

    [Fact]
    public void Fails_when_no_directory_is_configured()
    {
        // Arrange: settings with an empty folder list
        var options = new FileMonitoringOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert: the checker rejected it
        Assert.True(result.Failed);
    }

    [Fact]
    public void Fails_when_a_directory_path_is_relative()
    {
        // "relative\folder" is not a full path (it does not start with a drive such as C:\)
        var options = new FileMonitoringOptions { Directories = { "relative\\folder" } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Fails_when_a_directory_path_is_blank()
    {
        var options = new FileMonitoringOptions { Directories = { "   " } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Succeeds_for_valid_settings()
    {
        // Path.GetTempPath() is a real full path, so it counts as valid
        var options = new FileMonitoringOptions { Directories = { Path.GetTempPath() } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}