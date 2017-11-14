using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudConsole
{
    class Program
    {
        private static string _version = "1.0.0 pre-alpha";
        static void Main(string[] args)
        {
            if (args.Length == 0)
                _print_no_arg();
            else
            {

            }
        }

        private static void _print_no_arg()
        {
            Console.WriteLine("BaiduCloudSync Console Mode");
            Console.WriteLine("Version: {0}", _version);
            Console.WriteLine("");
            Console.WriteLine("Enter -h or --help to get help");
        }
    }
}
