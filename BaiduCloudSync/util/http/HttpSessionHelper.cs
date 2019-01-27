using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace GlobalUtil.http
{
    internal sealed class HttpWebRequestHelper
    {
        public static void MergeParametersToWebRequestHeader(Parameters header, HttpWebRequest request)
        {
            if (header == null || request == null)
                return;
            var merge_attributes = new Dictionary<string, _prototype_reflect_assign>();
            merge_attributes.Add(HttpSession.STR_ACCEPT, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_CONNECTION, _connection_assign);
            merge_attributes.Add(HttpSession.STR_CONTENT_LENGTH, _parsable_reflect_assign<long>);
            merge_attributes.Add(HttpSession.STR_CONTENT_TYPE, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_EXPECT, _expect_assign);
            merge_attributes.Add(HttpSession.STR_DATE, _parsable_reflect_assign<DateTime>);
            merge_attributes.Add(HttpSession.STR_HOST, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_IF_MODIFIED_SINCE, _parsable_reflect_assign<DateTime>);
            merge_attributes.Add(HttpSession.STR_RANGE, _range_assign);
            merge_attributes.Add(HttpSession.STR_REFERER, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_TRANSFER_ENCODING, _transfer_encoding_assign);
            merge_attributes.Add(HttpSession.STR_USER_AGENT, _str_reflect_assign);
            foreach (var param in header)
            {
                if (merge_attributes.ContainsKey(param.Key))
                    merge_attributes[param.Key].Invoke(request, param.Key, param.Value);
                else
                    _default_assign(request, param.Key, param.Value);
            }
        }
        #region private methods for merge parameters

        private delegate void _prototype_reflect_assign(HttpWebRequest request, string key, string value);
        private static void _default_assign(HttpWebRequest request, string key, string value)
        {
            request.Headers.Add(key, value);
        }
        private static void _str_reflect_assign(HttpWebRequest request, string key, string value)
        {
            request.GetType().GetProperty(key.Replace("-", "")).SetValue(request, value, null);
        }
        private static void _parsable_reflect_assign<T>(HttpWebRequest request, string key, string value)
        {
            T result = (T)Convert.ChangeType(value, typeof(T));
            request.GetType().GetProperty(key.Replace("-", "")).SetValue(request, result, null);
        }
        private static void _connection_assign(HttpWebRequest request, string key, string value)
        {
            if (value.ToLower() == HttpSession.STR_CONNECTION_KEEP_ALIVE)
                request.KeepAlive = true;
            else if (value.ToLower() == HttpSession.STR_CONNECTION_CLOSE)
                request.KeepAlive = false;
            else
                throw new ArgumentException("Invalid Connection status");
        }
        private static void _expect_assign(HttpWebRequest request, string key, string value)
        {
            Tracer.GlobalTracer.TraceWarning("Changing header field 'Expect' will affect all connections in the same ServicePoint.");
            if (value.ToLower() == HttpSession.STR_100_CONTINUE)
                request.ServicePoint.Expect100Continue = true;
            else if (string.IsNullOrEmpty(value))
                request.ServicePoint.Expect100Continue = false;
            else
                throw new ArgumentException("Invalid Expect value");
        }
        private static void _range_assign(HttpWebRequest request, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var range = Range.Parse(value);
            if (range.From == null && range.To == null) return;
            if (range.To == null)
                request.AddRange(range.From.Value);
            else if (range.From == null)
                request.AddRange(-range.To.Value);
            else
                request.AddRange(range.From.Value, range.To.Value);
        }
        private static void _transfer_encoding_assign(HttpWebRequest request, string key, string value)
        {
            if (value.ToLower() == HttpSession.STR_CHUNKED)
                request.SendChunked = true;
            else if (string.IsNullOrEmpty(value))
                request.SendChunked = false;
            else
                throw new ArgumentException("Invalid Transfer-Encoding value");
        }
        #endregion

        /// <summary>
        /// 将一个数据流转换为可以seek的数据流
        /// （即源数据流可以seek时返回源数据流，不可seek时将源数据流读取到内存中并返回内存数据流）
        /// </summary>
        /// <param name="input_stream">源数据流</param>
        /// <param name="close_if_unused">在不可seek情况下，是否关闭源数据流</param>
        /// <returns></returns>
        public static Stream ConvertToSeekableStream(Stream input_stream, bool close_if_unused = true)
        {
            bool length_accessible = true;
            try
            {
                long length = input_stream.Length;
                if (length < 0)
                    length_accessible = false;
            }
            catch (Exception)
            {
                length_accessible = false;
            }
            length_accessible = length_accessible && input_stream.CanSeek;

            if (length_accessible)
                return input_stream;
            else
            {
                var wrapped_memory_stream = new MemoryStream();
                int readed = 0;
                long total = 0;
                // warn while read 1GB from input_stream to memory
                const long warn_threshold = 1024 * 1024 * 1024;
                bool has_warned = false;
                var buffer = new byte[4096];
                do
                {
                    readed = input_stream.Read(buffer, 0, 4096);
                    total += readed;
                    if (!has_warned && total >= warn_threshold)
                    {
                        Tracer.GlobalTracer.TraceWarning("Total bytes read from non-seekable stream has exceeded the warning threshold (" + Util.FormatBytes(warn_threshold) + "), which may cause high usage of memory");
                        has_warned = true;
                    }
                    wrapped_memory_stream.Write(buffer, 0, readed);
                } while (readed > 0);

                // close the original stream and return the wrapped stream
                if (close_if_unused)
                    input_stream.Close();
                // seek to begin
                wrapped_memory_stream.Position = 0;
                return wrapped_memory_stream;
            }
        }
    }
}
