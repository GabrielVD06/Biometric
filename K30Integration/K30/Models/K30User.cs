namespace K30Integration.K30.Models;

public class K30User
{
    public ushort InternalNumber { get; set; }

    public string Password { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public uint CardNumber { get; set; }

    public byte Group { get; set; }

    public byte UserIdTimezone1 { get; set; }

    public byte UserIdTimezone2 { get; set; }

    public byte Permission { get; set; }

    public string UserId { get; set; } = string.Empty;

    public byte[] RawData { get; set; } = [];

    public override string ToString()
    {
        return
            $"Internal={InternalNumber} | " +
            $"UserId={UserId} | " +
            $"Name={Name} | " +
            $"Card={CardNumber} | " +
            $"Permission={Permission}";
    }
}