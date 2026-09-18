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
    // CONSTRUCTOR
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

        await _client.ConnectAsync(
            _ip,
            _port);

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
            $"Realtime response: " +
            $"Command={response.Command}");

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
                await ReadPacketAsync(
                    cancellationToken);

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
    // READ ATTENDANCE
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
            // ----------------------------------------------------
            // 1. Solicitar historial
            // ----------------------------------------------------

            Console.WriteLine();
            Console.WriteLine(
                "Solicitando historial de asistencias...");

            ZkResponse prepareResponse =
                await SendAsync(
                    ZkProtocol.CMD_ATTLOG_RRQ,
                    []);

            Console.WriteLine(
                $"Attendance response: " +
                $"Command={prepareResponse.Command}");

            Console.WriteLine(
                $"Attendance data length: " +
                $"{prepareResponse.Data.Length}");

            if (prepareResponse.Command !=
                ZkProtocol.CMD_PREPARE_DATA)
            {
                throw new InvalidOperationException(
                    "El K30 no respondió con " +
                    "CMD_PREPARE_DATA. " +
                    $"Command={prepareResponse.Command}");
            }

            if (prepareResponse.Data.Length < 4)
            {
                throw new InvalidOperationException(
                    "La respuesta CMD_PREPARE_DATA " +
                    "no contiene el tamaño de los datos.");
            }


            // ----------------------------------------------------
            // 2. Obtener tamaño total
            // ----------------------------------------------------

            uint totalSize =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    prepareResponse.Data.AsSpan(0, 4));

            Console.WriteLine();
            Console.WriteLine(
                $"Tamaño total anunciado por K30: " +
                $"{totalSize} bytes");


            if (totalSize == 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "El K30 no tiene asistencias almacenadas.");

                await FreeDataAsync();

                return [];
            }


            // ----------------------------------------------------
            // 3. Recibir CMD_DATA
            // ----------------------------------------------------

            List<byte> allData =
                new((int)totalSize);

            Console.WriteLine();
            Console.WriteLine(
                "Esperando paquetes CMD_DATA...");

            while (allData.Count < totalSize)
            {
                ZkPacket dataPacket =
                    await ReadPacketAsync();

                Console.WriteLine();
                Console.WriteLine(
                    $"Paquete de datos recibido: " +
                    $"Command={dataPacket.Command}");

                Console.WriteLine(
                    $"DataLength={dataPacket.Data.Length}");

                if (dataPacket.Command !=
                    ZkProtocol.CMD_DATA)
                {
                    throw new InvalidOperationException(
                        "El K30 envió un paquete inesperado " +
                        "durante la lectura de asistencias. " +
                        $"Command={dataPacket.Command}");
                }

                if (dataPacket.Data.Length == 0)
                {
                    throw new InvalidOperationException(
                        "El K30 envió un paquete CMD_DATA " +
                        "sin datos.");
                }

                allData.AddRange(
                    dataPacket.Data);

                Console.WriteLine(
                    $"Datos acumulados: " +
                    $"{allData.Count}/{totalSize}");
            }


            // ----------------------------------------------------
            // 4. Ajustar exactamente al tamaño anunciado
            // ----------------------------------------------------

            if (allData.Count > totalSize)
            {
                allData =
                    allData
                        .Take((int)totalSize)
                        .ToList();
            }

            byte[] result =
                allData.ToArray();


            // ----------------------------------------------------
            // 5. Mostrar información de la transferencia
            // ----------------------------------------------------

            Console.WriteLine();
            Console.WriteLine(
                "Transferencia completada.");

            Console.WriteLine(
                $"Bytes recibidos: {result.Length}");

            if (result.Length >= 4)
            {
                uint recordDataSize =
                    BinaryPrimitives
                        .ReadUInt32LittleEndian(
                            result.AsSpan(0, 4));

                Console.WriteLine(
                    $"Tamaño indicado dentro de CMD_DATA: " +
                    $"{recordDataSize} bytes");

                if (recordDataSize % 40 == 0)
                {
                    int recordCount =
                        (int)recordDataSize / 40;

                    Console.WriteLine(
                        $"Registros detectados: " +
                        $"{recordCount}");
                }
                else
                {
                    Console.WriteLine(
                        "El tamaño de registros no es " +
                        "múltiplo de 40; lo analizaremos " +
                        "en el siguiente paso.");
                }
            }


            // ----------------------------------------------------
            // 6. Liberar buffer del K30
            // ----------------------------------------------------

            await FreeDataAsync();

            return result;
        }
        finally
        {
            // ----------------------------------------------------
            // 7. Volver a habilitar dispositivo
            // ----------------------------------------------------

            await EnableDeviceAsync();
        }
    }


    // ============================================================
    // FREE DATA
    // ============================================================

    private async Task FreeDataAsync()
    {
        Console.WriteLine();
        Console.WriteLine(
            "Liberando buffer de datos del K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_FREE_DATA,
                []);

        Console.WriteLine(
            $"FREE_DATA response: " +
            $"Command={response.Command}");

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                "El K30 rechazó CMD_FREE_DATA. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "Buffer liberado correctamente.");
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
            $"{Convert.ToHexString(
                responsePacket.Encode())}");

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