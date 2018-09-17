using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BaiduCloudSync;
using System.IO;

namespace BaiduCloudSync_Test
{
    [TestClass]
    public class ConfigTest
    {
        [TestMethod]
        public void TestSetterForCookieFileName()
        {
            var config = new Config();
            config.CookieFileName = "data/cookie2.abcd";
            Assert.IsTrue(config.CookieFileName.EndsWith("data/cookie2.abcd") || config.CookieFileName.EndsWith("data\\cookie2.abcd"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestSetterForCookieFileNameWithNull()
        {
            var config = new Config();
            config.CookieFileName = null;
            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestSetterForCookieFileNameWithEmptyString()
        {
            var config = new Config();
            config.CookieFileName = "";
            Assert.Fail();
        }

        [TestMethod]
        public void TestSetterForCookieFileNameWithInvalidChar()
        {
            var config = new Config();
            var ex_catcher = new MultipleExceptionTypeCatcher(new Type[] { typeof(ArgumentException), typeof(NotSupportedException) });
            Assert.IsNotNull(ex_catcher.Throws(delegate { config.CookieFileName = "hahaha|"; }));
            Assert.IsNotNull(ex_catcher.Throws(delegate { config.CookieFileName = "hahaha:D"; }));
        }
        [TestMethod]
        public void TestLoadConfig()
        {
            var filename = "test_config.json";
            if (!File.Exists(filename))
            {
                // generate a new config file
                var config2 = new Config();
                config2.SaveConfig(filename);
            }
            var config = new Config();
            config.LoadConfig(filename);

            if (File.Exists(filename))
                File.Delete(filename);

            Assert.IsFalse(string.IsNullOrEmpty(config.CookieFileName));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestLoadConfigWithNullFileName()
        {
            var config = new Config();
            config.LoadConfig(null);
            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestLoadConfigWithEmptyFileName()
        {
            var config = new Config();
            config.LoadConfig("");
            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void TestLoadConfigFromNonExistedFile()
        {
            var config = new Config();
            var filename = Guid.NewGuid().ToString();
            if (File.Exists(filename))
                File.Delete(filename);
            config.LoadConfig(filename);
            Assert.Fail();
        }

        [TestMethod]
        public void TestLoadConfigFromInvalidPath()
        {
            var config = new Config();
            var catcher = new MultipleExceptionTypeCatcher(new Type[] { typeof(FileNotFoundException) });
            Assert.IsNotNull(catcher.Throws(delegate { config.LoadConfig("abcd|"); }));
            Assert.IsNotNull(catcher.Throws(delegate { config.LoadConfig("ab:d?"); }));
        }
        [TestMethod]
        public void TestSaveConfig()
        {
            string filename;
            do
            {
                filename = Guid.NewGuid().ToString();
            } while (File.Exists(filename));
            var config = new Config();
            config.SaveConfig(filename);

            Assert.IsFalse(string.IsNullOrEmpty(File.ReadAllText(filename)));
            File.Delete(filename);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestSaveConfigToNullPath()
        {
            var config = new Config();
            config.SaveConfig(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestSaveConfigToEmptyPath()
        {
            var config = new Config();
            config.SaveConfig("");
        }
        [TestMethod]
        public void TestSaveConfigToPathWithoutCreated()
        {
            var config = new Config();
            string path;
            do
            {
                path = Guid.NewGuid().ToString();
            } while (Directory.Exists(path));

            var dst_filename = Path.Combine(path, "abc", "degf");
            config.SaveConfig(dst_filename);
            Directory.Delete(path, true);
        }

        [TestMethod]
        public void TestSaveConfigToInvalidPath()
        {
            var config = new Config();
            var catcher = new MultipleExceptionTypeCatcher(new Type[] { typeof(NotSupportedException), typeof(ArgumentException) });
            Assert.IsNotNull(catcher.Throws(delegate { config.SaveConfig("abcd:/test"); }));
            Assert.IsNotNull(catcher.Throws(delegate { config.SaveConfig("hhhh?"); }));
        }
    }
}
