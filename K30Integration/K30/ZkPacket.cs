using System.Buffers.Binary;

namespace K30Integration.K30;

public class ZkPacket
{
    private const int TcpHeaderSize = 8;
    private const int PacketHeaderSize = 8;

    public ushort Command { get; set; }

    public ushort Checksum { get; set; }

    public ushort SessionId { get; set; }

    public ushort ReplyId { get; set; }

    public byte[] Data { get; set; } = [];


    // ============================================================
    // ENCODE
    // ============================================================

    public byte[] Encode()
    {
        byte[] data = Data ?? [];

        int payloadSize = PacketHeaderSize + data.Length;

        byte[] payload = new byte[payloadSize];

        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(0, 2),
            Command);

        // Checksum = 0 while calculating checksum.
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(2, 2),
            0);

        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(4, 2),
            SessionId);

        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(6, 2),
            ReplyId);

        if (data.Length > 0)
        {
            data.CopyTo(payload, 8);
        }

        Checksum = CalculateChecksum(payload);

        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(2, 2),
            Checksum);

        byte[] packet =
            new byte[TcpHeaderSize + payload.Length];

        packet[0] = ZkProtocol.Header1;
        packet[1] = ZkProtocol.Header2;
        packet[2] = ZkProtocol.Header3;
        packet[3] = ZkProtocol.Header4;

        BinaryPrimitives.WriteUInt32LittleEndian(
            packet.AsSpan(4, 4),
            (uint)payload.Length);

        payload.CopyTo(packet, TcpHeaderSize);

        return packet;
    }


    // ============================================================
    // DECODE
    // ============================================================

    public static ZkPacket Decode(byte[] packet)
    {
        if (packet == null)
            throw new ArgumentNullException(nameof(packet));

        if (packet.Length < 16)
        {
            throw new ArgumentException(
                "El paquete ZKTeco es demasiado pequeño.");
        }

        if (packet[0] != ZkProtocol.Header1 ||
            packet[1] != ZkProtocol.Header2 ||
            packet[2] != ZkProtocol.Header3 ||
            packet[3] != ZkProtocol.Header4)
        {
            throw new ArgumentException(
                "Header ZKTeco inválido.");
        }

        uint payloadSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                packet.AsSpan(4, 4));

        if (payloadSize < 8)
        {
            throw new ArgumentException(
                $"Payload ZKTeco inválido: {payloadSize}");
        }

        long totalSize =
            TcpHeaderSize + (long)payloadSize;

        if (packet.Length < totalSize)
        {
            throw new ArgumentException(
                $"Paquete ZKTeco incompleto. " +
                $"Esperado: {totalSize}, recibido: {packet.Length}");
        }

        ushort command =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(8, 2));

        ushort checksum =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(10, 2));

        ushort sessionId =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(12, 2));

        ushort replyId =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(14, 2));

        int dataLength =
            checked((int)payloadSize - PacketHeaderSize);

        byte[] data = [];

        if (dataLength > 0)
        {
            data = new byte[dataLength];

            Array.Copy(
                packet,
                16,
                data,
                0,
                dataLength);
        }

        return new ZkPacket
        {
            Command = command,
            Checksum = checksum,
            SessionId = sessionId,
            ReplyId = replyId,
            Data = data
        };
    }


    // ============================================================
    // CHECKSUM
    // ============================================================

    private static ushort CalculateChecksum(byte[] payload)
    {
        if (payload.Length < 4)
            throw new ArgumentException(
                "Payload demasiado pequeño para checksum.");

        uint checksum = 0;

        int length = payload.Length;

        int offset = 0;

        while (offset + 1 < length)
        {
            ushort value =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    payload.AsSpan(offset, 2));

            // Offset 2-3 = checksum field.
            if (offset != 2)
            {
                checksum += value;
            }

            offset += 2;
        }

        // Odd payload: last byte is considered as a low byte
        // with a zero high byte ONLY for checksum calculation.
        if (offset < length)
        {
            checksum += payload[offset];
        }

        // Fold 32-bit sum into 16 bits.
        checksum =
            (checksum & 0xFFFF) +
            (checksum >> 16);

        checksum =
            (checksum & 0xFFFF) +
            (checksum >> 16);

        checksum = (~checksum) & 0xFFFF;

        return (ushort)checksum;
    }
}