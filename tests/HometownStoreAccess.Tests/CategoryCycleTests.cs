using System.Linq;
using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    public class CategoryCycleTests
    {
        [Fact]
        public void StartsWithEverythingAndWraps()
        {
            var c = new CategoryCycle();
            c.SetOptions(new[] { "Drink", "Work" });
            Assert.Equal(CategoryCycle.All, c.Current);
            Assert.Equal("Drink", c.Step(1));
            Assert.Equal("Work", c.Step(1));
            Assert.Equal(CategoryCycle.All, c.Step(1));
            Assert.Equal("Work", c.Step(-1));
            Assert.Equal(3, c.Position);
            Assert.Equal(3, c.Count);
        }

        [Fact]
        public void KeepsTheChoiceWhileItStillExists()
        {
            var c = new CategoryCycle();
            c.SetOptions(new[] { "Drink", "Work" });
            c.Step(1);
            c.SetOptions(new[] { "Apparel", "Drink" });
            Assert.Equal("Drink", c.Current);
            c.SetOptions(new[] { "Apparel" });
            Assert.Equal(CategoryCycle.All, c.Current);
        }

        [Fact]
        public void IgnoresDuplicatesAndEmpties()
        {
            var c = new CategoryCycle();
            c.SetOptions(new[] { "Drink", "", null, "Drink", CategoryCycle.All, "Work" });
            Assert.Equal(new[] { CategoryCycle.All, "Drink", "Work" }, c.Options.ToArray());
        }

        [Theory]
        [InlineData("*", "item:Drink", true)]
        [InlineData("item", "item:Drink", true)]
        [InlineData("item", "item", true)]
        [InlineData("item", "items:Drink", false)]
        [InlineData("item:Drink", "item:Drink", true)]
        [InlineData("item:Drink", "item:DrinkCan", false)]
        [InlineData("shelf", "item:Drink", false)]
        [InlineData("item", "", false)]
        public void CoversSubCategories(string option, string category, bool expected)
        {
            Assert.Equal(expected, CategoryCycle.Covers(option, category));
        }

        [Fact]
        public void OptionsForGoodsAndBuildings()
        {
            string[] products = { "item:Drink", "item:FoodAndSnack", "item:Drink", "shelf:Storage" };
            Assert.Equal(new[] { "item", "item:Drink", "item:FoodAndSnack", "shelf" }, CategoryCycle.OptionsFor(products).ToArray());
            Assert.Equal(3, CategoryCycle.CountFor("item", products));
            Assert.Equal(1, CategoryCycle.CountFor("shelf", products));
            Assert.Equal(4, CategoryCycle.CountFor(CategoryCycle.All, products));
        }

        [Fact]
        public void OptionsForASingleTypeListOnlyItsCategories()
        {
            // "all goods" would show the same as everything, so it is left out.
            Assert.Equal(new[] { "item:Drink", "item:Work" }, CategoryCycle.OptionsFor(new[] { "item:Drink", "item:Work" }).ToArray());
            Assert.Empty(CategoryCycle.OptionsFor(new[] { "item:Drink", "item:Drink" }));
        }

        [Fact]
        public void OptionsForFlatCategories()
        {
            Assert.Equal(new[] { "Shelf", "Storage" }, CategoryCycle.OptionsFor(new[] { "Shelf", "Storage", "Shelf" }).ToArray());
            Assert.Empty(CategoryCycle.OptionsFor(new[] { "Shelf" }));
            Assert.Empty(CategoryCycle.OptionsFor(new string[0]));
        }

        [Fact]
        public void SingleOptionStaysOnEverything()
        {
            var c = new CategoryCycle();
            c.SetOptions(new string[0]);
            Assert.Equal(CategoryCycle.All, c.Step(1));
            Assert.Equal(CategoryCycle.All, c.Step(-1));
            Assert.True(c.Matches("anything"));
        }
    }
}
