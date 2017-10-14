using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BaiduCloudSync
{
    class TestBench
    {

        public void TestPCS_API(BaiduPCS api)
        {
            var trace = Tracer.GlobalTracer;
            var sw = new Stopwatch();

            trace.TraceInfo("Testing PCS API...");
            trace.TraceInfo("[1/?] Creating Directory");
            sw.Start();

            var temp_dir = api.CreateDirectory("/pcsapi_testbench");
            sw.Stop();
            var time = sw.ElapsedMilliseconds;
            trace.TraceInfo("Finished: ");
        }
    }
}
