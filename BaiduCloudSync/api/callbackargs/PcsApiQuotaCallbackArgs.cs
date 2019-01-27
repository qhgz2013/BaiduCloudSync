using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    public sealed class PcsApiQuotaCallbackArgs : PcsApiCallbackArgs
    {
        public long Total { get; private set; }
        public long Used { get; private set; }
        public double Percentage
        {
            get
            {
                if (Used == 0) return 0;
                return (double)Used / Total;
            }
        }
        public PcsApiQuotaCallbackArgs(string failure_msg, object state): base(PcsApiCallbackType.Quota, false, failure_msg, state)
        {
            Total = 0;
            Used = 0;
        }

        public PcsApiQuotaCallbackArgs(long used, long total, object state): base(PcsApiCallbackType.Quota, true, null, state)
        {
            Total = total;
            Used = used;
        }

        public override string ToString()
        {
            return $"Used: {GlobalUtil.Util.FormatBytes((ulong)Used)}, Total: {GlobalUtil.Util.FormatBytes((ulong)Total)} ({(Percentage * 100).ToString("0.00")}%)";
        }
    }
}
