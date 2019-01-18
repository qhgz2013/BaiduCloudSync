using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BaiduCloudSync.api;

namespace BaiduCloudSync_Test.api
{
    [TestClass]
    public class PcsPathTest
    {
        [TestMethod]
        public void PcsPathTest1()
        {
            Assert.AreEqual("/", new PcsPath("/").ToString());
            Assert.AreEqual("/", new PcsPath("/././/").ToString());
            Assert.AreEqual("/a/b", new PcsPath("/a//./b").ToString());
            Assert.AreEqual("/a/d", new PcsPath("/a/b/c/../.././/d").ToString());
            Assert.AreEqual("/", new PcsPath("/a/../").ToString());
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void PcsPathTest2()
        {
            new PcsPath("/a/../..");
            Assert.Fail();
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PcsPathTest3()
        {
            new PcsPath("");
            Assert.Fail();
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void PcsPathTest4()
        {
            new PcsPath("/nooo:D");
            Assert.Fail();
        }

        [TestMethod]
        public void PcsPathExtensionTest()
        {
            Assert.AreEqual("abc", new PcsPath("/a/b/c.abc").Extension);
            Assert.AreEqual("c", new PcsPath("/a/b/c.abc").NameWithoutExtension);
            Assert.AreEqual(".gitignore", new PcsPath("/./.gitignore").NameWithoutExtension);
            Assert.AreEqual("", new PcsPath("/.gitignore").Extension);
            Assert.AreEqual("abc.", new PcsPath("/a/b/../abc.").NameWithoutExtension);
            Assert.AreEqual("/a/abc.", new PcsPath("/a/b/../abc.").FullPath);

        }
    }
}
