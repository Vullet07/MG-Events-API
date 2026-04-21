using Services.Maps;

namespace MGEvents.Tests.Services;

public class IndoorMapGeometryTests
{
    [Fact]
    public void TryResolveZone_ReturnsNamedCampusZone_ForValidOpenCampusPoint()
    {
        var (latitude, longitude) = IndoorMapGeometry.EncodeLayerPoint("campus", 610d, 250d);

        var success = IndoorMapGeometry.TryResolveZone(latitude, longitude, out var zone);

        Assert.True(success);
        Assert.NotNull(zone);
        Assert.Equal("campus", zone!.LayerId);
        Assert.Equal("court", zone.ZoneId);
        Assert.Equal("Голямо игрище", zone.ZoneLabel);
    }

    [Fact]
    public void TryResolveZone_ReturnsFallbackCampusYard_ForCampusPointOutsideBuildings()
    {
        var (latitude, longitude) = IndoorMapGeometry.EncodeLayerPoint("campus", 600d, 150d);

        var success = IndoorMapGeometry.TryResolveZone(latitude, longitude, out var zone);

        Assert.True(success);
        Assert.NotNull(zone);
        Assert.Equal("CAMPUS-YARD", zone!.ZoneId);
        Assert.Equal("Дворна зона", zone.ZoneLabel);
    }

    [Fact]
    public void TryResolveZone_RejectsCampusPointInsideMainBuilding()
    {
        var (latitude, longitude) = IndoorMapGeometry.EncodeLayerPoint("campus", 310d, 250d);

        var success = IndoorMapGeometry.TryResolveZone(latitude, longitude, out var zone);

        Assert.False(success);
        Assert.Null(zone);
    }
}
