namespace K30Integration.K30.Models;

public class DeviceCapacity
{
    public uint UserCount { get; set; }
    public uint UserCapacity { get; set; }

    public uint FingerprintCount { get; set; }
    public uint FingerprintCapacity { get; set; }

    public uint AttendanceCount { get; set; }
    public uint AttendanceCapacity { get; set; }

    public uint Unknown1 { get; set; }
    public uint Unknown2 { get; set; }

    public override string ToString()
    {
        return
            $"Users: {UserCount}/{UserCapacity}, " +
            $"Fingerprints: {FingerprintCount}/{FingerprintCapacity}, " +
            $"Attendance: {AttendanceCount}/{AttendanceCapacity}";
    }
}