using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Drawing;

namespace BaiduCloudSync_Test.oauth
{
    [TestClass]
    public class OAuthPCWebImplTest
    {
        private string _wait_input_str(string title = null)
        {
            if (title == null) title = "";
            else title += "_";
            var gid = title + Guid.NewGuid().ToString() + ".txt";
            var stream_create = new FileStream(gid, FileMode.Create, FileAccess.Write, FileShare.None);
            stream_create.Close();
            FileInfo file;
            Process.Start("notepad", gid);
            do
            {
                file = new FileInfo(gid);
                Thread.Sleep(50);
            } while (file.Length == 0);
            var stream_in = new FileStream(gid, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var sr = new StreamReader(stream_in, System.Text.Encoding.Default);
            var text = sr.ReadToEnd();
            sr.Close();

            for (int retry_count = 0; retry_count < 5; retry_count++)
            {
                try
                {
                    File.Delete(gid);
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(10);
                }
            }
            return text;
        }
        [TestMethod]
        public void LoginTest()
        {
            var config = new BaiduCloudSync.Config();
            var cookie_filename = Guid.NewGuid().ToString();
            config.CookieFileName = cookie_filename;
            BaiduCloudSync.oauth.IOAuth oauth = new BaiduCloudSync.oauth.OAuthPCWebImpl(config: config);

            string username = null, password = null, captcha = null;
            bool keep_captcha = false;
            while (!oauth.IsLogin())
            {
                if (!keep_captcha)
                {
                    username = _wait_input_str("username");
                    password = _wait_input_str("password");
                }
                keep_captcha = false;
                try
                {
                    oauth.Login(username, password, captcha);
                    break;
                }
                catch (BaiduCloudSync.oauth.WrongPasswordException)
                {
                    Console.WriteLine("Password incorrect");
                }
                catch (BaiduCloudSync.oauth.InvalidCaptchaException)
                {
                    Console.WriteLine("Captcha incorrect");

                    var img = (Image)oauth.GetCaptcha();
                    img.Save("a.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                    var proc = Process.Start("a.bmp");
                    captcha = _wait_input_str("captcha");
                    keep_captcha = true;
                }
                catch (BaiduCloudSync.oauth.CaptchaRequiredException)
                {
                    var img = (Image)oauth.GetCaptcha();
                    img.Save("a.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                    var proc = Process.Start("a.bmp");
                    captcha = _wait_input_str("captcha");
                    keep_captcha = true;
                }
                finally
                {
                    if (!keep_captcha)
                        captcha = null;

                    try
                    {
                        File.Delete(cookie_filename);
                    }
                    catch (IOException) { }

                    if (File.Exists("a.bmp"))
                    {
                        try
                        {
                            File.Delete("a.bmp");
                        }
                        catch (IOException) { }
                    }
                }
            }

        }
    }
}
