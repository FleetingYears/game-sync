using System;
using System.IO;
using System.Text;

namespace GameSync.Config
{
    internal sealed class ConfigBinaryReader : IDisposable
    {
        private readonly MemoryStream _stream;
        private readonly BinaryReader _reader;
        private readonly UTF8Encoding _encoding = new UTF8Encoding(false, true);

        public ConfigBinaryReader(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            _stream = new MemoryStream(bytes, false);
            _reader = new BinaryReader(_stream, _encoding);
        }

        private void Require(int count)
        {
            if (count < 0 || count > _stream.Length - _stream.Position)
                throw new InvalidDataException("配置数据长度不合法或文件被截断。");
        }

        public int ReadHeader(string scope, string table, int fields, int key, int minimumRowSize)
        {
            Require(6);
            if (_reader.ReadByte() != 'G' || _reader.ReadByte() != 'S'
                || _reader.ReadByte() != 'C' || _reader.ReadByte() != 'F')
                throw new InvalidDataException("配置文件 magic 不正确。");
            if (_reader.ReadUInt16() != 1)
                throw new InvalidDataException("不支持的配置格式版本。");
            if (ReadString() != scope || ReadString() != table)
                throw new InvalidDataException("配置文件来源或表名与类不匹配。");
            int fieldCount = ReadCount();
            int rowCount = ReadCount();
            int keyIndex = ReadCount();
            if (fieldCount != fields || keyIndex != key)
                throw new InvalidDataException("配置字段数或主键与类不匹配。");
            if ((long)rowCount * minimumRowSize > _stream.Length - _stream.Position)
                throw new InvalidDataException("配置行数超出剩余文件长度。");
            return rowCount;
        }

        public void ExpectField(string name, byte code, byte flags, string enumName, string[] members, int[] values)
        {
            if (ReadString() != name || ReadByte() != code || ReadByte() != flags || ReadString() != enumName)
                throw new InvalidDataException("配置字段类型或顺序与类不匹配：" + name);
            if (ReadCount() != members.Length)
                throw new InvalidDataException("枚举定义与类不匹配：" + enumName);
            for (int i = 0; i < members.Length; i++)
                if (ReadString() != members[i] || ReadInt32() != values[i])
                    throw new InvalidDataException("枚举成员或固定值与类不匹配：" + enumName);
        }

        private byte ReadByte()
        {
            Require(1);
            return _reader.ReadByte();
        }

        private int ReadCount()
        {
            Require(4);
            uint count = _reader.ReadUInt32();
            if (count > int.MaxValue) throw new InvalidDataException("配置长度超出支持范围。");
            return (int)count;
        }

        public string ReadString()
        {
            int length = ReadCount();
            Require(length);
            try { return _encoding.GetString(_reader.ReadBytes(length)); }
            catch (DecoderFallbackException exception)
            { throw new InvalidDataException("配置字符串不是合法 UTF-8。", exception); }
        }

        public int ReadInt32() { Require(4); return _reader.ReadInt32(); }
        public long ReadInt64() { Require(8); return _reader.ReadInt64(); }

        public bool ReadBoolean()
        {
            byte value = ReadByte();
            if (value > 1) throw new InvalidDataException("bool 必须编码为 0 或 1。");
            return value == 1;
        }

        public float ReadSingle()
        {
            Require(4);
            float value = _reader.ReadSingle();
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("float 必须为有限值。");
            return value;
        }

        public double ReadDouble()
        {
            Require(8);
            double value = _reader.ReadDouble();
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("double 必须为有限值。");
            return value;
        }

        public int ReadEnum(int[] allowed)
        {
            int value = ReadInt32();
            if (Array.IndexOf(allowed, value) < 0) throw new InvalidDataException("配置存在未知枚举值。");
            return value;
        }

        public T[] ReadArray<T>(int minimumSize, Func<T> read)
        {
            int count = ReadCount();
            if ((long)count * minimumSize > _stream.Length - _stream.Position)
                throw new InvalidDataException("数组长度超出剩余数据长度。");
            var items = new T[count];
            for (int i = 0; i < count; i++) items[i] = read();
            return items;
        }

        public void EnsureEnd()
        {
            if (_stream.Position != _stream.Length)
                throw new InvalidDataException("配置文件存在多余数据。");
        }

        public void ValidateRows(int count, int minimumSize)
        {
            if ((long)count * minimumSize > _stream.Length - _stream.Position)
                throw new InvalidDataException("行数据长度不足，拒绝分配配置列表。");
        }

        public void Dispose() { _reader.Dispose(); }
    }
}
