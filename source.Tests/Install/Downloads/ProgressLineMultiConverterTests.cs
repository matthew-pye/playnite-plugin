using Graviton.Install.Downloads;

using System.Globalization;

using Xunit;

namespace Graviton.Tests.Install.Downloads;

public sealed class ProgressLineMultiConverterTests
{
    private readonly ProgressLineMultiConverter _converter = new();

    [Fact]
    public void Convert_WithKnownSize_DisplaysBytesAndPercentage()
    {
        var result = Convert(512L, 1024L, false);

        Assert.Equal("512 B / 1.0 KB (50%)", result);
    }

    [Fact]
    public void Convert_WithLargerSize_DisplaysMegabytes()
    {
        var result = Convert(1048576L, 4194304L, false);

        Assert.Equal("1.0 MB / 4.0 MB (25%)", result);
    }

    [Fact]
    public void Convert_WithUnknownSize_HidesProgress()
    {
        var result = Convert(1536L, 1536L, true);

        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Convert_WithNonPositiveMaximum_HidesProgress(int maximum)
    {
        var result = Convert(512, maximum, false);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_WithProgressAboveMaximum_ClampsPercentage()
    {
        var result = Convert(2048, 1024, false);

        Assert.Equal("2.0 KB / 1.0 KB (100%)", result);
    }

    [Fact]
    public void Convert_WithNumericStrings_FormatsProgress()
    {
        var result = Convert("1536", "2048", false);

        Assert.Equal("1.5 KB / 2.0 KB (75%)", result);
    }

    [Fact]
    public void Convert_WithMissingValues_ReturnsEmptyString()
    {
        var result = _converter.Convert(
            [512, 1024],
            typeof(string),
            new object(),
            CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_WithInvalidMaximum_ReturnsEmptyString()
    {
        var result = Convert(512, "not a number", false);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_WithFrenchCulture_UsesInvariantFormatting()
    {
        var result = _converter.Convert(
            [1536L, 2048L, false],
            typeof(string),
            new object(),
            CultureInfo.GetCultureInfo("fr-FR"));

        Assert.Equal("1.5 KB / 2.0 KB (75%)", result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
        {
            _converter.ConvertBack(
                "50%",
                [typeof(double), typeof(double), typeof(bool)],
                new object(),
                CultureInfo.InvariantCulture);
        });
    }

    private object Convert(object current, object maximum, bool isIndeterminate)
    {
        return _converter.Convert(
            [current, maximum, isIndeterminate],
            typeof(string),
            new object(),
            CultureInfo.InvariantCulture);
    }
}
