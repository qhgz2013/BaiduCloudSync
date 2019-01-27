using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    public sealed class PcsApiOperationCallbackArgs : PcsApiCallbackArgs
    {
        public PcsApiOperationCallbackArgs(string failure_msg, object state) : base(PcsApiCallbackType.OperationResult, false, failure_msg, state)
        {
        }

        public PcsApiOperationCallbackArgs(object state) : base(PcsApiCallbackType.OperationResult, true, null, state)
        {
        }

        public override string ToString()
        {
            return "Success: " + Success;
        }
    }
}
