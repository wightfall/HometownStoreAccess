using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class FloorNavTests
    {
        /// <summary>The level 1 shop: 6 shop-floor cells, a wall, 4 back-room cells (x = -4 .. 6).</summary>
        private static FloorRow Level1(string occupants = null)
        {
            var row = new FloorRow(-3);
            for (int x = -4; x <= 6; x++)
            {
                CellKind kind = x <= 1 ? CellKind.ShopFloor : x == 2 ? CellKind.None : CellKind.BackRoom;
                row.Cells.Add(new FloorCell(x, kind));
            }
            return row;
        }

        private static void Occupy(FloorRow row, int x, int size, string id)
        {
            for (int i = 0; i < size; i++) row.At(x + i).Occupant = id;
        }

        [Fact]
        public void Step_MovesAndStopsAtEdges()
        {
            FloorRow row = Level1();
            Assert.Equal(-3, FloorNav.Step(row, -4, 1));
            Assert.Equal(-4, FloorNav.Step(row, -4, -1));
            Assert.Equal(6, FloorNav.Step(row, 6, 1));
        }

        [Fact]
        public void NextPointOfInterest_SkipsCurrentBuildingAndLandsOnLeftEdge()
        {
            FloorRow row = Level1();
            Occupy(row, -4, 2, "shelf");
            Occupy(row, -1, 2, "cashier");
            Occupy(row, 3, 2, "storage");
            Assert.Equal(-1, FloorNav.NextPointOfInterest(row, -4, 1));
            Assert.Equal(-1, FloorNav.NextPointOfInterest(row, -3, 1));
            Assert.Equal(3, FloorNav.NextPointOfInterest(row, -1, 1));
            Assert.Null(FloorNav.NextPointOfInterest(row, 4, 1));
            // Going left from the right half of the cashier lands on the shelf's first cell.
            Assert.Equal(-4, FloorNav.NextPointOfInterest(row, 0, -1));
            Assert.Equal(3, FloorNav.NextPointOfInterest(row, 6, -1));
            Assert.Equal(-1, FloorNav.NextPointOfInterest(row, 3, -1));
        }

        [Fact]
        public void NextPointOfInterest_IncludesSpecialStops()
        {
            FloorRow row = Level1();
            row.Cells.Insert(0, new FloorCell(-5, CellKind.Special, "delivery"));
            Assert.Equal(-5, FloorNav.NextPointOfInterest(row, -4, -1));
        }

        [Fact]
        public void Pan_SpansTheRow()
        {
            FloorRow row = Level1();
            Assert.Equal(-1f, FloorNav.Pan(row, -4), 3);
            Assert.Equal(1f, FloorNav.Pan(row, 6), 3);
            Assert.Equal(0f, FloorNav.Pan(row, 1), 3);
        }

        [Fact]
        public void CheckPlacement_ReportsTheProblem()
        {
            FloorRow row = Level1();
            Occupy(row, -1, 2, "cashier");
            Assert.True(FloorNav.CheckPlacement(row, -4, 2, CellKind.ShopFloor).Ok);
            PlacementCheck blocked = FloorNav.CheckPlacement(row, -2, 2, CellKind.ShopFloor);
            Assert.Equal(PlacementProblem.Occupied, blocked.Problem);
            Assert.Equal("cashier", blocked.BlockingOccupant);
            Assert.Equal(PlacementProblem.WrongArea, FloorNav.CheckPlacement(row, 3, 2, CellKind.ShopFloor).Problem);
            Assert.Equal(PlacementProblem.OutOfArea, FloorNav.CheckPlacement(row, 1, 2, CellKind.ShopFloor).Problem);
            Assert.Equal(PlacementProblem.OutOfArea, FloorNav.CheckPlacement(row, 6, 2, CellKind.BackRoom).Problem);
            Assert.True(FloorNav.CheckPlacement(row, 5, 2, CellKind.BackRoom).Ok);
        }

        [Fact]
        public void CheckPlacement_IgnoresTheBuildingBeingMoved()
        {
            FloorRow row = Level1();
            Occupy(row, -4, 2, "shelf");
            Assert.True(FloorNav.CheckPlacement(row, -4, 2, CellKind.ShopFloor, ignoreOccupant: "shelf").Ok);
        }

        [Fact]
        public void FirstFit_PrefersCursorThenScans()
        {
            FloorRow row = Level1();
            Occupy(row, -4, 2, "shelf");
            Assert.Equal(0, FloorNav.FirstFit(row, 2, CellKind.ShopFloor, 0));
            Assert.Equal(-2, FloorNav.FirstFit(row, 2, CellKind.ShopFloor, -4));
            Assert.Equal(3, FloorNav.FirstFit(row, 2, CellKind.BackRoom, -4));
            Occupy(row, 3, 4, "big");
            Assert.Null(FloorNav.FirstFit(row, 2, CellKind.BackRoom, 3));
        }
    }
}
