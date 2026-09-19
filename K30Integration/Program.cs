using K30Integration.K30;
using K30Integration.K30.Models;

Console.WriteLine("========================================");
Console.WriteLine("       K30 - READ TEST");
Console.WriteLine("========================================");
Console.WriteLine();

ZkClient client =
    new(
        "192.168.1.201",
        4370);

try
{
    // =========================================================
    // CONNECT
    // =========================================================

    Console.WriteLine("1. CONNECT");
    Console.WriteLine();

    await client.ConnectAsync();

    Console.WriteLine();

    // =========================================================
    // DEVICE INFO
    // =========================================================

    Console.WriteLine("2. DEVICE INFO");
    Console.WriteLine();

    string firmware =
        await client.GetFirmwareAsync();

    Console.WriteLine(
        $"Firmware: {firmware}");

    string? serial =
        await client.GetOptionAsync(
            "~SerialNumber");

    Console.WriteLine(
        $"Serial: {serial}");

    string? deviceName =
        await client.GetOptionAsync(
            "~DeviceName");

    Console.WriteLine(
        $"Device: {deviceName}");

    string? platform =
        await client.GetOptionAsync(
            "~Platform");

    Console.WriteLine(
        $"Platform: {platform}");

    Console.WriteLine();

    // =========================================================
    // CAPACITY
    // =========================================================

    Console.WriteLine("3. CAPACITY");
    Console.WriteLine();

    DeviceCapacity capacity =
        await client.GetFreeSizesAsync();

    Console.WriteLine(
        capacity);

    Console.WriteLine();

    // =========================================================
    // USERS
    // =========================================================

    Console.WriteLine("4. USERS");
    Console.WriteLine();

    List<K30User> users =
        await client.ReadUsersAsync();

    Console.WriteLine();

    Console.WriteLine(
        $"USERS RESULT: {users.Count}");

    foreach (K30User user in users)
    {
        Console.WriteLine(
            user);
    }

    Console.WriteLine();

    // =========================================================
    // ATTENDANCE
    // =========================================================

    Console.WriteLine("5. ATTENDANCE");
    Console.WriteLine();

    byte[] attendanceRaw =
        await client.ReadAttendanceRawDiscoveryAsync();

    Console.WriteLine();

    Console.WriteLine(
        $"ATTENDANCE RAW: " +
        $"{attendanceRaw.Length} bytes");

    // =========================================================
    // PARSE
    // =========================================================

    Console.WriteLine();

    Console.WriteLine(
        "6. PARSING ATTENDANCE");

    Console.WriteLine();

    List<AttendanceRecord> records =
        ZkClient.ParseAttendance(
            attendanceRaw,
            users);

    Console.WriteLine();

    Console.WriteLine(
        $"ATTENDANCE RESULT: " +
        $"{records.Count} records");

    foreach (AttendanceRecord record in records)
    {
        Console.WriteLine(
            record);
    }

    // =========================================================
    // FIN
    // =========================================================

    Console.WriteLine();

    Console.WriteLine("========================================");
    Console.WriteLine("             READ COMPLETED");
    Console.WriteLine("========================================");

    Console.WriteLine();

    Console.WriteLine(
        $"Firmware : {firmware}");

    Console.WriteLine(
        $"Users    : {users.Count}");

    Console.WriteLine(
        $"Raw logs : {attendanceRaw.Length}");

    Console.WriteLine(
        $"Records  : {records.Count}");

    Console.WriteLine();

    Console.WriteLine(
        "NO se eliminaron usuarios.");

    Console.WriteLine(
        "NO se eliminaron asistencias.");

    Console.WriteLine(
        "CMD_CLEAR_ATTLOG NO fue ejecutado.");
}
catch (Exception ex)
{
    Console.WriteLine();

    Console.WriteLine("========================================");
    Console.WriteLine("                  ERROR");
    Console.WriteLine("========================================");

    Console.WriteLine();

    Console.WriteLine(
        ex.Message);

    Console.WriteLine();

    Console.WriteLine(
        ex.ToString());
}
finally
{
    Console.WriteLine();

    Console.WriteLine(
        "Desconectando...");

    await client.DisconnectAsync();

    Console.WriteLine(
        "Desconectado.");
}