using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    public sealed class PcsApiMultiObjectMetaCallbackArgs : PcsApiCallbackArgs
    {
        public PcsMetadata[] PcsMetadatas { get; private set; }
        public PcsApiMultiObjectMetaCallbackArgs(string failure_msg = null, object state = null) : base(PcsApiCallbackType.MultiObjectMetadata, false, failure_msg, state)
        {
            PcsMetadatas = null;
        }
        public PcsApiMultiObjectMetaCallbackArgs(IEnumerable<PcsMetadata> pcsMetadatas, object state = null) : base(PcsApiCallbackType.MultiObjectMetadata, true, null, state)
        {
            PcsMetadatas = pcsMetadatas.ToArray();
        }
    }
}
