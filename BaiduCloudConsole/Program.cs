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
        private static void Main(string[] args)
        {
            Tracer.GlobalTracer.TraceError(new BaiduCloudSync.oauth.NotLoggedInException());
            Tracer.GlobalTracer.TraceError(new BaiduCloudSync.oauth.NotLoggedInException("123"));

            Tracer.GlobalTracer.TraceError(new BaiduCloudSync.oauth.NotLoggedInException("123", new IOException()));
        }
    }
}
