using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil.http
{
    /// <summary>
    /// HTTP请求的范围参数
    /// </summary>
    public sealed class Range
    {
        /// <summary>
        /// 请求开始的字节数
        /// </summary>
        public long? From { get; private set; }
        /// <summary>
        /// 请求结束的字节数
        /// </summary>
        public long? To { get; private set; }

        /// <summary>
        /// HTTP请求的范围参数
        /// </summary>
        /// <param name="from">开始字节数，null为无限制，若该值小于0，则将该值视为结束字节数</param>
        /// <param name="to">结束字节数，null为无限制，该值必须不小于0</param>
        public Range(long? from = null, long? to = null)
        {
            if (to.HasValue && to.Value < 0)
                throw new ArgumentOutOfRangeException("to");
            if (from.HasValue && from.Value < 0)
            {
                if (to.HasValue && -from != to)
                    throw new ArgumentException("Conflict range: to: " + from + " and " + to);
                to = -from;
                from = null;
            }
            if (from >= to)
                throw new ArgumentException("To should larger than From");

            From = from;
            To = to;
        }

        public override string ToString()
        {
            if (From.HasValue || To.HasValue)
                return (From != null ? From.ToString() : "") + "-" + (To != null ? To.ToString() : "");
            else
                return "";
        }

        public static Range Parse(string s)
        {
            Range range;
            if (!TryParse(s, out range))
                throw new ArgumentException("Invalid string");
            return range;
        }

        public static bool TryParse(string s, out Range range)
        {
            range = null;
            if (s == null) return false;
            if (s.Length == 0 || s == "-")
            {
                range = new Range();
                return true;
            }

            long from = -1, to = -1;

            var split = s.Split('-');
            if (split.Length != 2)
                return false;
            var str_from = split[0];
            var str_to = split[1];
            if (str_from.Length > 0 && !long.TryParse(str_from, out from))
                return false;
            if (str_to.Length > 0 && !long.TryParse(str_to, out to))
                return false;
            try
            {
                range = new Range(from == -1 ? (long?)null : from, to == -1 ? (long?)null : to);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }
}
