﻿using System;
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

        private static string _version = "1.0.2 pre-alpha";
        private static void _debug_function()
        {
            //KEEP IT EMPTY if you are to build a release executable file!
        }
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

            try
            {
                if (args.Length == 0)
                    _print_no_arg();
                else
                {
                    _check_and_login();
                    _debug_function();
                    _exec_command(args);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Tracer.GlobalTracer.TraceError(ex);
            }

            if (!Directory.Exists("data"))
                Directory.CreateDirectory("data");
            NetStream.SaveCookie("data/cookie.dat");
            _key_manager.SaveKey("data/rsa_key.pem", true);
            _key_manager.SaveKey("data/aes_key.dat", false);
            _local_file_cacher?.Dispose();
            _remote_file_cacher?.Dispose();
            //Console.ReadKey();
        }
        //检验登陆状态，如未登陆则登陆
        private static void _check_and_login()
        {
            _remote_file_cacher = new RemoteFileCacher();
            _local_file_cacher = new LocalFileCacher();
            if (_remote_file_cacher.GetAllAccounts().Length == 0 && NetStream.DefaultCookieContainer.Keys.Count != 0)
                NetStream.DefaultCookieContainer = new Dictionary<string, System.Net.CookieContainer>();
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
                _remote_file_cacher.AddAccount(new BaiduPCS(oauth));
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
            Console.WriteLine("BaiduCloudSync 控制台模式");
            Console.WriteLine("Version: {0}", _version);
            Console.WriteLine("");
            Console.WriteLine("输入 -H 或者 --help 获取更多帮助");
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
            Console.WriteLine("-l | --list [网盘文件路径|FS ID] [--order [排序:name|size|time] --page [页数] --count [每页显示数量] --desc]");
            Console.WriteLine("\t列出文件夹下所有文件");
            Console.WriteLine("\t\t必要：网盘的文件路径(以/开头)或者文件的fs id");
            Console.WriteLine("\t\torder：可选，文件的排序顺序，name为按名称排序，size为按文件大小，time为按修改时间");
            Console.WriteLine("\t\tpage：可选，当前页数，从1开始计数");
            Console.WriteLine("\t\tcount：可选，每页显示的结果数");
            Console.WriteLine("\t\tdesc：可选，带有该参数后以降序排序，默认为升序排序");
            Console.WriteLine("-x | --delete [网盘文件路径|FS ID] [[网盘文件路径|FS ID] ...]");
            Console.WriteLine("\t删除指定路径的文件或文件夹(可指定多个文件路径/fs id)");
            Console.WriteLine("-m | --move [源路径|FS ID] [新路径|FS ID] [[--overwrite]]");
            Console.WriteLine("\t移动文件到新路径，默认不会覆盖新路径下的同名文件");
            Console.WriteLine("-c | --copy [源路径|FS ID] [新路径|FS ID] [[--overwrite]]");
            Console.WriteLine("\t复制文件到新路径，默认不会覆盖新路径下的同名文件");
            Console.WriteLine("-r | --rename [源路径|FS ID] [新文件名(不含路径)]");
            Console.WriteLine("\t将指定路径下的文件重命名");
            Console.WriteLine("-s | --search [搜索路径|FS ID] [文件名] [[-re|--regex] [-r|--recursion]]");
            Console.WriteLine("\t在指定路径下搜索文件，-re开启正则匹配，-r开启文件夹递归搜索");
            Console.WriteLine();
            Console.WriteLine("*** 文件链接 ***");
            Console.WriteLine("--to-symbollink [网盘文件路径|FS ID] [[文件链接路径]]");
            Console.WriteLine("\t将文件转换为文件链接，可选参数：文件链接路径（默认在文件名后加.symbollink）");
            Console.WriteLine("\t暂不支持文件夹直接转换");
            Console.WriteLine("--from-symbollink [文件链接路径|FS ID] [[网盘文件路径]]");
            Console.WriteLine("\t将文件链接转换为文件，可选参数：文件路径（默认去掉文件的.symbollink结尾）");
            Console.WriteLine("\t暂不支持文件夹直接转换");
            Console.WriteLine(); 
            Console.WriteLine("*** 文件传输 ***");
            Console.WriteLine("-d | --download [网盘文件路径|FS ID] [本地文件路径] [--threads [下载线程数] --speed [限速(KB/s)]]");
            Console.WriteLine("\t下载文件");
            Console.WriteLine("\t\t必要：网盘的文件路径(以/开头)或者文件/文件夹的fs id");
            Console.WriteLine("\t\t必要：本地文件的路径，包含文件名的绝对/相对路径");
            Console.WriteLine("\t\tthreads：可选，并行下载的线程数，被限速后平均10KB/s/线程，可根据实际网络上限进行调整");
            Console.WriteLine("\t\tspeed：可选，下载限速，单位为KB/s");
            Console.WriteLine("-u | --upload [本地文件路径] [网盘文件路径] [--threads [上传线程数] --speed [限速(KB/s)] --overwrite --encrypt [rsa|aes] --tasks [并行任务数]]");
            Console.WriteLine("\t上传文件");
            Console.WriteLine("\t\t必要：本地文件的路径，包含文件名的绝对/相对路径");
            Console.WriteLine("\t\t必要：网盘的文件路径（以/开头）");
            Console.WriteLine("\t\tthreads：可选，并行上传的线程数，默认为4，不建议修改，值太大可能会造成大量的超时重传");
            Console.WriteLine("\t\tspeed：可选，单个上传文件的上传限速，单位为KB/s");
            Console.WriteLine("\t\toverwrite：可选，开启该选项后会覆盖同名文件");
            Console.WriteLine("\t\tencrypt：可选，文件全局加密，加密后网盘文件会以.bcsd拓展名结束\r\n\t\t注：解密只能使用本软件或者自己按照文件格式进行解密");
            Console.WriteLine("\t\ttasks：可选，并行上传的文件数，默认5");
            Console.WriteLine();
            Console.WriteLine("*** 加密部分 ***");
            Console.WriteLine("--show-key");
            Console.WriteLine("\t输出目前的密钥信息");
            Console.WriteLine("--load-key [文件路径] [[-F | --force]]");
            Console.WriteLine("\t从指定的位置读取密钥到程序中，如密钥已存在，需要force进行强制覆盖，注：RSA密钥文件后缀必须为pem");
            Console.WriteLine("--save-key [文件路径]");
            Console.WriteLine("\t输出当前密钥到指定位置中\r\n\t注：RSA密钥的文件后缀为pem，如非pem，则会自动添加后缀");
            Console.WriteLine("--create-key [[密钥类型: rsa|aes]] [-F | --force]");
            Console.WriteLine("\t生成密钥，如密钥已存在，需要force进行强制覆盖，如不指定密钥类型，默认为rsa");
            Console.WriteLine("--delete-key [密钥类型: rsa|aes]");
            Console.WriteLine("\t删除密钥，利用该密钥加密的所有文件将会无法解密");
            Console.WriteLine();
        }
        private static void _scan_remote_dir(string local_path, string remote_path, out List<string> local_files, out List<ObjectMetadata> remote_files, bool recursion = true)
        {
            var rst_event = new ManualResetEventSlim();
            var parent_dir_path = remote_path.Substring(0, remote_path.LastIndexOf('/'));
            if (string.IsNullOrEmpty(parent_dir_path)) parent_dir_path = "/"; //根目录修正
            var file_list = new List<ObjectMetadata>();
            _remote_file_cacher.GetFileListAsync(parent_dir_path, _file_list_callback, state: new _temp_callback_state { reset = rst_event, list = file_list, page = 1, path = parent_dir_path });

            rst_event.Wait();
            var remote_file_info = file_list.Find(o => o.Path == remote_path);

            local_files = new List<string>();
            remote_files = new List<ObjectMetadata>();
            if (remote_file_info.FS_ID == 0)
                return;
            if (remote_file_info.IsDir)
                _scan_remote_dir_internal(local_path, remote_path, ref local_files, ref remote_files, recursion);
            else
            {
                local_files.Add(local_path);
                remote_files.Add(remote_file_info);
            }
        }
        private static void _scan_remote_dir_internal(string local_path, string remote_path, ref List<string> local_files, ref List<ObjectMetadata> remote_files, bool recursion = true)
        {
            var rst_event = new ManualResetEventSlim();
            var file_list = new List<ObjectMetadata>();
            _remote_file_cacher.GetFileListAsync(remote_path, _file_list_callback, state: new _temp_callback_state { reset = rst_event, list = file_list, page = 1, path = remote_path });
            rst_event.Wait();

            for (int i = 0; i < file_list.Count; i++)
            {
                if (file_list[i].IsDir)
                {
                    if (recursion)
                    {
                        _scan_remote_dir_internal(local_path + "/" + file_list[i].ServerFileName, remote_path + "/" + file_list[i].ServerFileName, ref local_files, ref remote_files, recursion);
                    }
                }
                else
                {
                    local_files.Add(local_path + "/" + file_list[i].ServerFileName);
                    remote_files.Add(file_list[i]);
                }
            }
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

            #region inputs
            if (cmd.Length > 3)
            {
                int index = 3;
                while (index < cmd.Length)
                    switch (cmd[index])
                    {
                        case "--threads":
                            if (index + 1 >= cmd.Length)
                            {
                                Console.WriteLine("参数不足");
                                return;
                            }
                            if (int.TryParse(cmd[index + 1], out max_thread) == false)
                            {
                                Console.WriteLine("线程数 {0} 无法转换为整型", cmd[index + 1]);
                                return;
                            }
                            index += 2;
                            break;
                        case "--speed":
                            if (index + 1 >= cmd.Length)
                            {
                                Console.WriteLine("参数不足");
                                return;
                            }
                            double temp_speed;
                            if (double.TryParse(cmd[index + 1], out temp_speed) == false)
                            {
                                Console.WriteLine("限速 {0} 无法转换为浮点数", cmd[index + 1]);
                                return;
                            }
                            speed_limit = (int)(temp_speed * 1024);
                            index += 2;
                            break;

                        default:
                            Console.WriteLine("未知参数：" + cmd[index]);
                            return;
                    }
            }
            #endregion

            var query_fsid_finish = new ManualResetEventSlim();
            long fs_id;
            bool query_success = false;
            if (long.TryParse(remote_path, out fs_id))
            {
                Console.WriteLine("查询文件夹路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    remote_path = data.Path;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                query_fsid_finish.Reset();
                if (!query_success)
                {
                    Console.WriteLine("查询文件夹失败，请查看该fs id是否存在");
                }
            }

            Console.WriteLine("读取文件信息……");
            List<string> local_files;
            List<ObjectMetadata> remote_files;
            _scan_remote_dir(local_path, remote_path, out local_files, out remote_files, true);

            if (remote_files.Count == 0)
            {
                Console.WriteLine("找不到该文件");
                return;
            }

            //开始下载
            for (int i = 0; i < remote_files.Count; i++)
            {
                if (remote_files.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("下载队列：[{0}/{1}] {3} -> {2}:", i + 1, local_files.Count, local_files[i], remote_files[i]);
                }

                var downloader = new Downloader(_remote_file_cacher, remote_files[i], local_files[i], max_thread, speed_limit, _key_manager);
                downloader.Start();
                downloader.TaskError += delegate
                {
                    Console.WriteLine();
                    Console.WriteLine("下载发生错误！");
                };
                bool decrypt_started = false, decrypt_response = false;
                downloader.DecryptStarted += delegate
                {
                    decrypt_started = true;
                };
                bool prealloc_finished = false, prealloc_response = false;
                downloader.PreAllocBlockFinished += delegate
                {
                    prealloc_finished = true;
                };

                Console.WriteLine("预分配硬盘空间……");
                Downloader.State stat;
                do
                {
                    stat = downloader.TaskState;
                    long finished_size = downloader.DownloadedSize;
                    long total_size = downloader.Size;

                    if (prealloc_finished && !decrypt_started)
                        _proceed_status_bar(finished_size, total_size, (long)downloader.AverageSpeed5s);
                    else
                        _proceed_status_bar(finished_size, total_size);

                    if (!prealloc_response && prealloc_finished)
                    {
                        prealloc_response = true;
                        _proceed_status_bar(total_size, total_size);

                        Console.WriteLine();
                        Console.WriteLine("下载开始……");
                    }
                    else if (!decrypt_response && decrypt_started)
                    {
                        decrypt_response = true;
                        _proceed_status_bar(total_size, total_size);
                        Console.WriteLine();
                        Console.WriteLine("文件解密开始……");
                    }
                    Thread.Sleep(100);
                } while ((stat & Downloader.State.FINISHED) == 0 && (stat & Downloader.State.ERROR) == 0);

                if (downloader.TaskState == Downloader.State.FINISHED)
                {
                    Console.WriteLine();
                    Console.WriteLine("下载完成");
                }
            }
        }
        private static string _format_bytes(long b, int precision = 1)
        {
            var fp = new string('0', precision);
            if (b < 0x400)
                return b.ToString() + "B";
            else if (b < 0x100000)
                return ((double)b / 0x400).ToString("0." + fp) + "KB";
            else if (b < 0x40000000)
                return ((double)b / 0x100000).ToString("0." + fp) + "MB";
            else if (b < 0x10000000000)
                return ((double)b / 0x40000000).ToString("0." + fp) + "GB";
            else
                return ((double)b / 0x10000000000).ToString("0." + fp) + "TB";
        }
        private struct _temp_callback_state
        {
            public ManualResetEventSlim reset;
            public List<ObjectMetadata> list;
            public int page;
            public string path;
        }
        private static void _file_list_callback(bool suc, ObjectMetadata[] data, object state)
        {
            var stat = (_temp_callback_state)state;
            var rst_event = stat.reset;
            var file_list = stat.list;
            var page = stat.page;
            var path = stat.path;

            if (!suc)
            {
                rst_event.Set();
                return;
            }

            file_list.AddRange(data);
            if (data.Length == 1000)
            {
                stat.page++;
                _remote_file_cacher.GetFileListAsync(path, _file_list_callback, page: page + 1, state: stat);
            }
            else
            {
                rst_event.Set();
            }
        }

        private static void _scan_dir(string local_path, string remote_path, out List<string> local_files, out List<string> remote_files, bool recursion = true)
        {
            local_files = new List<string>();
            remote_files = new List<string>();

            var dir_info = new DirectoryInfo(local_path);

            var files = dir_info.GetFiles();
            var dirs = dir_info.GetDirectories();

            foreach (var item in files)
            {
                if (!item.Name.EndsWith(".encrypted"))
                {
                    local_files.Add(item.FullName);
                    remote_files.Add(remote_path + "/" + item.Name);
                }
            }

            if (recursion)
            {
                foreach (var item in dirs)
                {
                    List<string> temp_local, temp_remote;
                    _scan_dir(local_path + "/" + item.Name, remote_path + "/" + item.Name, out temp_local, out temp_remote, recursion);
                    local_files.AddRange(temp_local);
                    remote_files.AddRange(temp_remote);
                }
            }
        }
        private static void _exec_upload(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Console.WriteLine("参数不足");
                return;
            }
            var local_path = cmd[1];
            var remote_path = cmd[2];
            var max_thread = Uploader.DEFAULT_MAX_THREAD;
            var speed_limit = 0;
            var encrypt_str = "";
            var overwrite = false;
            var parallel_task = 3;
            #region inputs
            if (cmd.Length > 3)
            {
                var index = 3;
                while (index < cmd.Length)
                {
                    switch (cmd[index])
                    {
                        case "--threads":
                            if (index + 1 >= cmd.Length)
                            {
                                Console.WriteLine("参数不足");
                                return;
                            }
                            if (int.TryParse(cmd[index + 1], out max_thread) == false)
                            {
                                Console.WriteLine("线程数 {0} 无法转换为整型", cmd[index + 1]);
                                return;
                            }
                            index += 2;
                            break;
                        case "--speed":
                            if (index + 1 >= cmd.Length)
                            {
                                Console.WriteLine("参数不足");
                                return;
                            }
                            double temp_speed;
                            if (double.TryParse(cmd[index + 1], out temp_speed) == false)
                            {
                                Console.WriteLine("限速 {0} 无法转换为浮点数", cmd[index + 1]);
                                return;
                            }
                            speed_limit = (int)(temp_speed * 1024);
                            index += 2;
                            break;
                        case "--encrypt":
                            if (index + 1 >= cmd.Length)
                            {
                                Console.WriteLine("参数不足");
                                return;
                            }
                            encrypt_str = cmd[index + 1];
                            if (encrypt_str != "aes" && encrypt_str != "rsa")
                            {
                                Console.WriteLine("无效的加密类型");
                                return;
                            }
                            index += 2;
                            break;
                        case "--overwrite":
                            overwrite = true;
                            index++;
                            break;
                        case "--tasks":
                            if (index + 1 >= cmd.Length)
                            {
                                Console.WriteLine("参数不足");
                                return;
                            }
                            if (int.TryParse(cmd[index + 1], out parallel_task) == false)
                            {
                                Console.WriteLine("并行任务数 {0} 无法转换为整型", cmd[index + 1]);
                                return;
                            }
                            if (parallel_task < 1)
                            {
                                Console.WriteLine("任务数必须要大于0");
                                return;
                            }
                            index += 2;
                            break;
                        default:
                            Console.WriteLine("未知参数：{0}", cmd[index]);
                            return;
                    }
                }
            }
            #endregion

            bool is_file = File.Exists(local_path), is_dir = Directory.Exists(local_path);
            if (is_file == false && is_dir == false)
            {
                Console.WriteLine("输入路径不存在");
                return;
            }

            List<string> local_files = new List<string>(), remote_files = new List<string>();
            if (is_dir)
            {
                if (remote_path.EndsWith("/") && remote_path != "/")
                    remote_path = remote_path.Substring(0, remote_path.Length - 1);
                _scan_dir(local_path, remote_path, out local_files, out remote_files, true);
            }
            else
            {
                local_files.Add(local_path);
                remote_files.Add(remote_path);
            }

            if (encrypt_str == "rsa")
                _key_manager.IsDynamicEncryption = true;
            else
                _key_manager.IsStaticEncryption = true;


            bool output_state = false;
            var output_state_lck = new object();

            var total_count = remote_files.Count;
            var fin_count = 0;
            var alloc_count = 0;
            var uploaders = new List<Uploader>();
            var lck = new object();

            var base_y = Console.CursorTop;
            var last_y = base_y;
            int loop_count = 0;
            while (fin_count < total_count)
            {
                loop_count++;
                for (int i = uploaders.Count; i < parallel_task && alloc_count < remote_files.Count; i++)
                {
                    var uploader = new Uploader(_local_file_cacher, _remote_file_cacher, local_files[alloc_count], remote_files[alloc_count],
                        0, overwrite, max_thread, speed_limit, _key_manager, !string.IsNullOrEmpty(encrypt_str));
                    uploader.Tag = alloc_count;
                    alloc_count++;
                    lock (lck)
                        uploaders.Add(uploader);
                    uploader.TaskError += (s, e) => { lock (lck) uploaders.Remove((Uploader)s); Interlocked.Increment(ref fin_count); };
                    uploader.TaskFinished += (s, e) => { lock (lck) uploaders.Remove((Uploader)s); Interlocked.Increment(ref fin_count); };

                    uploader.Start();
                }

                #region output
                if (loop_count == 10)
                {
                    loop_count = 0;
                    var fill_str = new string(' ', Console.WindowWidth);
                    lock (output_state_lck)
                    {
                        if (!output_state)
                        {
                            output_state = true;
                            base_y = Console.CursorTop;
                        }

                        Console.SetCursorPosition(0, base_y);
                        for (int i = base_y; i <= last_y + 1; i++)
                        {
                            Console.Write(fill_str);
                        }
                        Console.SetCursorPosition(0, base_y);
                        Console.WriteLine("[{0}/{1}]", fin_count, total_count);
                        lock (lck)
                        {
                            for (int i = 0; i < uploaders.Count; i++)
                            {
                                var item = uploaders[i];
                                var progress = (item.Size == 0 ? 0.0 : (item.UploadedSize * 100.0 / item.Size));
                                Console.WriteLine("#{0,-5} | {1,5}% | {2,-20} | {3,-10} | {5,-10} | {4}", item.Tag, progress.ToString("##0.0"), _format_bytes(item.UploadedSize) + "/" + _format_bytes(item.Size), _format_bytes((long)item.AverageSpeed5s) + "/s", item.RemoteFilePath, item.TaskState.ToString());

                            }
                        }
                        last_y = Console.CursorTop;
                    }
                    #endregion
                }
                Thread.Sleep(100);
            }
        }
        private static void _proceed_status_bar(long current, long total, long? speed = null)
        {
            double rate = ((total == 0) ? 0.0 : (100.0 * current / total));

            //进度条长度
            var bar_length = Math.Max(0, Console.WindowWidth - 45);
            var bar = new string('.', bar_length);
            var f_finished_bar = rate / 100 * bar_length;
            int i_finished_bar = (int)Math.Floor(f_finished_bar);
            var finished_bar = new string('=', i_finished_bar);
            if (bar_length != i_finished_bar)
                bar = finished_bar + ">" + bar.Substring(0, bar_length - i_finished_bar - 1);
            else
                bar = finished_bar;

            var size_info = string.Format("{0,-17}", _format_bytes(current) + "/" + _format_bytes(total));

            var speed_info = new string(' ', 10);
            if (speed != null)
                speed_info = string.Format("{0,-10}", _format_bytes((long)speed) + "/s");

            Console.Write("\r[" + string.Format("{0,5}", rate.ToString("0.0")) + "%] [" + bar + "] " + size_info + " " + speed_info);
        }
        private static void _stat_file_count(string path, out int files, out int dirs, out long size, bool recursion = true)
        {
            var rst_event = new ManualResetEventSlim();
            var file_list = new List<ObjectMetadata>();
            _remote_file_cacher.GetFileListAsync(path, _file_list_callback, state: new _temp_callback_state { reset = rst_event, list = file_list, page = 1, path = path });
            rst_event.Wait();
            dirs = 0;
            files = 0;
            size = 0;
            foreach (var item in file_list)
            {
                if (item.IsDir)
                {
                    dirs++;
                    if (recursion)
                    {
                        int temp_file, temp_dir;
                        long temp_size;
                        _stat_file_count(item.Path, out temp_file, out temp_dir, out temp_size, recursion);
                        dirs += temp_dir;
                        files += temp_file;
                        size += temp_size;
                    }
                }
                else
                {
                    files++;
                    size += (long)item.Size;
                }
            }
        }
        private static void _exec_list(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Console.WriteLine("参数不足");
                return;
            }
            var path = cmd[1];
            BaiduPCS.FileOrder order = BaiduPCS.FileOrder.name;
            var page = 1;
            var count = 200;
            bool asc = true;
            if (cmd.Length > 2)
            {
                int index = 2;
                while (index < cmd.Length)
                {
                    switch (cmd[index])
                    {
                        case "--order":
                            if (cmd[index + 1] == "name")
                                order = BaiduPCS.FileOrder.name;
                            else if (cmd[index + 1] == "size")
                                order = BaiduPCS.FileOrder.size;
                            else if (cmd[index + 1] == "time")
                                order = BaiduPCS.FileOrder.time;
                            else
                            {
                                Console.WriteLine("无效的排序依据：" + cmd[index + 1]);
                                return;
                            }
                            index += 2;
                            break;
                        case "--page":
                            page = int.Parse(cmd[index + 1]);
                            index += 2;
                            break;
                        case "--count":
                            count = int.Parse(cmd[index + 1]);
                            index += 2;
                            break;
                        case "--desc":
                            asc = false;
                            break;
                        default:
                            Console.WriteLine("无效的参数：" + cmd[index]);
                            return;
                    }
                }
            }

            long fs_id;
            if (long.TryParse(path, out fs_id))
            {
                Console.WriteLine("根据FS ID查询路径中");
                var fs_id_finish = new ManualResetEventSlim();
                var query_success = false;
                var is_file = true;
                _remote_file_cacher.QueryFileByFsID(fs_id, (suc, data, state) =>
                {
                    query_success = suc;
                    path = data.Path;
                    fs_id_finish.Set();
                    is_file = !data.IsDir;
                });

                fs_id_finish.Wait();
                if (!query_success)
                {
                    Console.WriteLine("查询失败，请检查输入是否正确");
                    return;
                }
                if (is_file)
                {
                    Console.WriteLine("查询到该id为文件，路径为：{0}，请用其父级目录列出文件列表", path);
                    return;
                }

            }

            var rst_event = new ManualResetEventSlim();
            ObjectMetadata[] files = null;
            _remote_file_cacher.GetFileListAsync(path, (suc, data, state) =>
            {
                if (suc)
                {
                    files = data;
                }
                else
                {
                    Console.WriteLine("获取失败");
                }
                rst_event.Set();
            }, order: order, asc: asc, page: page, size: count);

            rst_event.Wait();
            if (files != null)
            {
                Console.WriteLine("{0} 的文件信息: ", path);
                var padding = new string(' ', Console.WindowWidth);
                //20,40,15,18,18
                var head = ("FS ID" + padding).Substring(0, 20) + ("文件名" + padding).Substring(0, 37) + ("大小" + padding).Substring(0, 13) + ("创建时间" + padding).Substring(0, 14) + ("修改时间" + padding).Substring(0, 14);
                Console.WriteLine(head);
                foreach (var file in files)
                {
                    var file_name = file.ServerFileName;
                    Console.Write("{0,-20}", file.FS_ID);

                    var test_cursor = new List<int>();
                    foreach (var item in file.ServerFileName)
                    {
                        Console.Write(item);
                        test_cursor.Add(Console.CursorLeft);
                        if (Console.CursorLeft >= 59) //20+40-1
                        {
                            int file_name_length = -1;
                            for (int i = test_cursor.Count - 1; i >= 0; i--)
                            {
                                if (test_cursor[i] >= 56) continue;
                                file_name_length = test_cursor[i];
                                break;
                            }
                            Console.CursorLeft = file_name_length;
                            Console.Write(new string(' ', 59 - Console.CursorLeft - 3));
                            Console.Write("...");
                        }
                    }
                    if (Console.CursorLeft <= 60)
                        Console.Write(new string(' ', 60 - Console.CursorLeft));

                    var size = string.Format("{0,-15}", file.IsDir ? "<DIR>" : _format_bytes((long)file.Size));
                    Console.Write(size);

                    var ctime = string.Format("{0,-18}", util.FromUnixTimestamp(file.ServerCTime).ToString("yyyy-MM-dd HH:mm"));
                    var mtime = string.Format("{0,-18}", util.FromUnixTimestamp(file.ServerMTime).ToString("yyyy-MM-dd HH:mm"));
                    Console.WriteLine(ctime + mtime);
                }

                Console.WriteLine();
                int file_count, dir_count, file_count_rec, dir_count_rec;
                long size_count, size_count_rec;
                _stat_file_count(path, out file_count, out dir_count, out size_count, false);
                _stat_file_count(path, out file_count_rec, out dir_count_rec, out size_count_rec, true);
                Console.WriteLine("文件统计：仅该目录：{0} 文件夹， {1} 文件， {2}", dir_count.ToString("#,##0"), file_count.ToString("#,##0"), _format_bytes(size_count));
                Console.WriteLine("        包括子目录：{0} 文件夹， {1} 文件， {2}", dir_count_rec.ToString("#,##0"), file_count_rec.ToString("#,##0"), _format_bytes(size_count_rec));
            }
        }
        private static void _exec_show_key(string[] cmd)
        {
            if (cmd.Length != 1)
            {
                Console.WriteLine("参数过多");
                return;
            }
            Console.WriteLine("当前的RSA密钥信息：");
            if (_key_manager.HasRsaKey)
            {
                var rsa = new System.Security.Cryptography.RSACryptoServiceProvider();
                rsa.ImportCspBlob(_key_manager.RSAPrivateKey);
                var bit_length = rsa.KeySize;
                Console.WriteLine("\t{0}位RSA密钥", bit_length);
                Console.WriteLine("\t密钥特征码：{0}", util.Hex(MD5.ComputeHash(_key_manager.RSAPrivateKey, 0, _key_manager.RSAPrivateKey.Length)));
            }
            else
            {
                Console.WriteLine("\t无RSA密钥信息");
            }
            Console.WriteLine();
            Console.WriteLine("当前的AES密钥信息：");
            if (_key_manager.HasAesKey)
            {
                Console.WriteLine("\tAES密钥：{0}", util.Hex(_key_manager.AESKey));
                Console.WriteLine("\tAES初始向量：{0}", util.Hex(_key_manager.AESIV));
            }
            else
            {
                Console.WriteLine("\t无AES密钥信息");
            }
        }
        private static void _exec_load_key(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Console.WriteLine("参数过少");
                return;
            }
            if (cmd.Length > 3)
            {
                Console.WriteLine("参数过多");
                return;
            }
            var path = cmd[1];
            if (!File.Exists(path))
            {
                Console.WriteLine("文件不存在");
                return;
            }
            bool enable_force = false;
            if (cmd.Length == 3)
                if (cmd[2] == "-F" || cmd[2] == "--force")
                    enable_force = true;
                else
                {
                    Console.WriteLine("无效参数 {0}", cmd[2]);
                    return;
                }

            if (path.EndsWith(".pem") && _key_manager.HasRsaKey)
            {
                if (enable_force)
                    _key_manager.LoadKey(path);
                else
                {
                    Console.WriteLine("RSA密钥已存在，如需覆盖请使用-F参数，或者调用--delete-key删除");
                    return;
                }
            }
            else if (_key_manager.HasAesKey)
            {
                if (enable_force)
                    _key_manager.LoadKey(path);
                else
                {
                    Console.WriteLine("AES密钥已存在，如需覆盖请使用-F参数，或者调用--delete-key删除");
                    return;
                }
            }
            else
                return;

            Console.WriteLine("已读取密钥文件 {0}", path);
        }
        private static void _exec_save_key(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Console.WriteLine("参数过少");
                return;
            }
            if (cmd.Length > 3)
            {
                Console.WriteLine("参数过多");
                return;
            }
            var path = cmd[1];
            if (path.EndsWith(".pem") && _key_manager.HasRsaKey)
                _key_manager.SaveKey(path, true);
            else if (_key_manager.HasAesKey)
                _key_manager.SaveKey(path, false);
            else
            {
                if (path.EndsWith(".pem"))
                    Console.WriteLine("无RSA密钥信息，保存失败");
                else
                    Console.WriteLine("无AES密钥信息，保存失败");
                return;
            }

            Console.WriteLine("文件已保存到 {0} 中", path);
        }
        private static void _exec_create_key(string[] cmd)
        {
            bool create_rsa = true;
            bool enable_force = false;
            bool has_key_specified = false;
            int index = 1;
            while (index < cmd.Length)
            {
                switch (cmd[index])
                {
                    case "-F":
                    case "--force":
                        enable_force = true;
                        index++;
                        break;
                    case "aes":
                        if (has_key_specified)
                        {
                            Console.WriteLine("多次指定密钥类型，操作无效");
                            return;
                        }
                        else
                        {
                            has_key_specified = true;
                            create_rsa = false;
                        }
                        index++;
                        break;
                    case "rsa":
                        if (has_key_specified)
                        {
                            Console.WriteLine("多次指定密钥类型，操作无效");
                            return;
                        }
                        else
                        {
                            has_key_specified = true;
                            create_rsa = true;
                        }
                        index++;
                        break;
                    default:
                        Console.WriteLine("参数无效：{0}", cmd[index]);
                        return;
                }
            }

            if (_key_manager.HasRsaKey && create_rsa)
                if (enable_force)
                    _key_manager.CreateKey(true);
                else
                {
                    Console.WriteLine("RSA密钥已存在，如需覆盖请使用-F参数，或者调用--delete-key删除");
                    return;
                }
            else if (_key_manager.HasAesKey && !create_rsa)
                if (enable_force)
                    _key_manager.CreateKey(false);
                else
                {
                    Console.WriteLine("AES密钥已存在，如需覆盖请使用-F参数，或者调用--delete-key删除");
                    return;
                }
            else
                _key_manager.CreateKey(create_rsa);

            if (create_rsa)
            {
                Console.WriteLine("已生成RSA密钥，密钥信息：");
                var rsa = new System.Security.Cryptography.RSACryptoServiceProvider();
                rsa.ImportCspBlob(_key_manager.RSAPrivateKey);
                var bit_length = rsa.KeySize;
                Console.WriteLine("\t{0}位RSA密钥", bit_length);
                Console.WriteLine("\t密钥特征码：{0}", util.Hex(MD5.ComputeHash(_key_manager.RSAPrivateKey, 0, _key_manager.RSAPrivateKey.Length)));
            }
            else
            {
                Console.WriteLine("已生成AES密钥，密钥信息：");
                Console.WriteLine("\tAES密钥：{0}", util.Hex(_key_manager.AESKey));
                Console.WriteLine("\tAES初始向量：{0}", util.Hex(_key_manager.AESIV));
            }
        }
        private static void _exec_delete_key(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Console.WriteLine("参数不足");
                return;
            }
            var key_type = cmd[1];
            if (cmd.Length > 3)
            {
                Console.WriteLine("参数过多");
                return;
            }
            if (key_type != "aes" && key_type != "rsa")
            {
                Console.WriteLine("无效的密钥类型：{0}", key_type);
            }
            bool delete_rsa = key_type == "rsa";

            if (delete_rsa)
            {
                if (_key_manager.HasRsaKey)
                {
                    _key_manager.DeleteKey(true);
                    if (File.Exists("data/rsa_key.pem"))
                        File.Delete("data/rsa_key.pem");
                    Console.WriteLine("删除RSA密钥成功");
                }
                else
                    Console.WriteLine("无RSA密钥，忽略删除操作");
            }
            else
            {
                if (_key_manager.HasAesKey)
                {
                    _key_manager.DeleteKey(false);
                    if (File.Exists("data/aes_key.dat"))
                        File.Delete("data/aes_key.dat");
                    Console.WriteLine("删除AES密钥成功");
                }
                else
                    Console.WriteLine("无AES密钥，忽略删除操作");
            }
        }
        private static void _exec_delete(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Console.WriteLine("参数不足");
                return;
            }
            var list_delete = new List<string>(cmd);
            list_delete.RemoveAt(0); //command
            bool printed_querying = false;
            var query_finish_event = new ManualResetEventSlim();
            for (int i = 0; i < list_delete.Count; i++)
            {
                long fs_id;
                if (long.TryParse(list_delete[i], out fs_id))
                {
                    query_finish_event.Reset();
                    if (!printed_querying)
                    {
                        Console.WriteLine("查询文件路径中");
                        printed_querying = true; //不重复打印此消息
                    }

                    var query_success = false;
                    _remote_file_cacher.QueryFileByFsID(fs_id, (suc, data, state) =>
                    {
                        query_success = suc;
                        list_delete[i] = data.Path;
                        query_finish_event.Set();
                    });
                    query_finish_event.Wait();

                    if (!query_success)
                    {
                        Console.WriteLine("查询ID {0}失败，请查看该FS ID是否存在", fs_id);
                        return;
                    }
                }
            }
            var rst_event = new ManualResetEventSlim();
            _remote_file_cacher.DeletePathAsync(list_delete, (suc, data, state) =>
            {
                rst_event.Set();
            });
            rst_event.Wait();
            Console.WriteLine("删除完成");
        }
        private static void _exec_to_symbollink(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Console.WriteLine("参数不足");
                return;
            }
            var src_path = cmd[1];
            string dst_path = null;
            if (cmd.Length == 3)
                dst_path = cmd[2];

            var query_fsid_finish = new ManualResetEventSlim();
            long fs_id;
            bool query_success = false;
            bool is_dir = true;
            if (long.TryParse(src_path, out fs_id))
            {
                Console.WriteLine("查询源文件的文件路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    src_path = data.Path;
                    is_dir = data.IsDir;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                query_fsid_finish.Reset();
                if (!query_success)
                {
                    Console.WriteLine("查询源文件失败，请查看该fs id是否存在");
                    return;
                }
                if (is_dir)
                {
                    Console.WriteLine("暂不支持文件夹批量转换");
                    return;
                }
            }
            var rst_event = new ManualResetEventSlim();
            bool suc = false;
            _remote_file_cacher.ConvertToSymbolLinkAsync(src_path, (s, data, state) =>
            {
                suc = s;
                rst_event.Set();
            }, dst_path);

            rst_event.Wait();
            if (suc)
                Console.WriteLine("转换成功，已保存到 " + (dst_path == null ? src_path + ".symbollink" : dst_path));
            else
                Console.WriteLine("转换失败，详细日志可见 global-trace.log");

        }
        private static void _exec_from_symbollink(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Console.WriteLine("参数不足");
                return;
            }
            var src_path = cmd[1];
            string dst_path = null;
            if (cmd.Length == 3)
                dst_path = cmd[2];

            var query_fsid_finish = new ManualResetEventSlim();
            long fs_id;
            bool query_success = false;
            bool is_dir = true;
            if (long.TryParse(src_path, out fs_id))
            {
                Console.WriteLine("查询源文件的文件路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    src_path = data.Path;
                    is_dir = data.IsDir;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                query_fsid_finish.Reset();
                if (!query_success)
                {
                    Console.WriteLine("查询源文件失败，请查看该fs id是否存在");
                    return;
                }
                if (is_dir)
                {
                    Console.WriteLine("暂不支持文件夹批量转换");
                    return;
                }
            }
            var rst_event = new ManualResetEventSlim();
            bool suc = false;
            _remote_file_cacher.ConvertFromSymbolLinkAsync(src_path, (s, data, state) =>
            {
                suc = s;
                rst_event.Set();
            }, dst_path);

            rst_event.Wait();
            if (suc)
                Console.WriteLine("转换成功，已保存到 " + (dst_path != null ? dst_path : (src_path.EndsWith(".symbollink") ? src_path.Substring(0, src_path.Length - 11) : (src_path + "." + src_path.Split('.').Last()))));
            else
                Console.WriteLine("转换失败，详细日志可见 global-trace.log");

        }

        private static void _exec_move(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Console.WriteLine("参数不足");
                return;
            }
            else if (cmd.Length > 4)
            {
                Console.WriteLine("参数过多");
                return;
            }

            var src = cmd[1];
            var dst = cmd[2];
            var query_fsid_finish = new ManualResetEventSlim();
            long fs_id;
            bool query_success = false;
            if (long.TryParse(src, out fs_id))
            {
                Console.WriteLine("查询源文件夹的文件路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    src = data.Path;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                query_fsid_finish.Reset();
                if (!query_success)
                {
                    Console.WriteLine("查询源文件夹失败，请查看该fs id是否存在");
                    return;
                }
            }
            if (long.TryParse(dst, out fs_id))
            {
                Console.WriteLine("查询目标文件夹的文件路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    dst = data.Path;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                if (!query_success)
                {
                    Console.WriteLine("查询目标文件夹失败，请查看该fs id是否存在");
                    return;
                }
            }
            var rst_event = new ManualResetEventSlim();
            bool suc = false;
            bool overwrite = false;
            if (cmd.Length == 4 && cmd[3] == "--overwrite")
                overwrite = true;
            _remote_file_cacher.MovePathAsync(src, dst, (s, data, state) =>
            {
                suc = s;
                rst_event.Set();
            }, ondup: overwrite ? BaiduPCS.ondup.overwrite : BaiduPCS.ondup.newcopy);

            rst_event.Wait();
            if (suc)
                Console.WriteLine("移动成功：{0} -> {1}", src, dst);
            else
                Console.WriteLine("移动失败：{0} -> {1}", src, dst);
        }
        private static void _exec_copy(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Console.WriteLine("参数不足");
                return;
            }
            else if (cmd.Length > 4)
            {
                Console.WriteLine("参数过多");
                return;
            }

            var src = cmd[1];
            var dst = cmd[2];

            var query_fsid_finish = new ManualResetEventSlim();
            long fs_id;
            bool query_success = false;
            if (long.TryParse(src, out fs_id))
            {
                Console.WriteLine("查询源文件夹的文件路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    src = data.Path;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                query_fsid_finish.Reset();
                if (!query_success)
                {
                    Console.WriteLine("查询源文件夹失败，请查看该fs id是否存在");
                    return;
                }
            }
            if (long.TryParse(dst, out fs_id))
            {
                Console.WriteLine("查询目标文件夹的文件路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    dst = data.Path;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                if (!query_success)
                {
                    Console.WriteLine("查询目标文件夹失败，请查看该fs id是否存在");
                    return;
                }
            }
            var rst_event = new ManualResetEventSlim();
            bool suc = false;
            bool overwrite = false;
            if (cmd.Length == 4 && cmd[3] == "--overwrite")
                overwrite = true;
            _remote_file_cacher.CopyPathAsync(src, dst, (s, data, state) =>
            {
                suc = s;
                rst_event.Set();
            }, ondup: overwrite ? BaiduPCS.ondup.overwrite : BaiduPCS.ondup.newcopy);

            rst_event.Wait();
            if (suc)
                Console.WriteLine("复制成功：{0} -> {1}", src, dst);
            else
                Console.WriteLine("复制失败：{0} -> {1}", src, dst);
        }

        private static void _exec_rename(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Console.WriteLine("参数不足");
                return;
            }
            else if (cmd.Length > 3)
            {
                Console.WriteLine("参数过多");
                return;
            }

            var src = cmd[1];
            var newname = cmd[2];
            var query_fsid_finish = new ManualResetEventSlim();
            long fs_id;
            bool query_success = false;
            if (long.TryParse(src, out fs_id))
            {
                Console.WriteLine("查询源文件夹的文件路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    src = data.Path;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                query_fsid_finish.Reset();
                if (!query_success)
                {
                    Console.WriteLine("查询源文件夹失败，请查看该fs id是否存在");
                    return;
                }
            }
            var rst_event = new ManualResetEventSlim();
            bool suc = false;
            _remote_file_cacher.RenameAsync(src, newname, (s, data, state) =>
            {
                suc = s;
                rst_event.Set();
            });

            rst_event.Wait();
            if (suc)
                Console.WriteLine("重命名成功：{0} -> {1}", src, newname);
            else
                Console.WriteLine("重命名失败：{0} -> {1}", src, newname);
        }

        private static void _exec_search(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Console.WriteLine("参数不足");
                return;
            }

            var path = cmd[1];
            var keyword = cmd[2];

            var query_fsid_finish = new ManualResetEventSlim();
            long fs_id;
            bool query_success = false;
            bool is_file = true;
            if (long.TryParse(path, out fs_id))
            {
                Console.WriteLine("查询文件夹路径中...");
                _remote_file_cacher.QueryFileByFsID(fs_id, (s, data, state) =>
                {
                    query_success = true;
                    path = data.Path;
                    is_file = !data.IsDir;
                    query_fsid_finish.Set();
                });
                query_fsid_finish.Wait();
                query_fsid_finish.Reset();
                if (!query_success)
                {
                    Console.WriteLine("查询文件夹失败，请查看该fs id是否存在");
                    return;
                }
                if (is_file)
                {
                    Console.WriteLine("查询到该fs id为文件，无法进行搜索");
                    return;
                }
            }

            bool enable_regex = false, enable_recursion = false;
            int index = 3;
            for (int i = index; i < cmd.Length; i++)
            {
                switch (cmd[i])
                {
                    case "-re":
                    case "--regex":
                        enable_regex = true;
                        break;
                    case "-r":
                    case "--recursion":
                        enable_recursion = true;
                        break;

                    default:
                        Console.WriteLine("非法参数：" + cmd[i]);
                        return;
                }
            }

            var sync_lock = new ManualResetEventSlim();
            bool success = false;
            ObjectMetadata[] result = null;
            _remote_file_cacher.QueryFileListAsync(path, keyword, (suc, data, state) =>
            {
                success = suc;
                result = data;
                sync_lock.Set();
            }, enable_regex: enable_regex, recursion: enable_recursion);
            sync_lock.Wait();
            if (success)
            {
                var padding = new string(' ', Console.WindowWidth);

                //20,40,15,18,18
                var head = ("FS ID" + padding).Substring(0, 20) + ("文件名" + padding).Substring(0, 37) + ("大小" + padding).Substring(0, 13) + ("创建时间" + padding).Substring(0, 14) + ("修改时间" + padding).Substring(0, 14);
                Console.WriteLine(head);
                foreach (var file in result)
                {
                    var file_name = file.ServerFileName;
                    Console.Write("{0,-20}", file.FS_ID);

                    var test_cursor = new List<int>();
                    foreach (var item in file.ServerFileName)
                    {
                        Console.Write(item);
                        test_cursor.Add(Console.CursorLeft);
                        if (Console.CursorLeft >= 59) //20+40-1
                        {
                            int file_name_length = -1;
                            for (int i = test_cursor.Count - 1; i >= 0; i--)
                            {
                                if (test_cursor[i] >= 56) continue;
                                file_name_length = test_cursor[i];
                                break;
                            }
                            Console.CursorLeft = file_name_length;
                            Console.Write(new string(' ', 59 - Console.CursorLeft - 3));
                            Console.Write("...");
                        }
                    }
                    if (Console.CursorLeft <= 60)
                        Console.Write(new string(' ', 60 - Console.CursorLeft));

                    var size = string.Format("{0,-15}", file.IsDir ? "<DIR>" : _format_bytes((long)file.Size));
                    Console.Write(size);

                    var ctime = string.Format("{0,-18}", util.FromUnixTimestamp(file.ServerCTime).ToString("yyyy-MM-dd HH:mm"));
                    var mtime = string.Format("{0,-18}", util.FromUnixTimestamp(file.ServerMTime).ToString("yyyy-MM-dd HH:mm"));
                    Console.WriteLine(ctime + mtime);
                }
            }
            else
                Console.WriteLine("搜索失败");
        }
        private static void _exec_command(string[] cmd)
        {
            switch (cmd[0].ToLower())
            {
                case "-l":
                case "--list":
                    _exec_list(cmd);
                    break;
                case "-d":
                case "--download":
                    _exec_download(cmd);
                    break;
                case "-u":
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
                case "-h":
                case "--help":
                    _print_help();
                    break;
                case "-x":
                case "--delete":
                    _exec_delete(cmd);
                    break;
                case "--to-symbollink":
                    _exec_to_symbollink(cmd);
                    break;
                case "--from-symbollink":
                    _exec_from_symbollink(cmd);
                    break;
                case "-m":
                case "--move":
                    _exec_move(cmd);
                    break;
                case "-c":
                case "--copy":
                    _exec_copy(cmd);
                    break;
                case "-r":
                case "--rename":
                    _exec_rename(cmd);
                    break;
                case "-s":
                case "--search":
                    _exec_search(cmd);
                    break;
                case "--reset-cache":
                    _remote_file_cacher.ResetCache(0);
                    Console.WriteLine("cache reset.");
                    break;
                default:
                    Console.WriteLine("无效的指令：" + cmd[0]);
                    Environment.Exit(0);
                    break;
            }
        }
    }
}
