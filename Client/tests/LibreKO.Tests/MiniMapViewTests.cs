using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class MiniMapViewTests
{
    [Theory]
    [InlineData(512f, 100f, 1024f, 512f)]
    [InlineData(30f, 100f, 1024f, 100f)]
    [InlineData(1010f, 100f, 1024f, 924f)]
    [InlineData(40f, 300f, 512f, 256f)]
    public void TheViewStopsAtTheMapBorder(float position, float radius, float extent, float centre) =>
        Assert.Equal(centre, MiniMapView.ClampCenter(position, radius, extent));
}
