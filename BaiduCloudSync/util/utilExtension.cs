using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil
{
    public static class UtilExtension
    {
        public static double ToUnixTimestamp(this DateTime time)
        {
            return Util.ToUnixTimestamp(time);
        }

    }
}
