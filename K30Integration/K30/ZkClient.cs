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
    private readonly int _timeoutMs;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    private ushort _sessionId;
    private ushort _replyId;

    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected =>
        _tcpClient?.Connected == true &&
        _stream != null;

    public ushort SessionId => _sessionId;

    public ZkClient(
        string ip,
        int port = 4370,
        int timeoutMs = 5000)
    {
        _ip = ip;
        _port = port;
        _timeoutMs = timeoutMs;
    }

    // =========================================================
    // CONNECT
    // =========================================================

    public async Task ConnectAsync()
    {
        await _lock.WaitAsync();

        try
        {
            if (IsConnected)
                return;

            _tcpClient?.Dispose();

            _tcpClient = new TcpClient();

            using CancellationTokenSource cts =
                new(_timeoutMs);

            await _tcpClient.ConnectAsync(
                IPAddress.Parse(_ip),
                _port,
                cts.Token);

            _stream = _tcpClient.GetStream();

            _sessionId = 0;
            _replyId = 0;

            ZkResponse response =
                await SendCommandCoreAsync(
                    ZkProtocol.CMD_CONNECT);

            if (!response.IsOk)
            {
                throw new InvalidOperationException(
                    $"El K30 rechazó CONNECT. " +
                    $"Command={response.Command}");
            }

            _sessionId = response.SessionId;

            Console.WriteLine(
                $"Conectado. SessionId={_sessionId}");
        }
        finally
        {
            _lock.Release();
        }
    }

    // =========================================================
    // DISCONNECT
    // =========================================================

    public async Task DisconnectAsync()
    {
        await _lock.WaitAsync();

        try
        {
            if (_stream != null)
            {
                try
                {
                    await SendCommandCoreAsync(
                        ZkProtocol.CMD_EXIT);
                }
                catch
                {
                }
            }
        }
        finally
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();

            _stream = null;
            _tcpClient = null;

            _sessionId = 0;
            _replyId = 0;

            _lock.Release();
        }
    }

    // =========================================================
    // COMANDO PÚBLICO
    // =========================================================

    public async Task<ZkResponse> SendCommandAsync(
        ushort command,
        byte[]? data = null)
    {
        await _lock.WaitAsync();

        try
        {
            return await SendCommandCoreAsync(
                command,
                data);
        }
        finally
        {
            _lock.Release();
        }
    }

    // =========================================================
    // COMANDO INTERNO
    // =========================================================

    private async Task<ZkResponse> SendCommandCoreAsync(
        ushort command,
        byte[]? data = null)
    {
        EnsureConnected();

        ZkPacket packet = new()
        {
            Command = command,
            SessionId = _sessionId,
            ReplyId = _replyId,
            Data = data ?? []
        };

        byte[] encoded =
            packet.Encode();

        Console.WriteLine(
            $"TX Command={command}, " +
            $"Length={encoded.Length}, " +
            $"DataLength={packet.Data.Length}");

        await _stream!.WriteAsync(encoded);

        ZkPacket response =
            await ReceivePacketAsync();

        Console.WriteLine(
            $"RX Command={response.Command}, " +
            $"DataLength={response.Data.Length}");

        _replyId =
            unchecked((ushort)(_replyId + 1));

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
    // FIRMWARE
    // =========================================================

    public async Task<string?> GetFirmwareAsync()
    {
        ZkResponse response =
            await SendCommandAsync(
                ZkProtocol.CMD_GET_VERSION);

        if (!response.IsOk)
            return null;

        return DecodeString(response.Data);
    }

    // =========================================================
    // OPTIONS
    // =========================================================

    public async Task<string?> GetOptionAsync(
        string option)
    {
        byte[] data =
            Encoding.ASCII.GetBytes(
                option + "\0");

        ZkResponse response =
            await SendCommandAsync(
                ZkProtocol.CMD_OPTIONS_RRQ,
                data);

        if (!response.IsOk)
            return null;

        return DecodeString(response.Data);
    }

    public async Task<Dictionary<string, string?>>
        GetDeviceOptionsAsync()
    {
        Dictionary<string, string?> result = [];

        string[] options =
        [
            "~SerialNumber",
            "~DeviceName",
            "~Platform"
        ];

        foreach (string option in options)
        {
            result[option] =
                await GetOptionAsync(option);
        }

        return result;
    }

    // =========================================================
    // CAPACIDAD
    // =========================================================

    public async Task<DeviceCapacity>
        GetFreeSizesAsync()
    {
        return await GetCapacityAsync();
    }

    public async Task<DeviceCapacity>
        GetCapacityAsync()
    {
        ZkResponse response =
            await SendCommandAsync(
                ZkProtocol.CMD_GET_FREE_SIZES);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo obtener la capacidad. " +
                $"Command={response.Command}");
        }

        if (response.Data.Length < 32)
        {
            throw new InvalidOperationException(
                $"Respuesta de capacidad inválida. " +
                $"DataLength={response.Data.Length}");
        }

        return new DeviceCapacity
        {
            UserCount =
                ReadUInt32(response.Data, 0),

            UserCapacity =
                ReadUInt32(response.Data, 4),

            FingerprintCount =
                ReadUInt32(response.Data, 8),

            FingerprintCapacity =
                ReadUInt32(response.Data, 12),

            AttendanceCount =
                ReadUInt32(response.Data, 16),

            AttendanceCapacity =
                ReadUInt32(response.Data, 20),

            Unknown1 =
                ReadUInt32(response.Data, 24),

            Unknown2 =
                ReadUInt32(response.Data, 28)
        };
    }

    // =========================================================
    // ESTADO
    // =========================================================

    public async Task<DeviceState>
        GetStateAsync()
    {
        ZkResponse response =
            await SendCommandAsync(
                ZkProtocol.CMD_STATE_RRQ);

        if (!response.IsOk ||
            response.Data.Length < 4)
        {
            return DeviceState.Unknown;
        }

        uint state =
            ReadUInt32(response.Data, 0);

        return state switch
        {
            0 => DeviceState.Waiting,
            1 => DeviceState.FingerprintRegistration,
            2 => DeviceState.FingerprintIdentification,
            3 => DeviceState.Menu,
            4 => DeviceState.Busy,
            5 => DeviceState.CardWriting,
            _ => DeviceState.Unknown
        };
    }

    // =========================================================
    // DISABLE
    // =========================================================

    public async Task DisableDeviceAsync()
    {
        ZkResponse response =
            await SendCommandAsync(
                ZkProtocol.CMD_DISABLE_DEVICE);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo deshabilitar el dispositivo. " +
                $"Command={response.Command}");
        }
    }

    // =========================================================
    // ENABLE
    // =========================================================

    public async Task EnableDeviceAsync()
    {
        ZkResponse response =
            await SendCommandAsync(
                ZkProtocol.CMD_ENABLE_DEVICE);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo habilitar el dispositivo. " +
                $"Command={response.Command}");
        }
    }

    // =========================================================
    // LEER USUARIOS
    // =========================================================

    public async Task<List<K30User>>
        ReadUsersAsync()
    {
        await _lock.WaitAsync();

        bool disabled = false;

        try
        {
            EnsureConnected();

            Console.WriteLine(
                "Deshabilitando dispositivo para leer usuarios...");

            ZkResponse disable =
                await SendCommandCoreAsync(
                    ZkProtocol.CMD_DISABLE_DEVICE);

            if (!disable.IsOk)
            {
                throw new InvalidOperationException(
                    $"No se pudo deshabilitar el K30. " +
                    $"Command={disable.Command}");
            }

            disabled = true;

            byte[] request =
            [
                0x01,
                0x09,
                0x00,
                0x05,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            ];

            Console.WriteLine(
                "Solicitando usuarios...");

            ZkResponse response =
                await SendCommandCoreAsync(
                    ZkProtocol.CMD_DATA_WRRQ,
                    request);

            if (response.Command !=
                ZkProtocol.CMD_DATA)
            {
                throw new InvalidOperationException(
                    $"Respuesta inesperada leyendo usuarios. " +
                    $"Command={response.Command}, " +
                    $"DataLength={response.Data.Length}");
            }

            Console.WriteLine(
                $"Bytes recibidos usuarios: " +
                $"{response.Data.Length}");

            List<K30User> users =
                ParseUsers(response.Data);

            Console.WriteLine(
                $"Usuarios recibidos: {users.Count}");

            return users;
        }
        finally
        {
            if (disabled)
            {
                try
                {
                    Console.WriteLine(
                        "Habilitando dispositivo...");

                    await SendCommandCoreAsync(
                        ZkProtocol.CMD_ENABLE_DEVICE);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Advertencia: {ex.Message}");
                }
            }

            _lock.Release();
        }
    }

    // =========================================================
    // ATTENDANCE RAW
    // =========================================================

    public async Task<byte[]>
        ReadAttendanceRawDiscoveryAsync()
    {
        return await ReadAttendanceRawDiscoveryAsync(
            CancellationToken.None);
    }

    public async Task<byte[]>
        ReadAttendanceRawDiscoveryAsync(
            CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        bool disabled = false;

        try
        {
            EnsureConnected();

            Console.WriteLine(
                "Deshabilitando dispositivo para leer asistencias...");

            ZkResponse disable =
                await SendCommandCoreAsync(
                    ZkProtocol.CMD_DISABLE_DEVICE);

            if (!disable.IsOk)
            {
                throw new InvalidOperationException(
                    $"No se pudo deshabilitar el K30. " +
                    $"Command={disable.Command}");
            }

            disabled = true;

            byte[] request =
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

            Console.WriteLine(
                "Solicitando historial de asistencias...");

            ZkResponse response =
                await SendCommandCoreAsync(
                    ZkProtocol.CMD_DATA_WRRQ,
                    request);

            if (response.Command !=
                ZkProtocol.CMD_DATA)
            {
                throw new InvalidOperationException(
                    $"Respuesta inesperada leyendo asistencias. " +
                    $"Command={response.Command}, " +
                    $"DataLength={response.Data.Length}");
            }

            Console.WriteLine(
                $"Bytes recibidos ATTLOG: " +
                $"{response.Data.Length}");

            return response.Data;
        }
        finally
        {
            if (disabled)
            {
                try
                {
                    await SendCommandCoreAsync(
                        ZkProtocol.CMD_ENABLE_DEVICE);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Advertencia habilitando K30: " +
                        $"{ex.Message}");
                }
            }

            _lock.Release();
        }
    }

    // =========================================================
    // LEER ATTENDANCE COMPLETO
    // =========================================================

    public async Task<List<AttendanceRecord>>
        ReadAttendanceAsync(
            IReadOnlyList<K30User> users)
    {
        byte[] raw =
            await ReadAttendanceRawDiscoveryAsync();

        return ParseAttendance(
            raw,
            users);
    }

    // =========================================================
    // PARSER USUARIOS
    // =========================================================

    private static List<K30User>
        ParseUsers(
            byte[] data)
    {
        List<K30User> users = [];

        if (data.Length < 4)
            return users;

        uint declaredSize =
            ReadUInt32(data, 0);

        int available =
            data.Length - 4;

        int usable =
            Math.Min(
                checked((int)declaredSize),
                available);

        const int recordSize = 72;

        int count =
            usable / recordSize;

        Console.WriteLine(
            $"Tamaño declarado usuarios: " +
            $"{declaredSize}");

        Console.WriteLine(
            $"Registros de usuarios: {count}");

        for (int i = 0; i < count; i++)
        {
            int offset =
                4 + (i * recordSize);

            ReadOnlySpan<byte> record =
                data.AsSpan(
                    offset,
                    recordSize);

            K30User user = new()
            {
                InternalNumber =
                    BinaryPrimitives.ReadUInt16LittleEndian(
                        record.Slice(0, 2)),

                Permission =
                    record[2],

                Password =
                    DecodeFixedString(
                        record.Slice(3, 8)),

                Name =
                    DecodeFixedString(
                        record.Slice(11, 24)),

                CardNumber =
                    BinaryPrimitives.ReadUInt32LittleEndian(
                        record.Slice(35, 4)),

                Group =
                    record[39],

                UserIdTimezone1 =
                    record[42],

                UserIdTimezone2 =
                    record[44],

                UserId =
                    DecodeFixedString(
                        record.Slice(48, 9)),

                RawData =
                    record.ToArray()
            };

            users.Add(user);
        }

        return users;
    }

    // =========================================================
    // PARSER ATTENDANCE
    // =========================================================

    public static List<AttendanceRecord>
        ParseAttendance(
            byte[] data,
            IReadOnlyList<K30User> users)
    {
        List<AttendanceRecord> records = [];

        if (data.Length < 4)
            return records;

        uint declaredSize =
            ReadUInt32(data, 0);

        int available =
            data.Length - 4;

        int usable =
            Math.Min(
                checked((int)declaredSize),
                available);

        const int recordSize = 40;

        int count =
            usable / recordSize;

        Console.WriteLine(
            $"Tamaño declarado ATTLOG: " +
            $"{declaredSize}");

        Console.WriteLine(
            $"Bytes disponibles: " +
            $"{available}");

        Console.WriteLine(
            $"Registros ATTLOG: " +
            $"{count}");

        for (int i = 0; i < count; i++)
        {
            int offset =
                4 + (i * recordSize);

            ReadOnlySpan<byte> record =
                data.AsSpan(
                    offset,
                    recordSize);

            string userId =
                DecodeFixedString(
                    record.Slice(2, 9));

            int verifyMode =
                record[26];

            uint packedTime =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(27, 4));

            int status =
                record[31];

            DateTime timestamp =
                DecodeZkTime(
                    packedTime);

            K30User? user =
                users.FirstOrDefault(
                    x =>
                        string.Equals(
                            x.UserId,
                            userId,
                            StringComparison.OrdinalIgnoreCase));

            AttendanceRecord attendance =
                new()
                {
                    RecordNumber = i + 1,

                    UserId = userId,

                    UserName =
                        user?.Name ??
                        string.Empty,

                    Timestamp = timestamp,

                    VerifyMode = verifyMode,

                    Status = status,

                    RawData = record.ToArray()
                };

            records.Add(attendance);
        }

        return records;
    }

    // =========================================================
    // TIEMPO ZK
    // =========================================================

    private static DateTime
        DecodeZkTime(
            uint value)
    {
        try
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

            value /= 12;

            int year =
                (int)value + 2000;

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
    // RECIBIR PAQUETE
    // =========================================================

    private async Task<ZkPacket>
        ReceivePacketAsync()
    {
        EnsureConnected();

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
                $"Payload inválido: {payloadSize}");
        }

        if (payloadSize > 10_000_000)
        {
            throw new InvalidOperationException(
                $"Payload demasiado grande: {payloadSize}");
        }

        byte[] payload =
            await ReadExactAsync(
                checked((int)payloadSize));

        byte[] packet =
            new byte[
                checked(8 + payload.Length)];

        header.CopyTo(
            packet,
            0);

        payload.CopyTo(
            packet,
            8);

        return ZkPacket.Decode(packet);
    }

    // =========================================================
    // READ EXACT
    // =========================================================

    private async Task<byte[]>
        ReadExactAsync(
            int length)
    {
        byte[] buffer =
            new byte[length];

        int totalRead = 0;

        using CancellationTokenSource cts =
            new(_timeoutMs);

        while (totalRead < length)
        {
            int read =
                await _stream!.ReadAsync(
                    buffer.AsMemory(
                        totalRead,
                        length - totalRead),
                    cts.Token);

            if (read == 0)
            {
                throw new IOException(
                    "El K30 cerró la conexión.");
            }

            totalRead += read;
        }

        return buffer;
    }

    // =========================================================
    // STRING
    // =========================================================

    private static string
        DecodeString(
            byte[] data)
    {
        if (data.Length == 0)
            return string.Empty;

        int length =
            Array.IndexOf(
                data,
                (byte)0);

        if (length < 0)
            length = data.Length;

        return Encoding.ASCII
            .GetString(
                data,
                0,
                length)
            .Trim();
    }

    private static string
        DecodeFixedString(
            ReadOnlySpan<byte> data)
    {
        int length = 0;

        while (length < data.Length &&
               data[length] != 0)
        {
            length++;
        }

        return Encoding.ASCII
            .GetString(
                data[..length])
            .Trim();
    }

    // =========================================================
    // UINT32
    // =========================================================

    private static uint
        ReadUInt32(
            byte[] data,
            int offset)
    {
        return BinaryPrimitives
            .ReadUInt32LittleEndian(
                data.AsSpan(
                    offset,
                    4));
    }

    // =========================================================
    // VALIDAR CONEXIÓN
    // =========================================================

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException(
                "No existe conexión con el K30.");
        }
    }

    // =========================================================
    // DISPOSE
    // =========================================================

    public void Dispose()
    {
        try
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }
        finally
        {
            _stream = null;
            _tcpClient = null;

            _lock.Dispose();
        }
    }
}