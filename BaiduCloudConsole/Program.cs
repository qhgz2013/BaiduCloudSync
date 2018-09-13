using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaiduCloudSync;
using GlobalUtil;
using GlobalUtil.NetUtils;
using System.IO;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Diagnostics;

namespace BaiduCloudConsole
{
    class Program
    {
        private static void Main(string[] args)
        {
            var oauth = new BaiduCloudSync.oauth.OAuthPCWebImpl();

            string username = null, password = null, captcha = null;
            bool keep_captcha = false;
            for (;;)
            {
                if (!keep_captcha)
                {
                    Console.WriteLine("username:");
                    username = Console.ReadLine();
                    Console.WriteLine("password:");
                    password = Console.ReadLine();
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
                    Console.WriteLine("验证码错误");

                    var img = (Image)oauth.GetCaptcha();
                    Console.WriteLine("captcha:");
                    img.Save("a.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                    var proc = Process.Start("a.bmp");
                    captcha = Console.ReadLine();
                    keep_captcha = true;
                }
                catch (BaiduCloudSync.oauth.CaptchaRequiredException)
                {
                    var img = (Image)oauth.GetCaptcha();
                    Console.WriteLine("captcha:");
                    img.Save("a.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                    var proc = Process.Start("a.bmp");
                    captcha = Console.ReadLine();
                    keep_captcha = true;
                }
                finally
                {
                    if (!keep_captcha)
                        captcha = null;
                }
            }
        }
    }
}
