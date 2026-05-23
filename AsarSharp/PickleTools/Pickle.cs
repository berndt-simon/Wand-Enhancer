using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace AsarSharp.PickleTools;

public class Pickle
{
    public const int SIZE_INT32 = 4;
    public const int SIZE_UINT32 = 4;
    public const int SIZE_INT64 = 8;
    public const int SIZE_UINT64 = 8;
    public const int SIZE_FLOAT = 4;
    public const int SIZE_DOUBLE = 8;

    // Initial payload allocation. Bumped from 64 — large headers used to
    // realloc many times when growing geometrically from 64.
    public const int PAYLOAD_UNIT = 4096;

    public const long CAPACITY_READ_ONLY = 9007199254740992;

    private byte[] _header;
    private int _headerSize;
    private long _capacityAfterHeader;
    private int _writeOffset;

    private Pickle(byte[]? buffer = null)
    {
        if (buffer != null)
        {
            _header = buffer;
            _headerSize = buffer.Length - GetPayloadSize();
            _capacityAfterHeader = CAPACITY_READ_ONLY;
            _writeOffset = 0;

            if (_headerSize > buffer.Length)
            {
                _headerSize = 0;
            }

            if (_headerSize != AlignInt(_headerSize, SIZE_UINT32))
            {
                _headerSize = 0;
            }

            if (_headerSize == 0)
            {
                _header = new byte[0];
            }
        }
        else
        {
            _header = new byte[0];
            _headerSize = SIZE_UINT32;
            _capacityAfterHeader = 0;
            _writeOffset = 0;
            Resize(PAYLOAD_UNIT);
            SetPayloadSize(0);
        }
    }

    public static Pickle CreateEmpty() => new Pickle();
    public static Pickle CreateFromBuffer(byte[] buffer) => new Pickle(buffer);

    public byte[] GetHeader() => _header;
    public int GetHeaderSize() => _headerSize;

    public PickleIterator CreateIterator() => new PickleIterator(this);

    /// <summary>Total byte length of the serialised pickle (header + payload).</summary>
    public int GetTotalSize() => _headerSize + GetPayloadSize();

    /// <summary>Materialise the pickle into a fresh byte array (allocates).</summary>
    public byte[] ToBuffer()
    {
        var resultSize = GetTotalSize();
        var result = new byte[resultSize];
        _header.AsSpan(0, resultSize).CopyTo(result);
        return result;
    }

    /// <summary>Write the serialised pickle straight to <paramref name="stream"/> — no extra copy.</summary>
    public void WriteTo(Stream stream)
    {
        stream.Write(_header, 0, GetTotalSize());
    }


    public bool WriteBool(bool value) => WriteInt(value ? 1 : 0);

    public bool WriteInt(int value)
    {
        const int dataLength = SIZE_INT32; // already 4-byte aligned
        var newSize = _writeOffset + dataLength;

        if (newSize > _capacityAfterHeader)
        {
            Resize(Math.Max((int)_capacityAfterHeader * 2, newSize));
        }

        WriteInt32LE(value, _headerSize + _writeOffset);
        SetPayloadSize(newSize);
        _writeOffset = newSize;
        return true;
    }


    public bool WriteUInt32(uint value)
    {
        const int dataLength = SIZE_UINT32;
        var newSize = _writeOffset + dataLength;

        if (newSize > _capacityAfterHeader)
        {
            Resize(Math.Max((int)_capacityAfterHeader * 2, newSize));
        }

        WriteUInt32LE(value, _headerSize + _writeOffset);
        SetPayloadSize(newSize);
        _writeOffset = newSize;
        return true;
    }

    public bool WriteInt64(long value)
    {
        const int dataLength = SIZE_INT64;
        var newSize = _writeOffset + dataLength;

        if (newSize > _capacityAfterHeader)
        {
            Resize(Math.Max((int)_capacityAfterHeader * 2, newSize));
        }

        WriteInt64LE(value, _headerSize + _writeOffset);
        SetPayloadSize(newSize);
        _writeOffset = newSize;
        return true;
    }


    public bool WriteUInt64(ulong value)
    {
        const int dataLength = SIZE_UINT64;
        var newSize = _writeOffset + dataLength;

        if (newSize > _capacityAfterHeader)
        {
            Resize(Math.Max((int)_capacityAfterHeader * 2, newSize));
        }

        WriteUInt64LE(value, _headerSize + _writeOffset);
        SetPayloadSize(newSize);
        _writeOffset = newSize;
        return true;
    }

    public bool WriteFloat(float value)
    {
        const int dataLength = SIZE_FLOAT;
        var newSize = _writeOffset + dataLength;

        if (newSize > _capacityAfterHeader)
        {
            Resize(Math.Max((int)_capacityAfterHeader * 2, newSize));
        }

        BinaryPrimitives.WriteSingleLittleEndian(_header.AsSpan(_headerSize + _writeOffset), value);

        SetPayloadSize(newSize);
        _writeOffset = newSize;
        return true;
    }

    public bool WriteDouble(double value)
    {
        const int dataLength = SIZE_DOUBLE;
        var newSize = _writeOffset + dataLength;

        if (newSize > _capacityAfterHeader)
        {
            Resize(Math.Max((int)_capacityAfterHeader * 2, newSize));
        }

        BinaryPrimitives.WriteDoubleLittleEndian(_header.AsSpan(_headerSize + _writeOffset), value);

        SetPayloadSize(newSize);
        _writeOffset = newSize;
        return true;
    }

    public bool WriteString(string value)
    {
        var byteLen = Encoding.UTF8.GetByteCount(value);

        if (!WriteInt(byteLen))
        {
            return false;
        }

        var aligned = AlignInt(byteLen, SIZE_UINT32);
        var newSize = _writeOffset + aligned;

        if (newSize > _capacityAfterHeader)
        {
            Resize(Math.Max((int)_capacityAfterHeader * 2, newSize));
        }

        var writeStart = _headerSize + _writeOffset;
        Encoding.UTF8.GetBytes(value, _header.AsSpan(writeStart));

        // zero alignment padding
        _header.AsSpan(writeStart + byteLen, aligned - byteLen).Clear();

        SetPayloadSize(newSize);
        _writeOffset = newSize;
        return true;
    }

    public void SetPayloadSize(int payloadSize)
    {
        WriteUInt32LE((uint)payloadSize, 0);
    }

    public int GetPayloadSize() => (int)ReadUInt32LE(0);

    private void Resize(int newCapacity)
    {
        newCapacity = AlignInt(newCapacity, PAYLOAD_UNIT);
        var newHeader = new byte[_header.Length + newCapacity];
        Buffer.BlockCopy(_header, 0, newHeader, 0, _header.Length);
        _header = newHeader;
        _capacityAfterHeader = newCapacity;
    }

    public static int AlignInt(int i, int alignment)
    {
        return i + ((alignment - (i % alignment)) % alignment);
    }

    #region Auxiliary methods for reading/writing values in Little Endian

    private uint ReadUInt32LE(int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(offset));

    private void WriteInt32LE(int value, int offset) =>
        BinaryPrimitives.WriteInt32LittleEndian(_header.AsSpan(offset), value);

    private void WriteUInt32LE(uint value, int offset) =>
        BinaryPrimitives.WriteUInt32LittleEndian(_header.AsSpan(offset), value);

    private void WriteInt64LE(long value, int offset) =>
        BinaryPrimitives.WriteInt64LittleEndian(_header.AsSpan(offset), value);

    private void WriteUInt64LE(ulong value, int offset) =>
        BinaryPrimitives.WriteUInt64LittleEndian(_header.AsSpan(offset), value);


    #endregion
}