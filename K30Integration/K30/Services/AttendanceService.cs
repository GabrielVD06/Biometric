using K30Integration.K30.Models;

namespace K30Integration.K30.Services;

public class AttendanceService
{
    private readonly ZkClient _client;

    public AttendanceService(
        ZkClient client)
    {
        _client = client;
    }

    public async Task<List<AttendanceRecord>>
        GetAttendanceAsync(
            IReadOnlyList<K30User>? users = null,
            CancellationToken cancellationToken = default)
    {
        byte[] raw =
            await _client.ReadAttendanceRawDiscoveryAsync(
                cancellationToken);

        return ZkClient.ParseAttendance(
            raw,
            users);
    }
}