using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    public sealed class PcsApiPreCreateCallbackArgs : PcsApiCallbackArgs
    {
        public string UploadID { get; private set; }
        public PcsApiPreCreateCallbackArgs(string failure_msg, string upload_id, object state = null) : base(PcsApiCallbackType.PreCreateResult, !string.IsNullOrEmpty(upload_id), failure_msg, state)
        {
            UploadID = upload_id;
        }
        
    }
}
