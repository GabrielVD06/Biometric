namespace K30Integration.Utils;

public static class HexUtils
{
    public static string ToHex(byte[] data)
    {
        return Convert.ToHexString(data);
    }

    public static string ToHex(IEnumerable<byte> data)
    {
        return Convert.ToHexString(data.ToArray());
    }
}