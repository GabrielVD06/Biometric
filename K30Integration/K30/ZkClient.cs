using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace K30Integration.K30;

public class ZkClient : IDisposable
{
    private readonly string _ip;
    private readonly int _port;

    private TcpClient? _client;
    private NetworkStream? _stream;

    private ushort _sessionId;
    private ushort _replyId;

    private bool _disposed;

    public ushort SessionId => _sessionId;

    public bool IsConnected =>
        _client?.Connected == true &&
        _stream != null;


    // ============================================================
    // Constructor
    // ============================================================

    public ZkClient(string ip, int port = 4370)
    {
        _ip = ip;
        _port = port;
    }


    // ============================================================
    // CONNECT
    // ============================================================

    public async Task ConnectAsync()
    {
        EnsureNotDisposed();

        _client = new TcpClient();

        await _client.ConnectAsync(_ip, _port);

        _stream = _client.GetStream();

        Console.WriteLine(
            $"TCP conectado a {_ip}:{_port}");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_CONNECT,
                []);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"El K30 rechazó la conexión. " +
                $"Command={response.Command}");
        }

        _sessionId = response.SessionId;

        _replyId = 0;

        Console.WriteLine(
            "Conexión ZKTeco establecida.");

        Console.WriteLine(
            $"Session ID: {_sessionId}");
    }


    // ============================================================
    // DISCONNECT
    // ============================================================

    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return;

        try
        {
            await SendAsync(
                ZkProtocol.CMD_EXIT,
                []);
        }
        catch
        {
            // La conexión puede haber sido cerrada
            // previamente por el dispositivo.
        }

        try
        {
            _stream?.Close();
            _client?.Close();
        }
        catch
        {
        }

        _stream = null;
        _client = null;
    }


    // ============================================================
    // DISABLE DEVICE
    // ============================================================

    public async Task DisableDeviceAsync()
    {
        EnsureConnected();

        Console.WriteLine();
        Console.WriteLine(
            "Deshabilitando temporalmente el K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_DISABLE_DEVICE,
                []);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"El K30 rechazó CMD_DISABLE_DEVICE. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "K30 deshabilitado temporalmente.");
    }


    // ============================================================
    // ENABLE DEVICE
    // ============================================================

    public async Task EnableDeviceAsync()
    {
        EnsureConnected();

        Console.WriteLine();
        Console.WriteLine(
            "Habilitando nuevamente el K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_ENABLE_DEVICE,
                []);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"El K30 rechazó CMD_ENABLE_DEVICE. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "K30 habilitado nuevamente.");
    }


    // ============================================================
    // REALTIME
    // ============================================================

    public async Task EnableRealtimeAsync()
    {
        EnsureConnected();

        byte[] data =
        [
            0xFF,
            0xFF,
            0x00,
            0x00
        ];

        Console.WriteLine();
        Console.WriteLine(
            "Habilitando eventos realtime...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_REG_EVENT,
                data);

        Console.WriteLine(
            $"Realtime response: Command={response.Command}");

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"El K30 rechazó CMD_REG_EVENT. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "Eventos realtime habilitados.");
    }


    public async Task<ZkRealtimeEvent> WaitForRealtimeEventAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        while (!cancellationToken.IsCancellationRequested)
        {
            ZkPacket packet =
                await ReadPacketAsync(cancellationToken);

            if (packet.Command !=
                ZkProtocol.CMD_REG_EVENT)
            {
                Console.WriteLine();

                Console.WriteLine(
                    "Paquete recibido mientras " +
                    "esperábamos evento: " +
                    $"Command={packet.Command}");

                continue;
            }

            ZkRealtimeEvent realtimeEvent =
                ParseRealtimeEvent(packet);

            await SendRealtimeAckAsync(
                packet.ReplyId,
                cancellationToken);

            return realtimeEvent;
        }

        throw new OperationCanceledException(
            cancellationToken);
    }


    private async Task SendRealtimeAckAsync(
        ushort replyId,
        CancellationToken cancellationToken)
    {
        EnsureConnected();

        ZkPacket packet = new()
        {
            Command = ZkProtocol.CMD_ACK_OK,
            SessionId = _sessionId,
            ReplyId = replyId,
            Data = []
        };

        byte[] bytes =
            packet.Encode();

        Console.WriteLine(
            $"TX REALTIME ACK: " +
            $"{Convert.ToHexString(bytes)}");

        await _stream!.WriteAsync(
            bytes,
            cancellationToken);

        await _stream.FlushAsync(
            cancellationToken);
    }


    private static ZkRealtimeEvent ParseRealtimeEvent(
        ZkPacket packet)
    {
        ushort eventCode =
            packet.SessionId;

        byte[] data =
            packet.Data ?? [];

        return new ZkRealtimeEvent
        {
            EventCode = eventCode,
            Data = data,
            UserId = ParseUserId(data),
            Timestamp = ParseRealtimeTimestamp(data)
        };
    }


    private static string ParseUserId(
        byte[] data)
    {
        if (data.Length < 9)
            return string.Empty;

        return Encoding.ASCII
            .GetString(data, 0, 9)
            .Trim(
                '\0',
                ' ',
                '\r',
                '\n');
    }


    private static DateTime? ParseRealtimeTimestamp(
        byte[] data)
    {
        if (data.Length < 32)
            return null;

        int year =
            2000 + data[26];

        int month =
            data[27];

        int day =
            data[28];

        int hour =
            data[29];

        int minute =
            data[30];

        int second =
            data[31];

        try
        {
            return new DateTime(
                year,
                month,
                day,
                hour,
                minute,
                second);
        }
        catch
        {
            return null;
        }
    }


    // ============================================================
    // OPTIONS
    // ============================================================

    public async Task<string?> GetOptionAsync(
        string option)
    {
        EnsureConnected();

        byte[] data =
            Encoding.ASCII.GetBytes(
                option + "\0");

        Console.WriteLine();
        Console.WriteLine(
            $"Consultando opción: {option}");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_OPTIONS_RRQ,
                data);

        Console.WriteLine(
            $"Option response: " +
            $"Command={response.Command}");

        if (!response.IsOk)
        {
            Console.WriteLine(
                "El K30 rechazó la consulta.");

            return null;
        }

        if (response.Data.Length == 0)
        {
            Console.WriteLine(
                "El K30 no devolvió datos.");

            return null;
        }

        string result =
            Encoding.ASCII
                .GetString(response.Data)
                .Trim(
                    '\0',
                    ' ',
                    '\r',
                    '\n');

        Console.WriteLine(
            $"Option result: {result}");

        return result;
    }


    // ============================================================
    // ATTENDANCE - TEST
    // ============================================================

    public async Task<byte[]> ReadAttendanceRawAsync()
    {
        EnsureConnected();

        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "       LECTURA HISTORIAL K30            ");

        Console.WriteLine(
            "========================================");

        Console.WriteLine();

        await DisableDeviceAsync();

        try
        {
            Console.WriteLine();
            Console.WriteLine(
                "Solicitando historial de asistencias...");

            /*
             * CMD_ATTLOG_RRQ = 13
             *
             * Según el protocolo ZKTeco, esta orden
             * solicita los registros de asistencia.
             *
             * No se envía ningún comando de borrado.
             */
            ZkResponse response =
                await SendAsync(
                    ZkProtocol.CMD_ATTLOG_RRQ,
                    []);

            Console.WriteLine(
                $"Attendance response: " +
                $"Command={response.Command}");

            Console.WriteLine(
                $"Attendance data length: " +
                $"{response.Data.Length}");

            if (response.Data.Length > 0)
            {
                Console.WriteLine(
                    $"Attendance data: " +
                    $"{Convert.ToHexString(response.Data)}");
            }

            if (!response.IsOk &&
                response.Command !=
                    ZkProtocol.CMD_ACK_DATA)
            {
                throw new InvalidOperationException(
                    "El K30 rechazó la solicitud de " +
                    "asistencias. " +
                    $"Command={response.Command}");
            }

            return response.Data;
        }
        finally
        {
            await EnableDeviceAsync();
        }
    }


    // ============================================================
    // GENERIC COMMAND
    // ============================================================

    public async Task<ZkResponse> SendCommandAsync(
        ushort command,
        byte[]? data = null)
    {
        return await SendAsync(
            command,
            data ?? []);
    }


    // ============================================================
    // LOW LEVEL SEND
    // ============================================================

    private async Task<ZkResponse> SendAsync(
        ushort command,
        byte[] data)
    {
        EnsureConnected();

        ushort replyId =
            _replyId++;

        ZkPacket packet = new()
        {
            Command = command,
            SessionId = _sessionId,
            ReplyId = replyId,
            Data = data
        };

        byte[] bytes =
            packet.Encode();

        Console.WriteLine(
            $"TX: {Convert.ToHexString(bytes)}");

        await _stream!.WriteAsync(bytes);

        await _stream.FlushAsync();

        ZkPacket responsePacket =
            await ReadPacketAsync();

        Console.WriteLine(
            $"RX: " +
            $"{Convert.ToHexString(responsePacket.Encode())}");

        return new ZkResponse
        {
            Command =
                responsePacket.Command,

            Checksum =
                responsePacket.Checksum,

            SessionId =
                responsePacket.SessionId,

            ReplyId =
                responsePacket.ReplyId,

            Data =
                responsePacket.Data
        };
    }


    // ============================================================
    // READ PACKET
    // ============================================================

    private async Task<ZkPacket> ReadPacketAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        byte[] header =
            await ReadExactAsync(
                8,
                cancellationToken);

        if (header[0] !=
                ZkProtocol.Header1 ||
            header[1] !=
                ZkProtocol.Header2 ||
            header[2] !=
                ZkProtocol.Header3 ||
            header[3] !=
                ZkProtocol.Header4)
        {
            throw new InvalidOperationException(
                "Header ZKTeco inválido.");
        }

        uint payloadSize =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    header.AsSpan(4, 4));

        if (payloadSize < 8)
        {
            throw new InvalidOperationException(
                $"Payload ZKTeco inválido: " +
                $"{payloadSize}");
        }

        byte[] payload =
            await ReadExactAsync(
                checked((int)payloadSize),
                cancellationToken);

        byte[] completePacket =
            new byte[8 + payload.Length];

        Buffer.BlockCopy(
            header,
            0,
            completePacket,
            0,
            8);

        Buffer.BlockCopy(
            payload,
            0,
            completePacket,
            8,
            payload.Length);

        return ZkPacket.Decode(
            completePacket);
    }


    // ============================================================
    // READ EXACT
    // ============================================================

    private async Task<byte[]> ReadExactAsync(
        int length,
        CancellationToken cancellationToken)
    {
        byte[] buffer =
            new byte[length];

        int offset = 0;

        while (offset < length)
        {
            int read =
                await _stream!.ReadAsync(
                    buffer.AsMemory(
                        offset,
                        length - offset),
                    cancellationToken);

            if (read == 0)
            {
                throw new IOException(
                    "El K30 cerró la conexión.");
            }

            offset += read;
        }

        return buffer;
    }


    // ============================================================
    // VALIDATION
    // ============================================================

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException(
                "El K30 no está conectado.");
        }
    }


    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(ZkClient));
        }
    }


    // ============================================================
    // DISPOSE
    // ============================================================

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _stream?.Close();
            _client?.Close();
        }
        catch
        {
        }

        _stream = null;
        _client = null;
    }
}


