using System.Collections.Generic;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class MapGeometryTests
    {
        /// <summary>An L-shaped road: (0,0)..(3,0) then (3,1)..(3,3), plus a dead end at (1,1).</summary>
        private static HashSet<GridPoint> Road()
        {
            var set = new HashSet<GridPoint>();
            for (int x = 0; x <= 3; x++) set.Add(new GridPoint(x, 0));
            for (int y = 1; y <= 3; y++) set.Add(new GridPoint(3, y));
            set.Add(new GridPoint(1, 1));
            return set;
        }

        [Fact]
        public void StepsBetween_FollowsTheRoad()
        {
            var road = Road();
            Assert.Equal(0, MapGeometry.StepsBetween(road, new GridPoint(0, 0), new GridPoint(0, 0)));
            Assert.Equal(6, MapGeometry.StepsBetween(road, new GridPoint(0, 0), new GridPoint(3, 3)));
            Assert.Equal(2, MapGeometry.StepsBetween(road, new GridPoint(0, 0), new GridPoint(1, 1)));
        }

        [Fact]
        public void StepsBetween_UnreachableOrOffRoad()
        {
            var road = Road();
            road.Add(new GridPoint(10, 10));
            Assert.Null(MapGeometry.StepsBetween(road, new GridPoint(0, 0), new GridPoint(10, 10)));
            Assert.Null(MapGeometry.StepsBetween(road, new GridPoint(0, 0), new GridPoint(5, 5)));
        }

        [Fact]
        public void NeighboursOf_ReturnsRoadTiles()
        {
            List<GridPoint> n = MapGeometry.NeighboursOf(Road(), new GridPoint(1, 0));
            Assert.Equal(3, n.Count);
            Assert.Contains(new GridPoint(0, 0), n);
            Assert.Contains(new GridPoint(2, 0), n);
            Assert.Contains(new GridPoint(1, 1), n);
        }

        [Theory]
        [InlineData(0, 100, "dir_n")]
        [InlineData(100, 0, "dir_e")]
        [InlineData(0, -100, "dir_s")]
        [InlineData(-100, 0, "dir_w")]
        [InlineData(70, 70, "dir_ne")]
        [InlineData(-70, -70, "dir_sw")]
        [InlineData(-70, 70, "dir_nw")]
        [InlineData(70, -70, "dir_se")]
        public void CompassKey(double dx, double dy, string expected)
        {
            Assert.Equal(expected, MapGeometry.CompassKey(dx, dy));
        }

        [Fact]
        public void CompassKey_TinyOffsetIsNull()
        {
            Assert.Null(MapGeometry.CompassKey(0.1, 0.1, 1));
        }

        [Fact]
        public void BestMatch_PicksClosestDirection()
        {
            // Isometric board: neighbours drawn up-right and up-left.
            var candidates = new List<(double, double)> { (80, 40), (-80, 40), (80, -40) };
            Assert.Equal(0, MapGeometry.BestMatch(candidates, 1, 0));
            Assert.Equal(1, MapGeometry.BestMatch(candidates, -1, 0));
            Assert.Equal(2, MapGeometry.BestMatch(candidates, 0, -1));
            Assert.Equal(-1, MapGeometry.BestMatch(new List<(double, double)> { (-80, -40) }, 1, 1));
        }
    }
}
