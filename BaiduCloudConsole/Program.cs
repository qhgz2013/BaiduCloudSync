using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaiduCloudSync;
using GlobalUtil.http;
using System.IO;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Diagnostics;
using BaiduCloudSync.oauth.exception;

namespace BaiduCloudConsole
{
    class Program
    {
        private static void Main(string[] args)
        {
            HttpSession.LoadCookie("data/cookie.dat");
            BaiduCloudSync.oauth.IOAuth oauth = new BaiduCloudSync.oauth.OAuthPCWebImpl("default");

            string username = null, password = null, captcha = null;
            bool keep_captcha = false;
            while (!oauth.IsLogin)
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
                catch (WrongPasswordException)
                {
                    Console.WriteLine("Password incorrect");
                }
                catch (InvalidCaptchaException)
                {
                    Console.WriteLine("Captcha incorrect");

                    var img = (Image)oauth.GetCaptcha();
                    Console.WriteLine("captcha:");
                    img.Save("a.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                    var proc = Process.Start("a.bmp");
                    captcha = Console.ReadLine();
                    keep_captcha = true;
                }
                catch (CaptchaRequiredException)
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

            Console.WriteLine("login succeeded");
            Console.WriteLine("Baiduid: " + oauth.BaiduID);
            Console.WriteLine("Bduss: " + oauth.BDUSS);
            Console.WriteLine("Stoken: " + oauth.SToken);

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
