using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>Customers who come in or leave together are said in one message (1.4.3).</summary>
    public class GroupedNamesTests
    {
        private static string Arrived(params string[] names) =>
            GroupedNames.Describe(names, "customer_arrived", "customers_arrived_same", "customers_arrived_mixed");

        [Fact]
        public void OneCustomerKeepsTheUsualMessage()
        {
            Loc.SetLanguage("en");
            Assert.Equal("Naughty Kid came in.", Arrived("Naughty Kid"));
            Assert.Equal(string.Empty, Arrived());
        }

        [Fact]
        public void TheSameCustomerSeveralTimesIsCounted()
        {
            Loc.SetLanguage("th");
            try
            {
                Assert.Equal("เด็กโปก 3 คนเข้ามาในร้าน", Arrived("เด็กโปก", "เด็กโปก", "เด็กโปก"));
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        }

        [Fact]
        public void DifferentCustomersAreListedInTheOrderTheyCame()
        {
            Loc.SetLanguage("en");
            Assert.Equal("3 customers came in: 2 Naughty Kid, Uncle.", Arrived("Naughty Kid", "Uncle", "Naughty Kid"));
            Assert.Equal("2 customers left the shop: Uncle, Naughty Kid.",
                GroupedNames.Describe(new[] { "Uncle", "Naughty Kid" }, "customer_left", "customers_left_same", "customers_left_mixed"));
        }
    }
}