// ================================================================
// REALTIME EVENT
// ================================================================

public class ZkRealtimeEvent
{
    public ushort EventCode { get; set; }

    public byte[] Data { get; set; } = [];

    public string UserId { get; set; } =
        string.Empty;

    public DateTime? Timestamp { get; set; }


    public bool IsAttendance =>
        EventCode ==
        ZkProtocol.EF_ATTLOG;


    public bool IsFinger =>
        EventCode ==
        ZkProtocol.EF_FINGER;


    public bool IsVerify =>
        EventCode ==
        ZkProtocol.EF_VERIFY;


    public string EventName =>
        EventCode switch
        {
            ZkProtocol.EF_ATTLOG =>
                "Asistencia",

            ZkProtocol.EF_FINGER =>
                "Huella detectada",

            ZkProtocol.EF_VERIFY =>
                "Usuario verificado",

            ZkProtocol.EF_ENROLLUSER =>
                "Usuario registrado",

            ZkProtocol.EF_ENROLLFINGER =>
                "Huella registrada",

            ZkProtocol.EF_BUTTON =>
                "Botón",

            ZkProtocol.EF_UNLOCK =>
                "Desbloqueo",

            ZkProtocol.EF_FPFTR =>
                "Fingerprint feature",

            ZkProtocol.EF_ALARM =>
                "Alarma",

            _ =>
                $"Evento {EventCode}"
        };


    public override string ToString()
    {
        return
            $"Evento={EventName}, " +
            $"Code={EventCode}, " +
            $"UserId={UserId}, " +
            $"Time={Timestamp:yyyy-MM-dd HH:mm:ss}, " +
            $"DataLength={Data.Length}";
    }
}