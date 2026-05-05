using System.Text;

using hpack;
using NUnit.Framework;

namespace NUnitTests
{
	public class StaticTableTest
	{
        [Test]
		public void TestGetIndexByName()
		{
            var indexOfAuthority = StaticTable.GetIndex(Encoding.UTF8.GetBytes(":authority"));
            Assert.That(indexOfAuthority, Is.EqualTo(1));
        }

        [Test]
		public void TestGetIndexByUnknownName()
		{
            var indexOfInvalid = StaticTable.GetIndex(Encoding.UTF8.GetBytes(":invalid"));
            Assert.That(indexOfInvalid, Is.EqualTo(-1));
        }
    }
}
