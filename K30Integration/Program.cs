using K30Integration.K30;

const string K30_IP = "192.168.1.201";
const int K30_PORT = 4370;

using ZkClient k30 = new(K30_IP, K30_PORT);

try
{
    Console.WriteLine("========================================");
    Console.WriteLine("       PRUEBA HISTORIAL K30             ");
    Console.WriteLine("========================================");

    Console.WriteLine();
    Console.WriteLine("1. Conectando...");

    await k30.ConnectAsync();

    Console.WriteLine();
    Console.WriteLine($"Session ID: {k30.SessionId}");

    Console.WriteLine();
    Console.WriteLine("2. Información del dispositivo...");

    await k30.GetOptionAsync("~Platform");
    await k30.GetOptionAsync("~ZKFPVersion");

    Console.WriteLine();
    Console.WriteLine("3. Leyendo historial de asistencias...");

    byte[] data =
        await k30.ReadAttendanceRawAsync();

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("           RESULTADO DE PRUEBA           ");
    Console.WriteLine("========================================");

    Console.WriteLine();
    Console.WriteLine(
        $"Bytes recibidos: {data.Length}");

    if (data.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Datos recibidos:");

        Console.WriteLine(
            Convert.ToHexString(data));
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine(
            "El K30 no devolvió datos de asistencia.");
    }

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine(
        "El programa NO ejecuta ningún comando para");
    Console.WriteLine(
        "borrar el historial del K30.");
    Console.WriteLine("========================================");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("                ERROR                   ");
    Console.WriteLine("========================================");

    Console.WriteLine();
    Console.WriteLine(ex.Message);

    Console.WriteLine();
    Console.WriteLine(ex);
}
finally
{
    Console.WriteLine();
    Console.WriteLine("Desconectando del K30...");

    await k30.DisconnectAsync();

    Console.WriteLine(
        "Conexión finalizada.");
}