using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.nbt
{
    /// <summary>
    /// NBT的tag类型
    /// </summary>
    public enum TagType
    {
        TAG_UNDEFINED,
        TAG_INT, TAG_LONG, TAG_BYTE, TAG_UINT, TAG_ULONG, TAG_FLOAT, TAG_DOUBLE,
        TAG_STRING, TAG_BYTE_ARRAY, TAG_LIST
    }
    public abstract class Tag
    {
        protected TagType _tagType;

        protected Tag()
        {
            _tagType = TagType.TAG_UNDEFINED;
            _name = string.Empty;
            _data = null;
        }
        //读取data
        protected abstract void _read_data(Stream s);
        //写入整个tag
        protected abstract void _write_data(Stream s);
        /// <summary>
        /// 获取Tag中的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public virtual T GetData<T>()
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                return (T)Convert.ChangeType(_data, typeof(T)); //(T)(object)_data;
            return (T)typeof(T).Assembly.CreateInstance(typeof(T).Name);
        }
        public abstract void SetData<T>(T data);
        protected object _data;
        protected string _name;
        protected void _write_header(Stream s)
        {
            s.WriteByte((byte)_tagType);
            var name_bytes = Encoding.UTF8.GetBytes(_name);
            var length = BitConverter.GetBytes((ushort)name_bytes.Length).ToList();
            length.Reverse();
            s.Write(length.ToArray(), 0, 2);
            s.Write(name_bytes, 0, name_bytes.Length);
        }
        public static Tag ReadTagFromStream(Stream s)
        {
            var tagtype = (TagType)s.ReadByte();
            Tag ret = null;
            var name_length_ls = util.ReadBytes(s, 2).ToList();
            name_length_ls.Reverse();
            var name_length = BitConverter.ToUInt16(name_length_ls.ToArray(), 0);
            var name_bytes = util.ReadBytes(s, name_length);
            var name = Encoding.UTF8.GetString(name_bytes);

            switch (tagtype)
            {
                case TagType.TAG_UNDEFINED:
                    break;
                case TagType.TAG_INT:
                    ret = new TagInt(s);
                    break;
                case TagType.TAG_LONG:
                    ret = new TagLong(s);
                    break;
                case TagType.TAG_BYTE:
                    ret = new TagByte(s);
                    break;
                case TagType.TAG_UINT:
                    ret = new TagUInt(s);
                    break;
                case TagType.TAG_ULONG:
                    ret = new TagULong(s);
                    break;
                case TagType.TAG_FLOAT:
                    ret = new TagFloat(s);
                    break;
                case TagType.TAG_DOUBLE:
                    ret = new TagDouble(s);
                    break;
                case TagType.TAG_STRING:
                    ret = new TagString(s);
                    break;
                case TagType.TAG_BYTE_ARRAY:
                    ret = new TagByteArray(s);
                    break;
                case TagType.TAG_LIST:
                    ret = new TagList(s);
                    break;
                default:
                    throw new ArgumentException("Invalid Tag Type");
            }
            if (ret != null)
            {
                ret._name = name;
            }
            return ret;
        }
        public static void WriteTagToStream(Tag tag, Stream s)
        {
            tag._write_header(s);
            tag._write_data(s);
        }
        protected void _set_name(string name)
        {
            if (name == null) name = string.Empty;
            _name = name;
        }
    }
    public class TagInt : Tag
    {
        public TagInt(string name, int data)
        {
            _tagType = TagType.TAG_INT;
            _set_name(name);
            _data = data;
        }
        public TagInt(Stream s)
        {
            _tagType = TagType.TAG_INT;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var data = util.ReadBytes(s, 4).ToList();
            data.Reverse();
            _data = BitConverter.ToInt32(data.ToArray(), 0);
        }
        protected override void _write_data(Stream s)
        {
            var data = BitConverter.GetBytes((int)_data).ToList();
            data.Reverse();
            s.Write(data.ToArray(), 0, 4);
        }
        public override string ToString()
        {
            return "TagInt {" + _name + "=" + (int)_data + "}";
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                _data = Convert.ToInt32(data);
        }
    }
    public class TagLong : Tag
    {
        public TagLong(string name, long data)
        {
            _tagType = TagType.TAG_LONG;
            _set_name(name);
            _data = data;
        }
        public TagLong(Stream s)
        {
            _tagType = TagType.TAG_LONG;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var data = util.ReadBytes(s, 8).ToList();
            data.Reverse();
            _data = BitConverter.ToInt64(data.ToArray(), 0);
        }
        protected override void _write_data(Stream s)
        {
            var data = BitConverter.GetBytes((long)_data).ToList();
            data.Reverse();
            s.Write(data.ToArray(), 0, 8);
        }
        public override string ToString()
        {
            return "TagLong {" + _name + "=" + (long)_data + "}";
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                _data = Convert.ToInt64(data);
        }
    }
    public class TagByte : Tag
    {
        public TagByte(string name, byte data)
        {
            _tagType = TagType.TAG_BYTE;
            _set_name(name);
            _data = data;
        }
        public TagByte(Stream s)
        {
            _tagType = TagType.TAG_BYTE;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            _data = (byte)s.ReadByte();
        }
        protected override void _write_data(Stream s)
        {
            s.WriteByte((byte)_data);
        }
        public override string ToString()
        {
            return "TagByte {" + _name + "=" + (byte)_data + "}";
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                _data = Convert.ToByte(data);
        }
    }
    public class TagUInt : Tag
    {
        public TagUInt(string name, uint data)
        {
            _tagType = TagType.TAG_UINT;
            _set_name(name);
            _data = data;
        }
        public TagUInt(Stream s)
        {
            _tagType = TagType.TAG_UINT;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var data = util.ReadBytes(s, 4).ToList();
            data.Reverse();
            _data = BitConverter.ToUInt32(data.ToArray(), 0);
        }
        protected override void _write_data(Stream s)
        {
            var data = BitConverter.GetBytes((uint)_data).ToList();
            data.Reverse();
            s.Write(data.ToArray(), 0, 4);
        }
        public override string ToString()
        {
            return "TagUInt {" + _name + "=" + (uint)_data + "}";
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                _data = Convert.ToUInt32(data);
        }
    }
    public class TagULong : Tag
    {
        public TagULong(string name, ulong data)
        {
            _tagType = TagType.TAG_ULONG;
            _set_name(name);
            _data = data;
        }
        public TagULong(Stream s)
        {
            _tagType = TagType.TAG_ULONG;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var data = util.ReadBytes(s, 8).ToList();
            data.Reverse();
            _data = BitConverter.ToUInt64(data.ToArray(), 0);
        }
        protected override void _write_data(Stream s)
        {
            var data = BitConverter.GetBytes((ulong)_data).ToList();
            data.Reverse();
            s.Write(data.ToArray(), 0, 8);
        }
        public override string ToString()
        {
            return "TagULong {" + _name + "=" + (ulong)_data + "}";
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                _data = Convert.ToUInt64(data);
        }
    }
    public class TagFloat : Tag
    {
        public TagFloat(string name, float data)
        {
            _tagType = TagType.TAG_FLOAT;
            _set_name(name);
            _data = data;
        }
        public TagFloat(Stream s)
        {
            _tagType = TagType.TAG_FLOAT;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var data = util.ReadBytes(s, 4).ToList();
            data.Reverse();
            _data = BitConverter.ToSingle(data.ToArray(), 0);
        }
        protected override void _write_data(Stream s)
        {
            var data = BitConverter.GetBytes((float)_data).ToList();
            data.Reverse();
            s.Write(data.ToArray(), 0, 4);
        }
        public override string ToString()
        {
            return "TagFloat {" + _name + "=" + (float)_data + "}";
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                _data = Convert.ToSingle(data);
        }
    }
    public class TagDouble : Tag
    {
        public TagDouble(string name, double data)
        {
            _tagType = TagType.TAG_DOUBLE;
            _set_name(name);
            _data = data;
        }
        public TagDouble(Stream s)
        {
            _tagType = TagType.TAG_DOUBLE;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var data = util.ReadBytes(s, 8).ToList();
            data.Reverse();
            _data = BitConverter.ToDouble(data.ToArray(), 0);
        }
        protected override void _write_data(Stream s)
        {
            var data = BitConverter.GetBytes((double)_data).ToList();
            data.Reverse();
            s.Write(data.ToArray(), 0, 8);
        }
        public override string ToString()
        {
            return "TagDouble {" + _name + "=" + (double)_data + "}";
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
                typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
                typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(char) || typeof(T) == typeof(byte)
                )
                _data = Convert.ToDouble(data);
        }
    }
    public class TagString : Tag
    {
        public TagString(string name, string data)
        {
            if (data == null) data = string.Empty;
            _tagType = TagType.TAG_STRING;
            _set_name(name);
            _data = data;
        }
        public TagString(Stream s)
        {
            _tagType = TagType.TAG_STRING;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var length = util.ReadBytes(s, 4).ToList();
            length.Reverse();
            var ilength = BitConverter.ToInt32(length.ToArray(), 0);
            var data = util.ReadBytes(s, ilength);
            _data = Encoding.UTF8.GetString(data);
        }
        protected override void _write_data(Stream s)
        {
            var data = Encoding.UTF8.GetBytes((string)_data);
            var length = BitConverter.GetBytes(data.Length).ToList();
            length.Reverse();
            s.Write(length.ToArray(), 0, 4);
            s.Write(data, 0, data.Length);
        }
        public override string ToString()
        {
            return "TagString {" + _name + "=" + (string)_data + "}";
        }
        public override T GetData<T>()
        {
            if (typeof(T) == typeof(string))
                return (T)Convert.ChangeType(_data, typeof(T));
            return (T)typeof(T).Assembly.CreateInstance(typeof(T).Name);
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(string))
                _data = Convert.ToString(data);
        }
    }
    public class TagByteArray : Tag
    {
        public TagByteArray(string name, byte[] data)
        {
            _tagType = TagType.TAG_BYTE_ARRAY;
            _set_name(name);
            _data = data;
        }
        public TagByteArray(Stream s)
        {
            _tagType = TagType.TAG_BYTE_ARRAY;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var length = util.ReadBytes(s, 4).ToList();
            length.Reverse();
            var ilength = BitConverter.ToInt32(length.ToArray(), 0);
            var data = util.ReadBytes(s, ilength);
            _data = data;
        }
        protected override void _write_data(Stream s)
        {
            var ilength = ((byte[])_data).Length;
            var length = BitConverter.GetBytes(ilength).ToList();
            length.Reverse();
            s.Write(length.ToArray(), 0, 4);
            s.Write((byte[])_data, 0, ilength);
        }
        public override string ToString()
        {
            return "TagByteArray {" + _name + "=[ count=" + ((byte[])_data).Length + " ]}";
        }
        public override T GetData<T>()
        {
            if (typeof(T) == typeof(byte[]))
                return (T)Convert.ChangeType(_data, typeof(T));
            return (T)typeof(T).Assembly.CreateInstance(typeof(T).Name);
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(byte[]))
                _data = Convert.ChangeType(data, typeof(byte[]));
        }
        public byte GetData(int index)
        {
            if (index < 0 || index >= ((byte[])_data).Length) return 0;
            return ((byte[])_data)[index];
        }
        public void SetData(int index, byte data)
        {
            if (index < 0 || index >= ((byte[])_data).Length) return;
            ((byte[])_data)[index] = data;
        }
    }
    public class TagList : Tag, IList<Tag>
    {
        public int Count
        {
            get
            {
                return ((List<Tag>)_data).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public Tag this[int index]
        {
            get
            {
                return GetData(index);
            }

            set
            {
                SetData(index, value);
            }
        }

        public TagList(string name, List<Tag> data)
        {
            if (data == null) data = new List<Tag>();
            _tagType = TagType.TAG_LIST;
            _set_name(name);
            _data = data;
        }
        public TagList(Stream s)
        {
            _tagType = TagType.TAG_LIST;
            _read_data(s);
        }
        protected override void _read_data(Stream s)
        {
            var count = util.ReadBytes(s, 4).ToList();
            count.Reverse();
            var icount = BitConverter.ToInt32(count.ToArray(), 0);
            var data = new List<Tag>(icount);
            for (int i = 0; i < icount; i++)
            {
                data.Add(Tag.ReadTagFromStream(s));
            }
            _data = data;
        }
        protected override void _write_data(Stream s)
        {
            var data = (List<Tag>)_data;
            var count = BitConverter.GetBytes(data.Count).ToList();
            count.Reverse();
            s.Write(count.ToArray(), 0, 4);
            for (int i = 0; i < data.Count; i++)
            {
                WriteTagToStream(data[i], s);
            }
        }
        public override string ToString()
        {
            return "TagList {" + _name + "=[ count=" + ((List<Tag>)_data).Count + " ]}";
        }
        public override T GetData<T>()
        {
            if (typeof(T) == typeof(List<Tag>))
                return (T)Convert.ChangeType(_data, typeof(T));
            return (T)typeof(T).Assembly.CreateInstance(typeof(T).Name);
        }
        public override void SetData<T>(T data)
        {
            if (typeof(T) == typeof(List<Tag>))
                _data = Convert.ChangeType(data, typeof(List<Tag>));
        }
        public Tag GetData(int index)
        {
            //if (index < 0 || index >= ((List<Tag>)_data).Count) return null;
            return ((List<Tag>)_data)[index];
        }
        public void SetData(int index, Tag data)
        {
            //if (index < 0 || index >= ((List<Tag>)_data).Count) return;
            ((List<Tag>)_data)[index] = data;
        }

        public int IndexOf(Tag item)
        {
            return ((List<Tag>)_data).IndexOf(item);
        }

        public void Insert(int index, Tag item)
        {
            ((List<Tag>)_data).Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ((List<Tag>)_data).RemoveAt(index);
        }

        public void Add(Tag item)
        {
            ((List<Tag>)_data).Add(item);
        }

        public void Clear()
        {
            ((List<Tag>)_data).Clear();
        }

        public bool Contains(Tag item)
        {
            return ((List<Tag>)_data).Contains(item);
        }

        public void CopyTo(Tag[] array, int arrayIndex)
        {
            ((List<Tag>)_data).CopyTo(array, arrayIndex);
        }

        public bool Remove(Tag item)
        {
            return ((List<Tag>)_data).Remove(item);
        }

        public IEnumerator<Tag> GetEnumerator()
        {
            return ((List<Tag>)_data).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((List<Tag>)_data).GetEnumerator();
        }
    }
}
