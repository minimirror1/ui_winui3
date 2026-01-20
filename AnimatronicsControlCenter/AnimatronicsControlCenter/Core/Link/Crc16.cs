namespace AnimatronicsControlCenter.Core.Link;

/// <summary>
/// CRC-16-CCITT implementation for fragment integrity checking
/// Polynomial: 0x1021, Initial: 0xFFFF
/// </summary>
public static class Crc16
{
    private static readonly ushort[] Table = GenerateTable();

    private static ushort[] GenerateTable()
    {
        var table = new ushort[256];
        const ushort polynomial = 0x1021;

        for (int i = 0; i < 256; i++)
        {
            ushort crc = (ushort)(i << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ polynomial);
                else
                    crc <<= 1;
            }
            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Compute CRC-16 for the given data
    /// </summary>
    public static ushort Compute(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;

        for (int i = 0; i < length; i++)
        {
            byte index = (byte)((crc >> 8) ^ data[offset + i]);
            crc = (ushort)((crc << 8) ^ Table[index]);
        }

        return crc;
    }

    /// <summary>
    /// Compute CRC-16 for the entire array
    /// </summary>
    public static ushort Compute(byte[] data)
    {
        return Compute(data, 0, data.Length);
    }

    /// <summary>
    /// Verify CRC-16 (data should include the CRC bytes at the end)
    /// Returns true if valid (CRC remainder should be 0 or specific value)
    /// </summary>
    public static bool Verify(byte[] data, int offset, int lengthWithCrc)
    {
        if (lengthWithCrc < 2) return false;

        // Compute CRC over data (excluding the last 2 CRC bytes)
        ushort computed = Compute(data, offset, lengthWithCrc - 2);

        // Extract stored CRC (Big Endian)
        int crcOffset = offset + lengthWithCrc - 2;
        ushort stored = (ushort)((data[crcOffset] << 8) | data[crcOffset + 1]);

        return computed == stored;
    }

    /// <summary>
    /// Append CRC-16 to data (Big Endian)
    /// </summary>
    public static void Append(byte[] data, int offset, int length)
    {
        ushort crc = Compute(data, offset, length);
        data[offset + length] = (byte)(crc >> 8);
        data[offset + length + 1] = (byte)(crc & 0xFF);
    }
}
