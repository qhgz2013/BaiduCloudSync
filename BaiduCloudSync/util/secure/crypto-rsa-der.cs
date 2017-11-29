using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace GlobalUtil
{
    public class DerParser
    {
        private enum TagType : byte
        {
            Tag_Boolean = 1,
            Tag_Integer = 2,
            Tag_BitString = 3,
            Tag_OctetString = 4,
            Tag_Null = 5,
            Tag_ObjectIdentifier = 6,
            Tag_Utf8String = 0xc,
            Tag_PrintableString = 0x13,
            Tag_IA5String = 0x16,
            Tag_BMPString = 0x1e,
            Tag_Sequence = 0x30,
            Tag_Set = 0x31
        }

        private struct Tag
        {
            public object Data;
            public TagType Type;
        }
        private struct OID_Data
        {
            public byte[] encoded_data;
            public List<int> decoded_data;
        }


        /// <summary>
        /// 以PKCS#1格式(RFC3447标准)解析RSA密钥
        /// </summary>
        /// <param name="data">RSA密钥字节</param>
        /// <returns></returns>
        public static byte[] ParseDERPrivateKeyPKCS1(byte[] data)
        {
            int index = 0;
            Tag der_tag = _parse_object(data, ref index);
            RSAParameters param = new RSAParameters();
            var sequence = (List<Tag>)der_tag.Data;
            var version = (byte[])sequence[0].Data;
            var modules = (byte[])sequence[1].Data;
            var publicExponent = (byte[])sequence[2].Data;
            var privateExponent = (byte[])sequence[3].Data;
            var prime1 = (byte[])sequence[4].Data;
            var prime2 = (byte[])sequence[5].Data;
            var exponent1 = (byte[])sequence[6].Data;
            var exponent2 = (byte[])sequence[7].Data;
            var coefficient = (byte[])sequence[8].Data;
            param.Modulus = modules;
            param.Exponent = publicExponent;
            param.D = privateExponent;
            param.P = prime1;
            param.Q = prime2;
            param.DP = exponent1;
            param.DQ = exponent2;
            param.InverseQ = coefficient;
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(param);
            return rsa.ExportCspBlob(true);
        }
        /// <summary>
        /// 以PKCS#8格式(RFC5208标准)解析RSA密钥
        /// </summary>
        /// <param name="data">RSA密钥字节</param>
        /// <returns></returns>
        public static byte[] ParseDERPrivateKeyPKCS8(byte[] data)
        {
            int index = 0;
            var der_tag = _parse_object(data, ref index);
            var sequence = (List<Tag>)der_tag.Data;
            var private_key = (byte[])sequence[2].Data;
            return ParseDERPrivateKeyPKCS1(private_key);
        }
        /// <summary>
        /// 以PKCS#1格式(RFC3447标准)解析RSA公钥
        /// </summary>
        /// <param name="data">RSA公钥字节</param>
        /// <returns></returns>
        public static byte[] ParseDERPublicKeyPKCS1(byte[] data)
        {
            int index = 0;
            Tag der_tag = _parse_object(data, ref index);
            RSAParameters param = new RSAParameters();
            var sequence = (List<Tag>)der_tag.Data;
            var modules = (byte[])sequence[0].Data;
            var publicExponent = (byte[])sequence[1].Data;
            param.Modulus = modules;
            param.Exponent = publicExponent;
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(param);
            return rsa.ExportCspBlob(false);
        }
        /// <summary>
        /// 以PKCS#8格式(RFC5208标准)解析RSA公钥
        /// </summary>
        /// <param name="data">RSA公钥字节</param>
        /// <returns></returns>
        public static byte[] ParseDERPublicKeyPKCS8(byte[] data)
        {
            int index = 0;
            var der_tag = _parse_object(data, ref index);
            var sequence = (List<Tag>)der_tag.Data;
            var private_key = (byte[])sequence[1].Data;
            return ParseDERPublicKeyPKCS1(private_key);
        }
        private static Tag _parse_object(byte[] data, ref int index)
        {
            switch ((TagType)data[index++])
            {
                case TagType.Tag_Boolean:
                    return _parse_boolean(data, ref index);
                case TagType.Tag_Integer:
                    return _parse_integer(data, ref index);
                case TagType.Tag_BitString:
                    return _parse_bit_string(data, ref index);
                case TagType.Tag_OctetString:
                    return _parse_octet_string(data, ref index);
                case TagType.Tag_Null:
                    return _parse_null(data, ref index);
                case TagType.Tag_ObjectIdentifier:
                    return _parse_object_identifier(data, ref index);
                case TagType.Tag_Utf8String:
                    return _parse_utf8string(data, ref index);
                case TagType.Tag_PrintableString:
                    return _parse_pritablestring(data, ref index);
                case TagType.Tag_IA5String:
                    return _parse_ia5string(data, ref index);
                case TagType.Tag_BMPString:
                    return _parse_bmpstring(data, ref index);
                case TagType.Tag_Sequence:
                    return _parse_sequence(data, ref index);
                case TagType.Tag_Set:
                    return _parse_set(data, ref index);
                default:
                    throw new InvalidDataException("Unexpected DER Tag Value: 0x" + data[index - 1].ToString("X2").ToLower());
            }
        }
        private static Tag _parse_bit_string(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new byte[length - 1];
            Array.Copy(data, index + 1, ret, 0, length - 1);
            ret[length - 2] = (byte)(ret[length - 2] & (0xff << data[index]));
            index += length;
            return new Tag { Type = TagType.Tag_BitString, Data = ret };
        }
        private static Tag _parse_boolean(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            if (length != 1)
            {
                Tracer.GlobalTracer.TraceWarning("Boolean Tag should be 1 byte length, possible it is a mistaken data");
            }
            var boolean_data = new byte[length];
            Array.Copy(data, index, boolean_data, 0, length);
            index += length;
            if (length == 0)
                return new Tag { Type = TagType.Tag_Boolean, Data = false };
            if (boolean_data[0] == 0x00)
                return new Tag { Type = TagType.Tag_Boolean, Data = false };
            else if (boolean_data[0] == 0xff)
                return new Tag { Type = TagType.Tag_Boolean, Data = true };
            else
            {
                Tracer.GlobalTracer.TraceWarning("Boolean value incorrect, expecting 0x00(false) or 0xff(true)");
                return new Tag { Type = TagType.Tag_Boolean, Data = true };
            }
        }

        private static Tag _parse_integer(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new byte[length];
            Array.Copy(data, index, ret, 0, length);
            index += length;
            if (length > 1)
            {
                //erasing leading zero
                if (ret[0] == 0 && (ret[1] & 0x80) != 0)
                {
                    var temp_ret = new byte[length - 1];
                    Array.Copy(ret, 1, temp_ret, 0, length - 1);
                    ret = temp_ret;
                }
            }
            return new Tag { Type = TagType.Tag_Integer, Data = ret };
        }
        private static Tag _parse_null(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            if (length != 0)
            {
                Tracer.GlobalTracer.TraceWarning("Tag Null(0x05) should have 0 data length");
            }
            var ret = new byte[length];
            if (length > 0)
                Array.Copy(data, index, ret, 0, length);
            return new Tag { Type = TagType.Tag_Null, Data = ret };
        }
        private static Tag _parse_object_identifier(byte[] data, ref int index)
        {
            var ret = new OID_Data();
            var length = _parse_variable_length(data, ref index);
            ret.encoded_data = new byte[length];
            Array.Copy(data, index, ret.encoded_data, 0, length);
            index += length;
            ret.decoded_data = new List<int>();
            if (length == 0)
                return new Tag { Type = TagType.Tag_ObjectIdentifier, Data = ret };

            //first two nodes
            ret.decoded_data.Add(ret.encoded_data[0] / 40);
            ret.decoded_data.Add(ret.encoded_data[0] % 40);

            for (int i = 1; i < length;)
            {
                int value = 0;
                do
                {
                    value <<= 7;
                    value |= ret.encoded_data[i] & 0x7f;
                } while ((ret.encoded_data[i++] & 0x80) != 0);
                ret.decoded_data.Add(value);
            }
            return new Tag { Type = TagType.Tag_ObjectIdentifier, Data = ret };
        }
        private static Tag _parse_octet_string(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new byte[length];
            Array.Copy(data, index, ret, 0, length);
            index += length;
            return new Tag { Type = TagType.Tag_OctetString, Data = ret };
        }
        private static Tag _parse_bmpstring(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new byte[length];
            Array.Copy(data, index, ret, 0, length);
            index += length;
            return new Tag { Type = TagType.Tag_BMPString, Data = ret };
        }
        private static Tag _parse_ia5string(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new byte[length];
            Array.Copy(data, index, ret, 0, length);
            index += length;
            return new Tag { Type = TagType.Tag_IA5String, Data = ret };
        }
        private static Tag _parse_pritablestring(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new byte[length];
            Array.Copy(data, index, ret, 0, length);
            index += length;
            return new Tag { Type = TagType.Tag_PrintableString, Data = ret };
        }
        private static Tag _parse_utf8string(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new byte[length];
            Array.Copy(data, index, ret, 0, length);
            index += length;
            return new Tag { Type = TagType.Tag_Utf8String, Data = ret };
        }
        private static Tag _parse_sequence(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new List<Tag>();
            int from_index = index;
            while (index < from_index + length)
            {
                ret.Add(_parse_object(data, ref index));
            }
            return new Tag { Type = TagType.Tag_Sequence, Data = ret };
        }
        private static Tag _parse_set(byte[] data, ref int index)
        {
            var length = _parse_variable_length(data, ref index);
            var ret = new List<Tag>();
            int from_index = index;
            while (index < from_index + length)
            {
                ret.Add(_parse_object(data, ref index));
            }
            return new Tag { Type = TagType.Tag_Set, Data = ret };
        }
        private static int _parse_variable_length(byte[] data, ref int index)
        {
            if ((data[index] & 0x80) != 0)
            {
                int length = (data[index] & 0x7f);
                int i = 0;
                int value = 0;
                for (index++; i < length; index++, i++)
                {
                    value <<= 8;
                    value |= data[index];
                }
                return value;
            }
            else
                return data[index++];
        }
    }
}
