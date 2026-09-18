using K30Integration.K30;

Console.WriteLine("========================================");
Console.WriteLine("       ETAPA 3 - DIAGNOSTICO ATTLOG");
Console.WriteLine("========================================");
Console.WriteLine();

ZkClient client =
    new ZkClient(
        "192.168.1.201",
        4370);

try
{
    // ========================================================
    // 1. CONNECT
    // ========================================================

    Console.WriteLine(
        "1. Conectando...");

    await client.ConnectAsync();

    Console.WriteLine();

    // ========================================================
    // 2. INFORMACIÓN
    // ========================================================

    Console.WriteLine(
        "2. Información del dispositivo...");

    await client.GetOptionAsync(
        "~Platform");

    await client.GetOptionAsync(
        "~ZKFPVersion");

    Console.WriteLine();

    // ========================================================
    // 3. ATTLOG
    // ========================================================

    Console.WriteLine(
        "3. Leyendo historial de asistencias...");

    Console.WriteLine();

    List<K30Integration.K30.Models.AttendanceRecord>
        records =
        await client.ReadAttendanceAsync();

    // ========================================================
    // 4. RESUMEN
    // ========================================================

    Console.WriteLine();
    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        "             DIAGNOSTICO FINAL");

    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        $"Registros encontrados: {records.Count}");

    Console.WriteLine();

    Console.WriteLine(
        "Los datos anteriores son RAW.");

    Console.WriteLine(
        "Todavía NO se están interpretando como");

    Console.WriteLine(
        "ID / fecha / método / estado.");

    Console.WriteLine();

    Console.WriteLine(
        "El historial del K30 NO fue eliminado.");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        "                ERROR");

    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        ex.Message);

    Console.WriteLine();
    Console.WriteLine(
        ex.ToString());
}
finally
{
    await client.DisconnectAsync();
}