using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    public sealed class PcsApiObjectMetaCallbackArgs : PcsApiCallbackArgs
    {
        public PcsMetadata Metadata { get; private set; }

        public PcsApiObjectMetaCallbackArgs(string failure_msg, object state = null) : base(PcsApiCallbackType.SingleObjectMetadata, false, failure_msg, state)
        {
            Metadata = null;
        }

        public PcsApiObjectMetaCallbackArgs(PcsMetadata metadata, object state = null) : base(PcsApiCallbackType.SingleObjectMetadata, true, null, state)
        {
            Metadata = metadata;
        }

    }
}
