using K30Integration.K30.Models;

namespace K30Integration.K30.Services;

public class AttendanceService
{
    private readonly ZkClient _client;

    public AttendanceService(ZkClient client)
    {
        _client = client;
    }

    public async Task<AttendanceRecord?> WaitForFingerprintAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "       ESPERANDO HUELLA EN EL K30       ");

        Console.WriteLine(
            "========================================");

        Console.WriteLine();
        Console.WriteLine(
            "Coloca una huella registrada en el K30...");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            ZkRealtimeEvent realtimeEvent =
                await _client.WaitForRealtimeEventAsync(
                    cancellationToken);

            Console.WriteLine();
            Console.WriteLine(
                $"Evento recibido: {realtimeEvent.EventName}");

            Console.WriteLine(
                $"EventCode: {realtimeEvent.EventCode}");

            Console.WriteLine(
                $"DataLength: {realtimeEvent.Data.Length}");

            if (!string.IsNullOrWhiteSpace(
                    realtimeEvent.UserId))
            {
                Console.WriteLine(
                    $"User ID: {realtimeEvent.UserId}");
            }

            if (realtimeEvent.Timestamp.HasValue)
            {
                Console.WriteLine(
                    $"Hora: {realtimeEvent.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            Console.WriteLine(
                $"DATA: {Convert.ToHexString(realtimeEvent.Data)}");

            /*
             * Para esta primera prueba nos interesa
             * específicamente EF_ATTLOG.
             */

            if (!realtimeEvent.IsAttendance)
            {
                continue;
            }

            AttendanceRecord record = new()
            {
                UserId = realtimeEvent.UserId,
                Timestamp =
                    realtimeEvent.Timestamp
                    ?? DateTime.Now,

                /*
                 * En el paquete realtime de asistencia
                 * el verify type está en los bytes 24-25.
                 *
                 * Lo manejaremos aquí.
                 */

                VerifyMode =
                    ParseVerifyMode(
                        realtimeEvent.Data),

                Status = 0
            };

            return record;
        }

        return null;
    }

    private static int ParseVerifyMode(
        byte[] data)
    {
        if (data.Length < 26)
            return -1;

        return data[24];
    }
}