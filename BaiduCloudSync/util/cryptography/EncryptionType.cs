using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil.cryptography
{
    public enum EncryptionType
    {
        DynamicAES,
        StaticAES,
        [Obsolete("This encryption type only support ver 1.0 protocol, which is no longer maintain and use")]
        LegacyDynamicAES,
        [Obsolete("This encryption type only support ver 1.0 protocol, which is no longer maintain and use")]
        LegacyStaticAES
    }
}
