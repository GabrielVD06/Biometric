using K30Integration.K30;
using K30Integration.K30.Models;

const string K30_IP = "192.168.1.201";
const int K30_PORT = 4370;

using ZkClient k30 =
    new(K30_IP, K30_PORT);

try
{
    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        "       PRUEBA LECTURA ATTLOG K30        ");

    Console.WriteLine(
        "========================================");

    Console.WriteLine();

    Console.WriteLine(
        "1. Conectando...");

    await k30.ConnectAsync();

    Console.WriteLine();

    Console.WriteLine(
        $"Session ID: {k30.SessionId}");

    Console.WriteLine();

    Console.WriteLine(
        "2. Información del dispositivo...");

    await k30.GetOptionAsync(
        "~Platform");

    await k30.GetOptionAsync(
        "~ZKFPVersion");

    Console.WriteLine();

    Console.WriteLine(
        "3. Leyendo historial de asistencias...");

    List<AttendanceRecord> records =
        await k30.ReadAttendanceAsync();

    Console.WriteLine();

    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        "           REGISTROS OBTENIDOS          ");

    Console.WriteLine(
        "========================================");

    Console.WriteLine();

    Console.WriteLine(
        $"Total de registros: {records.Count}");

    Console.WriteLine();

    foreach (AttendanceRecord record in records)
    {
        Console.WriteLine(
            record.ToString());

        Console.WriteLine(
            $"RAW: {record.RawHex}");

        Console.WriteLine();
    }

    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        "El historial NO ha sido borrado.");

    Console.WriteLine(
        "No se ejecutó CMD_CLEAR_ATTLOG.");

    Console.WriteLine(
        "========================================");
}
catch (Exception ex)
{
    Console.WriteLine();

    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        "                ERROR                   ");

    Console.WriteLine(
        "========================================");

    Console.WriteLine();

    Console.WriteLine(
        ex.Message);

    Console.WriteLine();

    Console.WriteLine(
        ex);
}
finally
{
    Console.WriteLine();

    Console.WriteLine(
        "Desconectando del K30...");

    await k30.DisconnectAsync();

    Console.WriteLine();

    Console.WriteLine(
        "Conexión finalizada.");
}