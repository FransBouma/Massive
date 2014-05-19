using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class InsertTests
    {
        [TestMethod]
        public void InsertOriginalTest()
        {
            // arrange
            DummyTableContext tbl = new DummyTableContext();
            var expected = DBNull.Value;

            // act
            var actual = tbl.InsertOriginal(new 
            {
                LastName = "TestLastOld",
                FirstName = "TestFirst"
            });

            // assert
            Assert.AreEqual(expected, actual.ID);

        }

        [TestMethod]
        public void InsertNewTest()
        {
            // arrange
            DummyTableContext tbl = new DummyTableContext();

            // act
            var actual = tbl.InsertNew(new 
            { 
                LastName = "TestLastNew",
                FirstName = "TestFirst"
            });

            // assert
            Assert.IsTrue(actual.ID > 0);
        }
    }
}
