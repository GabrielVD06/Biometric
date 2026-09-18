using K30Integration.K30;
using K30Integration.K30.Services;

const string K30_IP = "192.168.1.201";
const int K30_PORT = 4370;

using ZkClient k30 =
    new(K30_IP, K30_PORT);

CancellationTokenSource cts =
    new();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();

    Console.WriteLine();
    Console.WriteLine(
        "Cancelando prueba...");
};

try
{
    Console.WriteLine(
        "========================================");

    Console.WriteLine(
        "       K30 REALTIME FINGERPRINT         ");

    Console.WriteLine(
        "========================================");

    // ------------------------------------------------------------
    // 1. CONNECT
    // ------------------------------------------------------------

    Console.WriteLine();
    Console.WriteLine(
        "1. Conectando...");

    await k30.ConnectAsync();

    Console.WriteLine(
        $"Session ID: {k30.SessionId}");

    // ------------------------------------------------------------
    // 2. DEVICE INFORMATION
    // ------------------------------------------------------------

    Console.WriteLine();
    Console.WriteLine(
        "2. Información del K30...");

    await k30.GetOptionAsync("~Platform");
    await k30.GetOptionAsync("~ZKFPVersion");

    // ------------------------------------------------------------
    // 3. ENABLE REALTIME
    // ------------------------------------------------------------

    Console.WriteLine();
    Console.WriteLine(
        "3. Configurando eventos realtime...");

    await k30.EnableRealtimeAsync();

    // ------------------------------------------------------------
    // 4. WAIT FINGER
    // ------------------------------------------------------------

    AttendanceService attendanceService =
        new(k30);
    var record =
        await attendanceService.WaitForFingerprintAsync(
            cts.Token);

    // ------------------------------------------------------------
    // 5. RESULT
    // ------------------------------------------------------------

    if (record != null)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "              RESULTADO                 ");

        Console.WriteLine(
            "========================================");

        Console.WriteLine();

        Console.WriteLine(
            $"Usuario ID : {record.UserId}");

        Console.WriteLine(
            $"Hora       : {record.Timestamp:yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine
        ($"Nombre: {record.UserName}");

        Console.WriteLine();

        Console.WriteLine(
            "La huella fue recibida correctamente.");

        Console.WriteLine(
            "========================================");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    Console.WriteLine(
        "Prueba cancelada.");
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
    Console.WriteLine(ex.Message);
    Console.WriteLine();
    Console.WriteLine(ex);
}
finally
{
    Console.WriteLine();
    Console.WriteLine(
        "Desconectando del K30...");

    await k30.DisconnectAsync();

    Console.WriteLine(
        "Conexión finalizada.");
}

static string GetVerifyModeName(
    int mode)
{
    return mode switch
    {
        0 => "Password",
        1 => "Huella",
        2 => "Tarjeta",
        _ => $"Desconocido ({mode})"
    };
}