using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace BaiduCloudSync_Test.util.net_util
{
    [TestClass]
    public class HttpSessionTest
    {
        [TestMethod]
        public void HttpGetAsyncTest()
        {
            var wait_event = new System.Threading.ManualResetEventSlim();
            var test_obj = new GlobalUtil.http.HttpSession();
            test_obj.HttpGetAsync("https://www.baidu.com/", (s, e) =>
            {
                wait_event.Set();
            }, wait_event);
            wait_event.Wait();
            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.IsTrue(test_obj.HTTP_Response.StatusCode == System.Net.HttpStatusCode.OK);
            Assert.IsNotNull(test_obj.ResponseStream);
            Assert.IsFalse(string.IsNullOrEmpty(test_obj.ReadResponseString()));
            test_obj.Close();
            Assert.IsNull(test_obj.ReadResponseString());
        }

        [TestMethod]
        public void HttpGetSyncTest()
        {
            var test_obj = new GlobalUtil.http.HttpSession();
            test_obj.HttpGet("https://www.baidu.com/");
            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.IsTrue(test_obj.HTTP_Response.StatusCode == System.Net.HttpStatusCode.OK);
            Assert.IsNotNull(test_obj.ResponseStream);
            Assert.IsFalse(string.IsNullOrEmpty(test_obj.ReadResponseString()));
            test_obj.Close();
            Assert.IsNull(test_obj.ReadResponseString());
        }


        private string _gen_date_string()
        {
            string[] month_map = new string[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var dt = DateTime.UtcNow;
            var ret = dt.DayOfWeek.ToString().Substring(0, 3) + ", ";
            ret += string.Format("{0:D2} {1} {2:D4} ", dt.Day, month_map[dt.Month], dt.Year);
            ret += string.Format("{0:D2}:{1:D2}:{2:D2} GMT", dt.Hour, dt.Minute, dt.Second);
            return ret;
        }
        [TestMethod]
        public void HttpGetLocalTest()
        {
            var ready = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            bool fail = false;
            var thd = new Thread(new ThreadStart(delegate
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6666));
                socket.Listen(0);

                ready.Set();
                try
                {
                    var buffer = new byte[4096];
                    var established_socket = socket.Accept();
                    var count = established_socket.Receive(buffer);
                    if (count == 0) throw new ArgumentException();
                    var response = "HTTP/1.1 200 OJBK\r\n";
                    var header = "Connection: close\r\n" +
                        "Date: " + _gen_date_string() + "\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        "Content-Length: 10\r\n" +
                        "Server: TheFuckServer/1.0\r\n" +
                        "\r\n";
                    var body = "emmmmmmmmm";
                    var total_msg = response + header + body;
                    var send_msg = System.Text.Encoding.UTF8.GetBytes(total_msg);
                    established_socket.Send(send_msg);
                    established_socket.Close();
                }
                catch (Exception)
                {
                    fail = true;
                }
                finally
                {
                    finish.Set();
                    socket.Close();
                }
            }));
            thd.Start();
            ready.Wait();

            var test_obj = new GlobalUtil.http.HttpSession();
            test_obj.HttpGet("http://127.0.0.1:6666/");

            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.AreEqual("OJBK", test_obj.HTTP_Response.StatusDescription);
            Assert.IsFalse(fail);
            Assert.AreEqual(HttpStatusCode.OK, test_obj.HTTP_Response.StatusCode);
            Assert.AreEqual("emmmmmmmmm", test_obj.ReadResponseString());
            finish.Wait();
            test_obj.Close();
        }

        [TestMethod]
        public void HttpGetContinuousTest()
        {
            var test_obj = new GlobalUtil.http.HttpSession();
            for (int i = 0; i < 5; i++)
            {
                test_obj.HttpGet("https://www.baidu.com/");
                Assert.IsNotNull(test_obj.HTTP_Response);
                Assert.IsTrue(test_obj.HTTP_Response.StatusCode == System.Net.HttpStatusCode.OK);
                Assert.IsNotNull(test_obj.ResponseStream);
                Assert.IsFalse(string.IsNullOrEmpty(test_obj.ReadResponseString()));
                test_obj.Close();
                Assert.IsNull(test_obj.ReadResponseString());
            }
        }

        [TestMethod]
        public void HttpGetLocalProtocolErrorTest()
        {
            var ready = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            bool fail = false;
            var thd = new Thread(new ThreadStart(delegate
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6667));
                socket.Listen(0);

                ready.Set();
                try
                {
                    var buffer = new byte[4096];
                    var established_socket = socket.Accept();
                    var count = established_socket.Receive(buffer);
                    if (count == 0) throw new ArgumentException();
                    var response = "HTTP/1.1 404 Not Found\r\n";
                    var header = "Connection: close\r\n" +
                        "Date: " + _gen_date_string() + "\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        "Content-Length: 10\r\n" +
                        "Server: TheFuckServer/1.0\r\n" +
                        "\r\n";
                    var body = "emmmmmmmmm";
                    var total_msg = response + header + body;
                    var send_msg = System.Text.Encoding.UTF8.GetBytes(total_msg);
                    established_socket.Send(send_msg);
                    established_socket.Close();

                }
                catch (Exception)
                {
                    fail = true;
                }
                finally
                {
                    finish.Set();
                    socket.Close();
                }
            }));
            thd.Start();
            ready.Wait();

            var test_obj = new GlobalUtil.http.HttpSession(retry_times: 0);
            test_obj.HttpGet("http://127.0.0.1:6667/");

            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.IsFalse(fail);
            Assert.AreEqual(HttpStatusCode.NotFound, test_obj.HTTP_Response.StatusCode);
            Assert.AreEqual("emmmmmmmmm", test_obj.ReadResponseString());
            finish.Wait();
            test_obj.Close();
        }

        [TestMethod]
        [ExpectedException(typeof(GlobalUtil.http.StackedHttpException))]
        public void HttpGetLocalIOExceptionTest()
        {
            var ready = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            var thd = new Thread(new ThreadStart(delegate
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6668));
                socket.Listen(0);

                ready.Set();
                try
                {
                    var buffer = new byte[4096];
                    for (int i = 0; i < 6; i++)
                    {
                        var established_socket = socket.Accept();
                        GlobalUtil.Tracer.GlobalTracer.TraceInfo("connection estabilished: " + established_socket.RemoteEndPoint);
                        var count = established_socket.Receive(buffer);
                        if (count == 0) throw new ArgumentException();
                        established_socket.Send(System.Text.Encoding.UTF8.GetBytes("HTTP/1.1 2"));
                        established_socket.Close();
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    finish.Set();
                    socket.Close();
                }
            }));
            thd.Start();
            ready.Wait();

            var test_obj = new GlobalUtil.http.HttpSession(retry_times: 3);
            var p = new GlobalUtil.http.Parameters();
            p.Add("foo", "bar");
            test_obj.HttpGet("http://127.0.0.1:6668/", query: p);
            Assert.Fail();
        }

        [TestMethod]
        public void HttpGetLocalRangeTest()
        {
            var ready = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            bool fail = false;
            var thd = new Thread(new ThreadStart(delegate
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6669));
                socket.Listen(0);

                ready.Set();
                try
                {
                    var buffer = new byte[4096];
                    var established_socket = socket.Accept();
                    var count = established_socket.Receive(buffer);
                    if (count == 0) throw new ArgumentException();
                    var response = "HTTP/1.1 200 OJBK\r\n";
                    var header = "Connection: close\r\n" +
                        "Date: " + _gen_date_string() + "\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        "Content-Length: 5\r\n" +
                        "Server: TheFuckServer/1.0\r\n" +
                        "Content-Range: bytes 5-9/10\r\n" + 
                        "\r\n";
                    var body = "mmmmm";
                    var total_msg = response + header + body;
                    var send_msg = System.Text.Encoding.UTF8.GetBytes(total_msg);
                    established_socket.Send(send_msg);
                    established_socket.Close();
                }
                catch (Exception)
                {
                    fail = true;
                }
                finally
                {
                    finish.Set();
                    socket.Close();
                }
            }));
            thd.Start();
            ready.Wait();

            var test_obj = new GlobalUtil.http.HttpSession(timeout: 5000);
            test_obj.HttpGet("http://127.0.0.1:6669/", range: new GlobalUtil.http.Range(5));

            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.AreEqual("OJBK", test_obj.HTTP_Response.StatusDescription);
            Assert.IsFalse(fail);
            Assert.AreEqual(HttpStatusCode.OK, test_obj.HTTP_Response.StatusCode);
            Assert.AreEqual("mmmmm", test_obj.ReadResponseString());
            finish.Wait();
            test_obj.Close();
        }

        [TestMethod]
        public void HttpGetLocalRangeTest2()
        {

            var ready = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            bool fail = false;
            var thd = new Thread(new ThreadStart(delegate
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6669));
                socket.Listen(0);

                ready.Set();
                try
                {
                    var buffer = new byte[4096];
                    var established_socket = socket.Accept();
                    var count = established_socket.Receive(buffer);
                    if (count == 0) throw new ArgumentException();
                    var response = "HTTP/1.1 200 OJBK\r\n";
                    var header = "Connection: close\r\n" +
                        "Date: " + _gen_date_string() + "\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        "Content-Length: 5\r\n" +
                        "Server: TheFuckServer/1.0\r\n" +
                        "Content-Range: bytes 0-4/10\r\n" +
                        "\r\n";
                    var body = "emmmm";
                    var total_msg = response + header + body;
                    var send_msg = System.Text.Encoding.UTF8.GetBytes(total_msg);
                    established_socket.Send(send_msg);
                    established_socket.Close();
                }
                catch (Exception)
                {
                    fail = true;
                }
                finally
                {
                    finish.Set();
                    socket.Close();
                }
            }));
            thd.Start();
            ready.Wait();

            var test_obj = new GlobalUtil.http.HttpSession(timeout: 5000);
            test_obj.HttpGet("http://127.0.0.1:6669/", range: new GlobalUtil.http.Range(-5));

            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.AreEqual("OJBK", test_obj.HTTP_Response.StatusDescription);
            Assert.IsFalse(fail);
            Assert.AreEqual(HttpStatusCode.OK, test_obj.HTTP_Response.StatusCode);
            Assert.AreEqual("emmmm", test_obj.ReadResponseString());
            finish.Wait();
            test_obj.Close();
        }

        [TestMethod]
        public void HttpGetLocalRangeTest3()
        {
            var ready = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            bool fail = false;
            var thd = new Thread(new ThreadStart(delegate
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6670));
                socket.Listen(0);

                ready.Set();
                try
                {
                    var buffer = new byte[4096];
                    var established_socket = socket.Accept();
                    var count = established_socket.Receive(buffer);
                    if (count == 0) throw new ArgumentException();
                    var response = "HTTP/1.1 200 OJBK\r\n";
                    var header = "Connection: close\r\n" +
                        "Date: " + _gen_date_string() + "\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        "Content-Length: 7\r\n" +
                        "Server: TheFuckServer/1.0\r\n" +
                        "Content-Range: bytes 1-7/10\r\n" +
                        "\r\n";
                    var body = "emmmmmm";
                    var total_msg = response + header + body;
                    var send_msg = System.Text.Encoding.UTF8.GetBytes(total_msg);
                    established_socket.Send(send_msg);
                    established_socket.Close();
                }
                catch (Exception)
                {
                    fail = true;
                }
                finally
                {
                    finish.Set();
                    socket.Close();
                }
            }));
            thd.Start();
            ready.Wait();

            var test_obj = new GlobalUtil.http.HttpSession(timeout: 5000);
            test_obj.HttpGet("http://127.0.0.1:6670/", range: new GlobalUtil.http.Range(1, 7));

            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.AreEqual("OJBK", test_obj.HTTP_Response.StatusDescription);
            Assert.IsFalse(fail);
            Assert.AreEqual(HttpStatusCode.OK, test_obj.HTTP_Response.StatusCode);
            Assert.AreEqual("emmmmmm", test_obj.ReadResponseString());
            finish.Wait();
            test_obj.Close();
        }


        [TestMethod]
        public void HttpPostLocalTest()
        {
            var ready = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            bool fail = false;
            string str = null;
            var thd = new Thread(new ThreadStart(delegate
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6671));
                socket.Listen(0);

                ready.Set();
                try
                {
                    var buffer = new byte[4096];
                    var established_socket = socket.Accept();
                    var count = established_socket.Receive(buffer);
                    if (count == 0) throw new ArgumentException();
                    var response = "HTTP/1.1 200 OJBK\r\n";
                    var header = "Connection: close\r\n" +
                        "Date: " + _gen_date_string() + "\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        "Content-Length: 5\r\n" +
                        "Server: TheFuckServer/1.0\r\n" +
                        "\r\n";
                    var body = "emmmm";
                    var total_msg = response + header + body;
                    var send_msg = System.Text.Encoding.UTF8.GetBytes(total_msg);
                    count = established_socket.Receive(buffer);
                    established_socket.Send(send_msg);
                    str = System.Text.Encoding.UTF8.GetString(buffer, 0, count);
                    established_socket.Close();
                }
                catch (Exception)
                {
                    fail = true;
                }
                finally
                {
                    finish.Set();
                    socket.Close();
                }
            }));
            thd.Start();
            ready.Wait();

            var test_obj = new GlobalUtil.http.HttpSession(timeout: 5000, content_type: "application/x-www-form-urlencoded");
            var p = new GlobalUtil.http.Parameters();
            p.Add("foo", "bar");
            p.Add("a", "");
            test_obj.HttpPost("http://127.0.0.1:6671/", p);

            Assert.IsNotNull(test_obj.HTTP_Response);
            Assert.AreEqual("OJBK", test_obj.HTTP_Response.StatusDescription);
            Assert.IsFalse(fail);
            Assert.AreEqual(HttpStatusCode.OK, test_obj.HTTP_Response.StatusCode);
            Assert.AreEqual("emmmm", test_obj.ReadResponseString());
            finish.Wait();
            test_obj.Close();
            Assert.AreEqual("foo=bar&a=", str);
        }
    }
}
