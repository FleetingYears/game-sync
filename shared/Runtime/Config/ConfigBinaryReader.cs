using System;
using System.IO;
using System.Text;

namespace GameSync.Config
{
    internal sealed class ConfigBinaryReader : IDisposable
    {
        /// <summary>当前配置二进制数据流。</summary>
        private readonly MemoryStream m_Stream;
        /// <summary>读取小端二进制字段的读取器。</summary>
        private readonly BinaryReader m_Reader;
        /// <summary>拒绝非法字节序列的 UTF-8 编码器。</summary>
        private readonly UTF8Encoding m_Encoding = new UTF8Encoding(false, true);

        public ConfigBinaryReader(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            m_Stream = new MemoryStream(bytes, false);
            m_Reader = new BinaryReader(m_Stream, m_Encoding);
        }

        private void Require(int count)
        {
            if (count < 0 || count > m_Stream.Length - m_Stream.Position)
                throw new InvalidDataException("配置数据长度不合法或文件被截断。");
        }

        public int ReadHeader(string scope, string table, int fields, int key, int minimumRowSize)
        {
            Require(6);
            if (m_Reader.ReadByte() != 'G' || m_Reader.ReadByte() != 'S'
                || m_Reader.ReadByte() != 'C' || m_Reader.ReadByte() != 'F')
                throw new InvalidDataException("配置文件 magic 不正确。");
            if (m_Reader.ReadUInt16() != 1)
                throw new InvalidDataException("不支持的配置格式版本。");
            if (ReadString() != scope || ReadString() != table)
                throw new InvalidDataException("配置文件来源或表名与类不匹配。");
            int fieldCount = ReadCount();
            int rowCount = ReadCount();
            int keyIndex = ReadCount();
            if (fieldCount != fields || keyIndex != key)
                throw new InvalidDataException("配置字段数或主键与类不匹配。");
            if ((long)rowCount * minimumRowSize > m_Stream.Length - m_Stream.Position)
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
            return m_Reader.ReadByte();
        }

        private int ReadCount()
        {
            Require(4);
            uint count = m_Reader.ReadUInt32();
            if (count > int.MaxValue) throw new InvalidDataException("配置长度超出支持范围。");
            return (int)count;
        }

        public string ReadString()
        {
            int length = ReadCount();
            Require(length);
            try { return m_Encoding.GetString(m_Reader.ReadBytes(length)); }
            catch (DecoderFallbackException exception)
            { throw new InvalidDataException("配置字符串不是合法 UTF-8。", exception); }
        }

        public int ReadInt32() { Require(4); return m_Reader.ReadInt32(); }
        public long ReadInt64() { Require(8); return m_Reader.ReadInt64(); }

        public bool ReadBoolean()
        {
            byte value = ReadByte();
            if (value > 1) throw new InvalidDataException("bool 必须编码为 0 或 1。");
            return value == 1;
        }

        public float ReadSingle()
        {
            Require(4);
            float value = m_Reader.ReadSingle();
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("float 必须为有限值。");
            return value;
        }

        public double ReadDouble()
        {
            Require(8);
            double value = m_Reader.ReadDouble();
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
            if ((long)count * minimumSize > m_Stream.Length - m_Stream.Position)
                throw new InvalidDataException("数组长度超出剩余数据长度。");
            var items = new T[count];
            for (int i = 0; i < count; i++) items[i] = read();
            return items;
        }

        public void EnsureEnd()
        {
            if (m_Stream.Position != m_Stream.Length)
                throw new InvalidDataException("配置文件存在多余数据。");
        }

        public void ValidateRows(int count, int minimumSize)
        {
            if ((long)count * minimumSize > m_Stream.Length - m_Stream.Position)
                throw new InvalidDataException("行数据长度不足，拒绝分配配置列表。");
        }

        public void Dispose() { m_Reader.Dispose(); }
    }
}
