using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    public sealed class PcsApiSegmentUploadCallbackArgs : PcsApiCallbackArgs
    {
        public string SegmentMD5 { get; private set; }
        public PcsApiSegmentUploadCallbackArgs(string segment_md5, string failure_msg, object state = null) : base(PcsApiCallbackType.SegmentUploadResult, !string.IsNullOrEmpty(segment_md5), failure_msg, state)
        {
            SegmentMD5 = segment_md5;
        }
        
    }
}
