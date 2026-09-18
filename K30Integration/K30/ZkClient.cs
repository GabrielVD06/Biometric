using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using K30Integration.K30.Models;

namespace K30Integration.K30;

public class ZkClient : IDisposable
{
    private readonly string _ip;
    private readonly int _port;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    private ushort _sessionId;

    // Número de solicitud que enviamos al dispositivo.
    private ushort _replyId;

    private bool _connected;
    private bool _disposed;

    public ushort SessionId => _sessionId;

    public bool IsConnected => _connected;

    public ZkClient(
        string ip,
        int port = 4370)
    {
        _ip = ip;
        _port = port;
    }

    // =========================================================
    // CONNECT
    // =========================================================

    public async Task ConnectAsync()
    {
        if (_connected)
            return;

        _tcpClient = new TcpClient();

        await _tcpClient.ConnectAsync(
            IPAddress.Parse(_ip),
            _port);

        _stream = _tcpClient.GetStream();

        Console.WriteLine(
            $"TCP conectado a {_ip}:{_port}");

        _sessionId = 0;

        // El primer ReplyId utilizado por nuestra sesión.
        _replyId = 0;

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_CONNECT);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"El K30 rechazó CONNECT. " +
                $"Command={response.Command}");
        }

        _sessionId = response.SessionId;

        _connected = true;

        Console.WriteLine(
            "Conexión ZKTeco establecida.");

        Console.WriteLine(
            $"Session ID: {_sessionId}");
    }

    // =========================================================
    // DISCONNECT
    // =========================================================

    public async Task DisconnectAsync()
    {
        if (!_connected)
            return;

        try
        {
            ZkResponse response =
                await SendAsync(
                    ZkProtocol.CMD_EXIT);

            if (!response.IsOk)
            {
                Console.WriteLine(
                    $"Advertencia al desconectar: " +
                    $"Command={response.Command}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Advertencia durante EXIT: {ex.Message}");
        }
        finally
        {
            _connected = false;

            _stream?.Dispose();
            _tcpClient?.Dispose();

            _stream = null;
            _tcpClient = null;
        }
    }

    // =========================================================
    // DISABLE
    // =========================================================

    public async Task DisableDeviceAsync()
    {
        Console.WriteLine();
        Console.WriteLine(
            "Deshabilitando temporalmente el K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_DISABLE_DEVICE);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo deshabilitar el K30. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "K30 deshabilitado temporalmente.");
    }

    // =========================================================
    // ENABLE
    // =========================================================

    public async Task EnableDeviceAsync()
    {
        Console.WriteLine();
        Console.WriteLine(
            "Habilitando nuevamente el K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_ENABLE_DEVICE);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo habilitar el K30. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "K30 habilitado nuevamente.");
    }

    // =========================================================
    // OPTIONS
    // =========================================================

    public async Task<string?> GetOptionAsync(
        string option)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Consultando opción: {option}");

        byte[] data =
            Encoding.ASCII.GetBytes(
                option + "\0");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_OPTIONS_RRQ,
                data);

        if (!response.IsOk)
        {
            Console.WriteLine(
                $"Option response error: " +
                $"{response.Command}");

            return null;
        }

        string result =
            Encoding.ASCII.GetString(
                response.Data)
            .TrimEnd('\0');

        Console.WriteLine(
            $"Option response: Command={response.Command}");

        Console.WriteLine(
            $"Option result: {result}");

        return result;
    }

    // =========================================================
    // READ ATTENDANCE
    // =========================================================

    public async Task<List<AttendanceRecord>>
        ReadAttendanceAsync()
    {
        byte[] raw =
            await ReadAttendanceRawAsync();

        return ParseAttendanceRecords(raw);
    }

    // =========================================================
    // READ ATTENDANCE RAW
    // =========================================================

    public async Task<byte[]> ReadAttendanceRawAsync()
    {
        if (!_connected)
        {
            throw new InvalidOperationException(
                "El K30 no está conectado.");
        }

        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "       LECTURA HISTORIAL K30");

        Console.WriteLine(
            "========================================");

        await DisableDeviceAsync();

        try
        {
            // Solicitud documentada para ATTLOG.
            byte[] attendanceRequest =
            [
                0x01,
                0x0D,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            ];

            Console.WriteLine();
            Console.WriteLine(
                "Solicitando historial de asistencias...");

            Console.WriteLine(
                $"CMD_DATA_WRRQ payload: " +
                $"{Convert.ToHexString(attendanceRequest)}");

            ZkResponse response =
                await SendAsync(
                    ZkProtocol.CMD_DATA_WRRQ,
                    attendanceRequest);

            Console.WriteLine(
                $"Attendance response: " +
                $"Command={response.Command}");

            Console.WriteLine(
                $"Attendance data length: " +
                $"{response.Data.Length}");

            // Algunos dispositivos devuelven los datos
            // directamente.
            if (response.Command ==
                ZkProtocol.CMD_DATA)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "El K30 devolvió los datos " +
                    "directamente en CMD_DATA.");

                return response.Data;
            }

            // Otros dispositivos preparan primero el buffer.
            if (response.Command ==
                ZkProtocol.CMD_PREPARE_DATA)
            {
                if (response.Data.Length < 4)
                {
                    throw new InvalidOperationException(
                        "CMD_PREPARE_DATA no contiene " +
                        "el tamaño de datos.");
                }

                uint totalSize =
                    BinaryPrimitives.ReadUInt32LittleEndian(
                        response.Data.AsSpan(0, 4));

                Console.WriteLine();
                Console.WriteLine(
                    $"Tamaño total anunciado por K30: " +
                    $"{totalSize} bytes");

                byte[] result =
                    await ReceiveDataPacketsAsync(
                        totalSize);

                await FreeDataAsync();

                return result;
            }

            throw new InvalidOperationException(
                $"Respuesta inesperada al solicitar " +
                $"asistencias. Command={response.Command}");
        }
        finally
        {
            try
            {
                await EnableDeviceAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"ERROR habilitando K30: {ex.Message}");

                throw;
            }
        }
    }

    // =========================================================
    // RECEIVE DATA PACKETS
    // =========================================================

    private async Task<byte[]>
        ReceiveDataPacketsAsync(
            uint totalSize)
    {
        Console.WriteLine();
        Console.WriteLine(
            "Esperando paquetes CMD_DATA...");

        using MemoryStream buffer =
            new();

        while (buffer.Length < totalSize)
        {
            ZkPacket packet =
                await ReadPacketAsync();

            Console.WriteLine();
            Console.WriteLine(
                $"Paquete recibido: " +
                $"Command={packet.Command}");

            Console.WriteLine(
                $"DataLength={packet.Data.Length}");

            if (packet.Command !=
                ZkProtocol.CMD_DATA)
            {
                throw new InvalidOperationException(
                    $"Se esperaba CMD_DATA " +
                    $"({ZkProtocol.CMD_DATA}), " +
                    $"pero llegó {packet.Command}.");
            }

            buffer.Write(packet.Data);

            Console.WriteLine(
                $"Datos acumulados: " +
                $"{buffer.Length}/{totalSize}");
        }

        byte[] result =
            buffer.ToArray();

        if (result.Length > totalSize)
        {
            Array.Resize(
                ref result,
                checked((int)totalSize));
        }

        Console.WriteLine();
        Console.WriteLine(
            "Transferencia completada.");

        Console.WriteLine(
            $"Bytes recibidos: {result.Length}");

        return result;
    }

    // =========================================================
    // PARSE ATTENDANCE
    // =========================================================

    private static List<AttendanceRecord>
        ParseAttendanceRecords(
            byte[] data)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "       PARSING DE ASISTENCIAS");

        Console.WriteLine(
            "========================================");

        if (data.Length < 4)
        {
            throw new InvalidOperationException(
                $"Los datos tienen solamente " +
                $"{data.Length} bytes.");
        }

        int recordDataSize =
            BinaryPrimitives.ReadInt32LittleEndian(
                data.AsSpan(0, 4));

        Console.WriteLine();
        Console.WriteLine(
            $"Tamaño de registros: " +
            $"{recordDataSize}");

        if (recordDataSize <= 0)
        {
            Console.WriteLine(
                "El K30 no tiene registros.");

            return [];
        }

        int availableBytes =
            data.Length - 4;

        if (recordDataSize >
            availableBytes)
        {
            throw new InvalidOperationException(
                $"El K30 indicó {recordDataSize} " +
                $"bytes, pero solamente recibimos " +
                $"{availableBytes}.");
        }

        if (recordDataSize % 40 != 0)
        {
            throw new InvalidOperationException(
                $"El tamaño de registros " +
                $"{recordDataSize} no es múltiplo de 40.");
        }

        int recordCount =
            recordDataSize / 40;

        Console.WriteLine(
            $"Número de registros: {recordCount}");

        List<AttendanceRecord> records =
            [];

        for (int i = 0; i < recordCount; i++)
        {
            int offset =
                4 + (i * 40);

            byte[] rawRecord =
                new byte[40];

            Array.Copy(
                data,
                offset,
                rawRecord,
                0,
                40);

            AttendanceRecord record =
                ParseAttendanceRecord(
                    rawRecord,
                    i + 1);

            records.Add(record);
        }

        return records;
    }

    // =========================================================
    // PARSE SINGLE ATTENDANCE RECORD
    // =========================================================

    private static AttendanceRecord
        ParseAttendanceRecord(
            byte[] data,
            int recordNumber)
    {
        if (data.Length != 40)
        {
            throw new ArgumentException(
                "Un registro de asistencia debe " +
                "tener exactamente 40 bytes.");
        }

        ushort userSerial =
            BinaryPrimitives.ReadUInt16LittleEndian(
                data.AsSpan(0, 2));

        string userId =
            Encoding.ASCII.GetString(
                data,
                2,
                9)
            .TrimEnd('\0', ' ');

        int verifyMode =
            data[26];

        uint packedTime =
            BinaryPrimitives.ReadUInt32LittleEndian(
                data.AsSpan(27, 4));

        int status =
            data[31];

        DateTime timestamp =
            DecodeZkTime(
                packedTime);

        return new AttendanceRecord
        {
            RecordNumber = recordNumber,
            UserId = userId,
            Timestamp = timestamp,
            VerifyMode = verifyMode,
            Status = status,
            RawData = data
        };
    }

    // =========================================================
    // ZK TIME
    // =========================================================

    private static DateTime
        DecodeZkTime(
            uint value)
    {
        int second =
            (int)(value % 60);

        value /= 60;

        int minute =
            (int)(value % 60);

        value /= 60;

        int hour =
            (int)(value % 24);

        value /= 24;

        int day =
            (int)(value % 31) + 1;

        value /= 31;

        int month =
            (int)(value % 12) + 1;

        int year =
            (int)(value / 12) + 2000;

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
            return DateTime.MinValue;
        }
    }

    // =========================================================
    // FREE DATA
    // =========================================================

    public async Task FreeDataAsync()
    {
        Console.WriteLine();
        Console.WriteLine(
            "Liberando buffer de datos del K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_FREE_DATA);

        Console.WriteLine(
            $"FREE_DATA response: " +
            $"Command={response.Command}");

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"FREE_DATA falló. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "Buffer liberado correctamente.");
    }

    // =========================================================
    // REALTIME
    // =========================================================

    public async Task RegisterRealtimeEventsAsync(
        ushort eventFlags)
    {
        byte[] data =
        [
            0xFF,
            0xFF,
            0x00,
            0x00
        ];

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_REG_EVENT,
                data);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudieron registrar " +
                $"eventos realtime. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            $"Eventos realtime registrados: " +
            $"0x{eventFlags:X4}");
    }

    // =========================================================
    // GENERIC SEND
    // =========================================================

    private async Task<ZkResponse>
        SendAsync(
            ushort command,
            byte[]? data = null)
    {
        if (_stream == null)
        {
            throw new InvalidOperationException(
                "No existe conexión TCP con el K30.");
        }

        // Guardamos el número que corresponde a ESTA
        // solicitud.
        ushort currentReplyId =
            _replyId;

        ZkPacket packet =
            new()
            {
                Command = command,
                SessionId = _sessionId,
                ReplyId = currentReplyId,
                Data = data ?? []
            };

        byte[] bytes =
            packet.Encode();

        Console.WriteLine(
            $"TX: {Convert.ToHexString(bytes)}");

        await _stream.WriteAsync(
            bytes);

        await _stream.FlushAsync();

        ZkPacket response =
            await ReadPacketAsync();

        Console.WriteLine(
            $"RX: {Convert.ToHexString(response.Encode())}");

        /*
         * MUY IMPORTANTE:
         *
         * No copiamos response.ReplyId.
         *
         * El ReplyId es nuestro contador de solicitudes.
         * Avanzamos después de recibir la respuesta.
         */
        _replyId++;

        return new ZkResponse
        {
            Command = response.Command,
            Checksum = response.Checksum,
            SessionId = response.SessionId,
            ReplyId = response.ReplyId,
            Data = response.Data
        };
    }

    // =========================================================
    // READ PACKET
    // =========================================================

    private async Task<ZkPacket>
        ReadPacketAsync()
    {
        if (_stream == null)
        {
            throw new InvalidOperationException(
                "No existe conexión TCP.");
        }

        byte[] header =
            await ReadExactAsync(8);

        if (header[0] != ZkProtocol.Header1 ||
            header[1] != ZkProtocol.Header2 ||
            header[2] != ZkProtocol.Header3 ||
            header[3] != ZkProtocol.Header4)
        {
            throw new InvalidOperationException(
                "Header ZKTeco inválido: " +
                Convert.ToHexString(header));
        }

        uint payloadSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                header.AsSpan(4, 4));

        if (payloadSize < 8)
        {
            throw new InvalidOperationException(
                $"Payload ZKTeco inválido: " +
                $"{payloadSize} bytes.");
        }

        byte[] payload =
            await ReadExactAsync(
                checked((int)payloadSize));

        byte[] packetBytes =
            new byte[
                header.Length +
                payload.Length];

        Buffer.BlockCopy(
            header,
            0,
            packetBytes,
            0,
            header.Length);

        Buffer.BlockCopy(
            payload,
            0,
            packetBytes,
            header.Length,
            payload.Length);

        return ZkPacket.Decode(
            packetBytes);
    }

    // =========================================================
    // READ EXACT
    // =========================================================

    private async Task<byte[]>
        ReadExactAsync(
            int count)
    {
        if (_stream == null)
        {
            throw new InvalidOperationException(
                "No existe conexión TCP.");
        }

        byte[] buffer =
            new byte[count];

        int offset = 0;

        while (offset < count)
        {
            int read =
                await _stream.ReadAsync(
                    buffer.AsMemory(
                        offset,
                        count - offset));

            if (read == 0)
            {
                throw new IOException(
                    "El K30 cerró la conexión.");
            }

            offset += read;
        }

        return buffer;
    }

    // =========================================================
    // REALTIME EVENT
    // =========================================================

    public async Task<ZkRealtimeEvent>
        WaitForRealtimeEventAsync(
            CancellationToken cancellationToken = default)
    {
        ZkPacket packet =
            await ReadPacketAsync();

        return ParseRealtimeEvent(packet);
    }

    private static ZkRealtimeEvent
        ParseRealtimeEvent(
            ZkPacket packet)
    {
        int eventCode =
            packet.SessionId;

        string eventName =
            eventCode switch
            {
                1 => "ATTLOG",
                2 => "FINGER",
                4 => "ENROLLUSER",
                8 => "ENROLLFINGER",
                16 => "BUTTON",
                32 => "UNLOCK",
                128 => "VERIFY",
                256 => "FPFTR",
                512 => "ALARM",
                _ => $"UNKNOWN ({eventCode})"
            };

        return new ZkRealtimeEvent
        {
            EventCode = eventCode,
            EventName = eventName,
            Data = packet.Data,
            IsAttendance =
                eventCode ==
                ZkProtocol.EF_ATTLOG
        };
    }

    // =========================================================
    // DISPOSE
    // =========================================================

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _stream?.Dispose();
        _tcpClient?.Dispose();

        _stream = null;
        _tcpClient = null;

        _connected = false;
    }
}

// =============================================================
// REALTIME MODEL
// =============================================================

public class ZkRealtimeEvent
{
    public int EventCode { get; set; }

    public string EventName { get; set; } =
        string.Empty;

    public byte[] Data { get; set; } =
        [];

    public bool IsAttendance { get; set; }

    public string UserId { get; set; } =
        string.Empty;

    public DateTime? Timestamp { get; set; }
}