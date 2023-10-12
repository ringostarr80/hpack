using NUnit.Framework;

namespace hpack
{
	public class StaticTableTest
	{
        [Test]
		public void testGetIndexByName()
		{
            var indexOfAuthority = StaticTable.GetIndex(System.Text.Encoding.UTF8.GetBytes(":authority"));
            Assert.AreEqual(1, indexOfAuthority);
        }

        [Test]
		public void testGetIndexByUnknownName()
		{
            var indexOfInvalid = StaticTable.GetIndex(System.Text.Encoding.UTF8.GetBytes(":invalid"));
            Assert.AreEqual(-1, indexOfInvalid);
        }
    }
}
