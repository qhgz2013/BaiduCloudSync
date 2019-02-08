using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    public sealed class PcsApiURLCallbackArgs : PcsApiCallbackArgs
    {
        public string[] URL { get; private set; }

        public PcsApiURLCallbackArgs(string failure_msg, object state = null) : base(PcsApiCallbackType.URL, false, failure_msg, state)
        {
            URL = null;
        }

        public PcsApiURLCallbackArgs(IEnumerable<string> urls, object state = null): base(PcsApiCallbackType.URL, true, null, state)
        {
            URL = urls.ToArray();
        }
    }
}
