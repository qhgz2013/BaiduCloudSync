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
            Console.WriteLine("username:");
            var username = Console.ReadLine();
            Console.WriteLine("password:");
            var password = Console.ReadLine();

            try
            {
                oauth.Login(username, password);
            }
            catch (BaiduCloudSync.oauth.CaptchaRequiredException)
            {
                var img = (Image)oauth.GetCaptcha();
                Console.WriteLine("captcha:");
                img.Save("a.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                var proc = Process.Start("a.bmp");
                var captcha = Console.ReadLine();

                oauth.Login(username, password, captcha);
            }
        }
    }
}
