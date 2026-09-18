using System.Buffers.Binary;
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
    private ushort _replyId;

    private bool _connected;

    public ZkClient(string ip, int port = 4370)
    {
        _ip = ip;
        _port = port;
    }

    // ============================================================
    // CONNECT
    // ============================================================

    public async Task ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        if (_connected)
            return;

        _tcpClient = new TcpClient();

        await _tcpClient.ConnectAsync(
            _ip,
            _port,
            cancellationToken);

        _stream = _tcpClient.GetStream();

        Console.WriteLine(
            $"TCP conectado a {_ip}:{_port}");

        // CONNECT utiliza ReplyId 0.
        _replyId = 0;

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_CONNECT,
                [],
                cancellationToken,
                incrementReply: false);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"El K30 rechazó CONNECT. " +
                $"Command={response.Command}");
        }

        _sessionId = response.SessionId;

        // El primer comando después de CONNECT
        // también utiliza ReplyId 0.
        _replyId = 0;

        _connected = true;

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
        if (!_connected)
        {
            CleanupSocket();
            return;
        }

        try
        {
            Console.WriteLine();
            Console.WriteLine(
                "Desconectando del K30...");

            ZkResponse response =
                await SendAsync(
                    ZkProtocol.CMD_EXIT,
                    [],
                    CancellationToken.None);

            Console.WriteLine(
                $"EXIT response: {response}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error durante EXIT: {ex.Message}");
        }
        finally
        {
            _connected = false;

            CleanupSocket();

            Console.WriteLine(
                "Conexión finalizada.");
        }
    }

    public void Dispose()
    {
        CleanupSocket();
    }

    private void CleanupSocket()
    {
        try
        {
            _stream?.Close();
        }
        catch
        {
        }

        try
        {
            _tcpClient?.Close();
        }
        catch
        {
        }

        _stream = null;
        _tcpClient = null;
        _connected = false;
    }

    // ============================================================
    // DEVICE OPTIONS
    // ============================================================

    public async Task<string> GetOptionAsync(
        string option,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        Console.WriteLine(
            $"Consultando opción: {option}");

        byte[] data =
            Encoding.ASCII.GetBytes(
                option + "\0");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_OPTIONS_RRQ,
                data,
                cancellationToken);

        Console.WriteLine(
            $"Option response: Command={response.Command}");

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo obtener la opción '{option}'. " +
                $"Command={response.Command}");
        }

        string result =
            Encoding.ASCII
                .GetString(response.Data)
                .TrimEnd('\0');

        Console.WriteLine(
            $"Option result: {result}");

        return result;
    }

    // ============================================================
    // DISABLE DEVICE
    // ============================================================

    public async Task DisableDeviceAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        Console.WriteLine(
            "Deshabilitando temporalmente el K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_DISABLE_DEVICE,
                [],
                cancellationToken);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo deshabilitar el K30. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "K30 deshabilitado temporalmente.");
    }

    // ============================================================
    // ENABLE DEVICE
    // ============================================================

    public async Task EnableDeviceAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        Console.WriteLine(
            "Habilitando nuevamente el K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_ENABLE_DEVICE,
                [],
                cancellationToken);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"No se pudo habilitar el K30. " +
                $"Command={response.Command}");
        }

        Console.WriteLine(
            "K30 habilitado nuevamente.");
    }

    // ============================================================
    // ATTENDANCE
    // ============================================================

    public async Task<List<AttendanceRecord>>
        ReadAttendanceAsync(
            CancellationToken cancellationToken = default)
    {
        byte[] raw =
            await ReadAttendanceRawAsync(
                cancellationToken);

        return ParseAttendanceRecords(raw);
    }

    // ============================================================
    // READ ATTENDANCE RAW
    // ============================================================

    public async Task<byte[]> ReadAttendanceRawAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        bool disabled = false;

        try
        {
            await DisableDeviceAsync(
                cancellationToken);

            disabled = true;

            Console.WriteLine();
            Console.WriteLine(
                "Solicitando historial de asistencias...");

            /*
             * Este es el comando que el K30 ya aceptó.
             */
            ZkResponse response =
                await SendAsync(
                    ZkProtocol.CMD_ATTLOG_RRQ,
                    [],
                    cancellationToken);

            Console.WriteLine(
                $"Attendance response: Command={response.Command}");

            Console.WriteLine(
                $"Attendance data length={response.Data.Length}");

            // ----------------------------------------------------
            // DATA DIRECTO
            // ----------------------------------------------------

            if (response.Command ==
                ZkProtocol.CMD_DATA)
            {
                Console.WriteLine(
                    "El K30 devolvió los datos directamente.");

                return ExtractAttendancePayload(
                    response.Data);
            }

            // ----------------------------------------------------
            // PREPARE DATA
            // ----------------------------------------------------

            if (response.Command ==
                ZkProtocol.CMD_PREPARE_DATA)
            {
                uint preparedSize =
                    ReadPreparedSize(response.Data);

                Console.WriteLine(
                    "CMD_PREPARE_DATA recibido.");

                Console.WriteLine(
                    $"Tamaño anunciado por K30: {preparedSize} bytes");

                byte[] completeData =
                    await ReadDataPacketsAsync(
                        preparedSize,
                        cancellationToken);

                return ExtractAttendancePayload(
                    completeData);
            }

            throw new InvalidOperationException(
                $"Respuesta inesperada al solicitar asistencias. " +
                $"Command={response.Command}");
        }
        finally
        {
            try
            {
                await FreeDataAsync(
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Aviso FREE_DATA: {ex.Message}");
            }

            if (disabled)
            {
                try
                {
                    await EnableDeviceAsync(
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Aviso ENABLE: {ex.Message}");
                }
            }
        }
    }

    // ============================================================
    // READ DATA
    // ============================================================

    private async Task<byte[]> ReadDataPacketsAsync(
        uint expectedSize,
        CancellationToken cancellationToken)
    {
        using MemoryStream memory =
            new MemoryStream();

        Console.WriteLine();
        Console.WriteLine(
            "Recibiendo bloque de datos del K30...");

        while (memory.Length < expectedSize)
        {
            ZkPacket packet =
                await ReadPacketAsync(
                    cancellationToken);

            Console.WriteLine(
                $"DATA RX: Command={packet.Command}, " +
                $"DataLength={packet.Data.Length}, " +
                $"ReplyId={packet.ReplyId}");

            if (packet.Command ==
                ZkProtocol.CMD_DATA)
            {
                await memory.WriteAsync(
                    packet.Data,
                    cancellationToken);

                continue;
            }

            if (packet.Command ==
                ZkProtocol.CMD_ACK_OK)
            {
                break;
            }

            if (packet.Command ==
                ZkProtocol.CMD_ACK_ERROR)
            {
                throw new InvalidOperationException(
                    "El K30 devolvió CMD_ACK_ERROR " +
                    "durante la transferencia de datos.");
            }

            throw new InvalidOperationException(
                $"Respuesta inesperada durante DATA: " +
                $"Command={packet.Command}");
        }

        byte[] result =
            memory.ToArray();

        Console.WriteLine(
            $"Bytes recibidos: {result.Length}");

        return result;
    }

    // ============================================================
    // FREE DATA
    // ============================================================

    private async Task FreeDataAsync(
        CancellationToken cancellationToken)
    {
        if (!_connected || _stream == null)
            return;

        Console.WriteLine();
        Console.WriteLine(
            "Liberando buffer de datos del K30...");

        ZkResponse response =
            await SendAsync(
                ZkProtocol.CMD_FREE_DATA,
                [],
                cancellationToken);

        Console.WriteLine(
            $"FREE_DATA response: Command={response.Command}");
    }

    // ============================================================
    // DIAGNOSTIC PARSER
    // ============================================================

    private static List<AttendanceRecord>
        ParseAttendanceRecords(byte[] data)
    {
        List<AttendanceRecord> records =
            new();

        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "       DIAGNOSTICO DEL ATTLOG");

        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"Bytes totales: {data.Length}");

        if (data.Length < 4)
        {
            Console.WriteLine(
                "El bloque no contiene los 4 bytes iniciales.");

            return records;
        }

        uint declaredSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                data.AsSpan(0, 4));

        Console.WriteLine(
            $"Tamaño declarado: {declaredSize}");

        const int recordSize = 40;

        int availableBytes =
            data.Length - 4;

        int recordCount =
            availableBytes / recordSize;

        Console.WriteLine(
            $"Tamaño de registro asumido: {recordSize} bytes");

        Console.WriteLine(
            $"Cantidad de registros: {recordCount}");

        Console.WriteLine();

        /*
         * IMPORTANTE:
         *
         * En esta versión NO vamos a afirmar que los
         * offsets corresponden al formato estándar.
         *
         * Primero mostramos exactamente qué contiene
         * cada registro.
         */

        for (int i = 0; i < recordCount; i++)
        {
            int offset =
                4 + (i * recordSize);

            byte[] record =
                new byte[recordSize];

            Array.Copy(
                data,
                offset,
                record,
                0,
                recordSize);

            PrintDiagnosticRecord(
                i + 1,
                offset,
                record);

            /*
             * Creamos el objeto únicamente para mantener
             * compatible ReadAttendanceAsync().
             *
             * Los campos todavía NO se consideran válidos.
             */
            records.Add(
                new AttendanceRecord
                {
                    RecordNumber = i + 1,
                    UserId = string.Empty,
                    UserName = string.Empty,
                    Timestamp = DateTime.MinValue,
                    VerifyMode = -1,
                    Status = -1,
                    RawData = record
                });
        }

        return records;
    }

    // ============================================================
    // DIAGNOSTIC RECORD
    // ============================================================

    private static void PrintDiagnosticRecord(
        int number,
        int offset,
        byte[] record)
    {
        Console.WriteLine(
            $"---------------- REGISTRO {number} ----------------");

        Console.WriteLine(
            $"Offset dentro del bloque: {offset}");

        Console.WriteLine(
            $"Longitud: {record.Length} bytes");

        Console.WriteLine();

        Console.WriteLine(
            "OFFSET : " +
            "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F " +
            "10 11 12 13 14 15 16 17 18 19 1A 1B 1C 1D 1E 1F " +
            "20 21 22 23 24 25 26 27");

        Console.WriteLine(
            "HEX    : " +
            FormatHex(record));

        Console.WriteLine(
            "ASCII  : " +
            FormatAscii(record));

        Console.WriteLine();

        /*
         * Mostramos también los valores numéricos
         * de cada posición.
         */

        Console.WriteLine(
            "BYTES  :");

        for (int start = 0;
             start < record.Length;
             start += 8)
        {
            int count =
                Math.Min(8, record.Length - start);

            string values =
                string.Join(
                    " ",
                    record
                        .Skip(start)
                        .Take(count)
                        .Select(
                            b => b.ToString("D3")));

            Console.WriteLine(
                $"  [{start:D2}-{start + count - 1:D2}] {values}");
        }

        Console.WriteLine();

        /*
         * Valores little-endian que podrían resultar
         * útiles para detectar campos numéricos.
         */

        Console.WriteLine(
            "UINT16 LE:");

        for (int i = 0;
             i + 1 < record.Length;
             i += 2)
        {
            ushort value =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    record.AsSpan(i, 2));

            Console.Write(
                $"[{i:D2}]={value}  ");

            if ((i / 2 + 1) % 6 == 0)
                Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine();
    }

    private static string FormatHex(
        byte[] data)
    {
        return string.Join(
            " ",
            data.Select(
                b => b.ToString("X2")));
    }

    private static string FormatAscii(
        byte[] data)
    {
        StringBuilder result =
            new StringBuilder(data.Length);

        foreach (byte b in data)
        {
            if (b >= 32 && b <= 126)
                result.Append((char)b);
            else
                result.Append('.');
        }

        return result.ToString();
    }

    // ============================================================
    // PREPARE SIZE
    // ============================================================

    private static uint ReadPreparedSize(
        byte[] data)
    {
        if (data.Length < 4)
        {
            throw new InvalidOperationException(
                "CMD_PREPARE_DATA no contiene información suficiente.");
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(
            data.AsSpan(0, 4));
    }

    // ============================================================
    // EXTRACT ATTENDANCE PAYLOAD
    // ============================================================

    private static byte[] ExtractAttendancePayload(
        byte[] data)
    {
        if (data.Length < 4)
        {
            throw new InvalidOperationException(
                "El bloque de asistencia no contiene " +
                "el tamaño inicial.");
        }

        uint declaredSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                data.AsSpan(0, 4));

        Console.WriteLine(
            $"Tamaño interno del bloque: {declaredSize}");

        if (declaredSize == 0)
        {
            return data;
        }

        int available =
            data.Length - 4;

        int expected =
            (int)Math.Min(
                declaredSize,
                (uint)available);

        byte[] result =
            new byte[4 + expected];

        Array.Copy(
            data,
            0,
            result,
            0,
            result.Length);

        return result;
    }

    // ============================================================
    // SEND
    // ============================================================

    private async Task<ZkResponse> SendAsync(
        ushort command,
        byte[] data,
        CancellationToken cancellationToken,
        bool incrementReply = true)
    {
        EnsureSocket();

        ushort currentReplyId =
            _replyId;

        ZkPacket packet = new()
        {
            Command = command,
            SessionId = _sessionId,
            ReplyId = currentReplyId,
            Data = data ?? []
        };

        byte[] encoded =
            packet.Encode();

        Console.WriteLine(
            $"TX: {Convert.ToHexString(encoded)}");

        await _stream!.WriteAsync(
            encoded,
            cancellationToken);

        await _stream.FlushAsync(
            cancellationToken);

        ZkPacket responsePacket =
            await ReadPacketAsync(
                cancellationToken);

        Console.WriteLine(
            $"RX: {Convert.ToHexString(responsePacket.Encode())}");

        ZkResponse response = new()
        {
            Command = responsePacket.Command,
            Checksum = responsePacket.Checksum,
            SessionId = responsePacket.SessionId,
            ReplyId = responsePacket.ReplyId,
            Data = responsePacket.Data
        };

        if (incrementReply)
        {
            _replyId++;
        }

        return response;
    }

    // ============================================================
    // READ PACKET
    // ============================================================

    private async Task<ZkPacket> ReadPacketAsync(
        CancellationToken cancellationToken)
    {
        EnsureSocket();

        byte[] header =
            await ReadExactAsync(
                8,
                cancellationToken);

        if (header[0] != ZkProtocol.Header1 ||
            header[1] != ZkProtocol.Header2 ||
            header[2] != ZkProtocol.Header3 ||
            header[3] != ZkProtocol.Header4)
        {
            throw new InvalidOperationException(
                "Header ZKTeco inválido recibido.");
        }

        uint payloadSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                header.AsSpan(4, 4));

        if (payloadSize < 8)
        {
            throw new InvalidOperationException(
                $"Payload inválido: {payloadSize}");
        }

        byte[] payload =
            await ReadExactAsync(
                checked((int)payloadSize),
                cancellationToken);

        byte[] completePacket =
            new byte[8 + payload.Length];

        header.CopyTo(
            completePacket,
            0);

        payload.CopyTo(
            completePacket,
            8);

        return ZkPacket.Decode(
            completePacket);
    }

    // ============================================================
    // READ EXACT
    // ============================================================

    private async Task<byte[]> ReadExactAsync(
        int count,
        CancellationToken cancellationToken)
    {
        EnsureSocket();

        byte[] buffer =
            new byte[count];

        int totalRead = 0;

        while (totalRead < count)
        {
            int read =
                await _stream!.ReadAsync(
                    buffer.AsMemory(
                        totalRead,
                        count - totalRead),
                    cancellationToken);

            if (read == 0)
            {
                throw new IOException(
                    "El K30 cerró la conexión.");
            }

            totalRead += read;
        }

        return buffer;
    }

    // ============================================================
    // VALIDATION
    // ============================================================

    private void EnsureConnected()
    {
        if (!_connected ||
            _tcpClient == null ||
            _stream == null)
        {
            throw new InvalidOperationException(
                "El K30 no está conectado.");
        }
    }

    private void EnsureSocket()
    {
        if (_tcpClient == null ||
            _stream == null)
        {
            throw new InvalidOperationException(
                "El socket ZKTeco no está disponible.");
        }
    }
}