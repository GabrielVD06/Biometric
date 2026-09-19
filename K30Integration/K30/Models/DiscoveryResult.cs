namespace K30Integration.K30.Models;

public class DiscoveryResult
{
    public string? Firmware { get; set; }

    public DeviceCapacity? Capacity { get; set; }

    public DeviceState StateBefore { get; set; }

    public DeviceState StateAfter { get; set; }

    public Dictionary<string, string?> Options { get; set; } = [];

    public List<K30User> Users { get; set; } = [];

    public byte[] AttendanceRaw { get; set; } = [];

    public int AttendanceRawLength => AttendanceRaw.Length;
}