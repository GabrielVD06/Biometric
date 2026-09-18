namespace K30Integration.K30;

public class ZkResponse
{
    public ushort Command { get; set; }
    public ushort Checksum { get; set; }
    public ushort SessionId { get; set; }
    public ushort ReplyId { get; set; }
    public byte[] Data { get; set; } = [];

    public bool IsOk =>
        Command == ZkProtocol.CMD_ACK_OK;

    public bool IsUnauthorized =>
        Command == ZkProtocol.CMD_ACK_UNAUTH;

    public override string ToString()
    {
        return
            $"Command={Command}, " +
            $"Checksum=0x{Checksum:X4}, " +
            $"SessionId={SessionId}, " +
            $"ReplyId={ReplyId}, " +
            $"DataLength={Data.Length}";
    }
}