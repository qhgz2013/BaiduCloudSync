using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil.cryptography.streamadapter
{
    [Flags]
    internal enum KeyFlags
    {
        IsKeyEncryptedUsingRSA = 0x1,
        HasAESIVChecksum = 0x2,
        HasAESKeyChecksum = 0x4,
        HasDataChecksum = 0x8
    }
}
