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

namespace BaiduCloudConsole
{
    class Program
    {
        private static RemoteFileCacher _remote_file_cacher;
        private static LocalFileCacher _local_file_cacher;
        private static KeyManager _key_manager;

        private static string _version = "1.0.0 pre-alpha";
        private static void Main(string[] args)
        {
            if (!Directory.Exists("data"))
                Directory.CreateDirectory("data");
            NetStream.LoadCookie("data/cookie.dat");
            _key_manager = new KeyManager();
            if (File.Exists("data/rsa_key.pem"))
                _key_manager.LoadKey("data/rsa_key.pem");
            if (File.Exists("data/aes_key.dat"))
                _key_manager.LoadKey("data/aes_key.dat");

            Console.WriteLine("BaiduCloudSync 控制台模式");
            Console.WriteLine("Version: {0}", _version);
            Console.WriteLine("");

            try
            {
                if (args.Length == 0)
                    _print_no_arg();
                else
                {
                    _check_and_login();

                    _exec_command(args);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            if (!Directory.Exists("data"))
                Directory.CreateDirectory("data");
            NetStream.SaveCookie("data/cookie.dat");
            _key_manager.SaveKey("data/rsa_key.pem", true);
            _key_manager.SaveKey("data/aes_key.dat", false);
        }
        //检验登陆状态，如未登陆则登陆
        private static void _check_and_login()
        {
            var account_count = NetStream.DefaultCookieContainer.Keys.Count;
            if (account_count == 0)
            {
                Console.WriteLine("未检测到登陆信息，请登陆 [L] 或者通过cookie传递账号信息 [C]");
                Console.Write("输入 [L] 或 [C] > ");
                var str = Console.ReadLine().ToLower();
                var oauth = new BaiduOAuth();
                if (str == "l")
                {
                    Console.Write("输入账号 > ");
                    var username = Console.ReadLine();
                    Console.Write("输入密码 > ");
                    string password = string.Empty;
                    while (true)
                    {
                        var key_data = Console.ReadKey(true);
                        if (key_data.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine();
                            break;
                        }
                        else if (key_data.Key == ConsoleKey.Backspace)
                        {
                            if (password.Length > 0)
                            {
                                password = password.Substring(0, password.Length - 1);
                                Console.Write("\b \b");
                            }
                        }
                        else
                        {
                            if (key_data.KeyChar > 0)
                            {
                                password += key_data.KeyChar;
                                Console.Write("*");
                            }
                        }
                    }

                    bool captcha_required = false;
                    string captcha = string.Empty;

                    oauth.LoginCaptchaRequired += delegate
                    {
                        captcha_required = true;
                    };

                    bool login_suc = false;
                    while (login_suc == false)
                    {
                        if (captcha_required)
                            oauth.SetVerifyCode(captcha);

                        Console.WriteLine("开始登陆");
                        captcha_required = false;
                        login_suc = oauth.Login(username, password);
                        if (login_suc)
                            Console.WriteLine("登陆成功");
                        else
                            Console.WriteLine("登陆失败: [{0}]: {1}", oauth.GetLastFailedCode, oauth.GetLastFailedReason);

                        if (captcha_required)
                        {
                            Console.WriteLine("下载验证码图片...");
                            var img = oauth.GetCaptcha();
                            if (!Directory.Exists("cache"))
                                Directory.CreateDirectory("cache");
                            img.Save("cache/captcha.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                            System.Diagnostics.Process.Start(Path.Combine(Environment.CurrentDirectory, "cache", "captcha.bmp"));
                            Console.Write("请输入该验证码 > ");
                            captcha = Console.ReadLine();
                        }
                    }
                }
                else if (str == "c")
                {
                    //login by cookie
                    Console.Write("请输入cookie.txt的文件路径 > ");
                    var path = Console.ReadLine();
                    if (string.IsNullOrEmpty(path))
                    {
                        Console.WriteLine("错误：空路径");
                        Environment.Exit(0);
                    }
                    else if (File.Exists(path) == false)
                    {
                        Console.WriteLine("错误：文件不存在");
                        Environment.Exit(0);
                    }
                    _parse_cookie_txt(path);
                }
                else
                {
                    Console.WriteLine("非法输入");
                    Environment.Exit(0);
                }
                NetStream.SaveCookie("data/cookie.dat");
                _remote_file_cacher = new RemoteFileCacher();
                _remote_file_cacher.AddAccount(new BaiduPCS(oauth));
                _local_file_cacher = new LocalFileCacher();
                Console.WriteLine("欢迎回来，" + oauth.NickName);
            }

        }
        private static void _parse_cookie_txt(string _path)
        {
            var text = File.ReadAllLines(_path);
            var key = "default";
            if (NetStream.DefaultCookieContainer.Keys.Count > 0)
                key = NetStream.DefaultCookieContainer.Keys.First();
            else
                NetStream.DefaultCookieContainer.Add(key, new System.Net.CookieContainer());

            foreach (var item in text)
            {
                var reg = Regex.Match(item, @"^(?<domain>.+?)\s+(?<flag>(TRUE|FALSE))\s+(?<path>.+?)\s+(?<secure>(TRUE|FALSE))\s+(?<expiration>\d+)\s+(?<name>.+?)\s+(?<value>.+?)$");
                if (reg.Success)
                {
                    var domain = reg.Result("${domain}");
                    var flag = reg.Result("${flag}") == "TRUE";
                    var path = reg.Result("${path}");
                    var secure = reg.Result("${secure}") == "TRUE";
                    var expiration = long.Parse(reg.Result("${expiration}"));
                    var name = reg.Result("${name}");
                    var value = reg.Result("${value}");

                    var cookie = new System.Net.Cookie(name, value, path, domain);
                    cookie.Secure = secure;

                    NetStream.DefaultCookieContainer[key].Add(cookie);
                }
            }

            var valid_result = NetStream.DefaultCookieContainer[key].GetCookies(new Uri("https://passport.baidu.com/"));
            if (valid_result.Count == 0)
            {
                Console.WriteLine("文件数据未含有登陆信息");
                Environment.Exit(0);
            }
        }
        private static void _print_no_arg()
        {
            Console.WriteLine("输入 -h 或者 --help 获取更多帮助");
        }
        private static void _print_help()
        {
            _print_no_arg();
            Console.WriteLine();
            Console.WriteLine("使用命令行:");
            Console.WriteLine("BaiduCloudConsole --[函数名] 参数");
            //Console.WriteLine();
            //Console.WriteLine("*** 账号相关 ***");
            //Console.WriteLine("--add-account --username [用户名] --password [密码]");
            //Console.WriteLine("\t添加指定的百度账号/密码，使用OAuth登陆");
            //Console.WriteLine("--add-account --cookie [cookie.txt]");
            //Console.WriteLine("\t添加指定的百度账号，使用cookie的文件格式为Netscape cookie.txt，兼容curl和wget的");
            //Console.WriteLine("--list-account");
            //Console.WriteLine("\t列出所有已保存的账号");
            //Console.WriteLine("--delete-account --username [用户名]");
            //Console.WriteLine("\t删除指定用户名的登陆信息");
            Console.WriteLine("*** 文件操作 ***");
            Console.WriteLine("-L | --list [网盘文件路径] [--order [排序:name|size|time] --page [页数] --count [每页显示数量] --desc]");
            Console.WriteLine("\t列出文件夹下所有文件");
            Console.WriteLine();
            Console.WriteLine("*** 文件传输 ***");
            Console.WriteLine("-D | --download [网盘文件路径] [本地文件路径] [--threads [下载线程数] --speed [限速(KB/s)]]");
            Console.WriteLine("\t下载文件，可选选项：下载线程数（默认96），限速（默认0，即无限速）");
            Console.WriteLine("-U | --upload [本地文件路径] [网盘文件路径] [--threads [上传线程数] --speed [限速(KB/s)] --encrypt]");
            Console.WriteLine("\t上传文件，可选选项：上传线程数（默认4），限速（默认0，即无限速），有encrypt选项开启文件加密，密钥见加密部分");
            Console.WriteLine();
            Console.WriteLine("*** 加密部分 ***");
            Console.WriteLine("--show-key");
            Console.WriteLine("\t输出目前的密钥信息");
            Console.WriteLine("--load-key [文件路径] [[-F | --force]]");
            Console.WriteLine("\t从指定的位置读取密钥到程序中，如密钥已存在，需要force进行强制覆盖");
            Console.WriteLine("--save-key [文件路径]");
            Console.WriteLine("\t输出当前密钥到指定位置中");
            Console.WriteLine("--create-key [[密钥类型: rsa|aes]] [-F | --force]");
            Console.WriteLine("\t生成密钥，如密钥已存在，需要force进行强制覆盖，如不指定密钥类型，默认为rsa");
            Console.WriteLine("--delete-key [密钥类型: rsa|aes]");
            Console.WriteLine();
        }
        private static void _exec_download(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Console.WriteLine("参数不足");
                return;
            }

            var remote_path = cmd[1];
            var local_path = cmd[2];

            var max_thread = Downloader.DEFAULT_MAX_THREAD;
            var speed_limit = 0;

            if (cmd.Length > 3)
            {
                int index = 3;
                while (index < cmd.Length)
                    switch (cmd[index])
                    {
                        case "--threads":
                            max_thread = int.Parse(cmd[index + 1]);
                            index += 2;
                            break;
                        case "--speed":
                            speed_limit = (int)(double.Parse(cmd[index + 1]) * 1024);
                            index += 2;
                            break;

                        default:
                            Console.WriteLine("未知参数：" + cmd[index]);
                            return;
                    }
            }

            var downloader = new Downloader()
        }
        private static void _exec_upload(string[] cmd)
        {

        }
        private static void _exec_list(string[] cmd)
        {

        }
        private static void _exec_show_key(string[] cmd)
        {

        }
        private static void _exec_load_key(string[] cmd)
        {

        }
        private static void _exec_save_key(string[] cmd)
        {

        }
        private static void _exec_create_key(string[] cmd)
        {

        }
        private static void _exec_delete_key(string[] cmd)
        {

        }
        private static void _exec_command(string[] cmd)
        {
            switch (cmd[0])
            {
                case "-L":
                case "--list":
                    _exec_list(cmd);
                    break;
                case "-D":
                case "--download":
                    _exec_download(cmd);
                    break;
                case "-U":
                case "--upload":
                    _exec_upload(cmd);
                    break;
                case "--show-key":
                    _exec_show_key(cmd);
                    break;
                case "--load-key":
                    _exec_load_key(cmd);
                    break;
                case "--save-key":
                    _exec_save_key(cmd);
                    break;
                case "--create-key":
                    _exec_create_key(cmd);
                    break;
                case "--delete-key":
                    _exec_delete_key(cmd);
                    break;
                default:
                    Console.WriteLine("无效的指令：" + cmd[0]);
                    Environment.Exit(0);
                    break;
            }
        }
    }
}
