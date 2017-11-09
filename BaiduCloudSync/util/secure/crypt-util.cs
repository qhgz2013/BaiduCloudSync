using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace GlobalUtil
{
    public partial class Crypt
    {
        /// <summary>
        /// 将字节散列化加密
        /// </summary>
        /// <param name="seed">随机种子</param>
        /// <returns></returns>
        //public static byte[] GetEncryptHashTable(int? seed = null)
        //{
        //    Random rnd;
        //    if (seed == null)
        //        rnd = new Random();
        //    else
        //        rnd = new Random((int)seed);

        //    List<byte> bList = new List<byte>(256);
        //    List<byte> hashList = new List<byte>(256);
        //    for (int i = 0; i < 256; i++) bList.Add((byte)i);
        //    while (bList.Count > 0)
        //    {
        //        var index = rnd.Next(bList.Count);
        //        hashList.Add(bList[index]);
        //        bList.RemoveAt(index);
        //    }
        //    return hashList.ToArray();
        //}
        //public static byte GetEncryptByte(byte data, byte[] encryptHashTable)
        //{
        //    return encryptHashTable[data];
        //}
        //public static byte GetDecryptByte(byte data, byte[] decryptHashTable)
        //{
        //    return decryptHashTable[data];
        //}
        //public static byte[] GetEncryptHashTable(byte[] decryptHashTable)
        //{
        //    return GetDecryptHashTable(decryptHashTable);
        //}
        //public static byte[] GetDecryptHashTable(byte[] encryptHashTable)
        //{
        //    List<KeyValuePair<byte, byte>> keydata = new List<KeyValuePair<byte, byte>>(256);
        //    for (int i = 0; i < 256; i++) keydata.Add(new KeyValuePair<byte, byte>((byte)i, encryptHashTable[i]));
        //    keydata.Sort((a, b) => a.Value.CompareTo(b.Value));
        //    var decdata = new List<byte>(256);
        //    for (int i = 0; i < 256; i++) decdata.Add(keydata[i].Key);
        //    return decdata.ToArray();
        //}

        public static Image GenerateRandomBitmap(int width, int height, int? seed = null)
        {
            Random rnd;
            if (seed == null)
                rnd = new Random();
            else
                rnd = new Random((int)seed);
            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
            var lck = bmp.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
            var data = new byte[lck.Stride * bmp.Height];
            Marshal.Copy(lck.Scan0, data, 0, data.Length);

            rnd.NextBytes(data);
            Marshal.Copy(data, 0, lck.Scan0, data.Length);
            bmp.UnlockBits(lck);
            return bmp;
        }
    }
}
