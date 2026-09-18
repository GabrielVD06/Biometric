using System.Buffers.Binary;

namespace K30Integration.K30;

public class ZkPacket
{
    public ushort Command { get; set; }

    public ushort Checksum { get; set; }

    public ushort SessionId { get; set; }

    public ushort ReplyId { get; set; }

    public byte[] Data { get; set; } = [];

    private const int HeaderSize = 8;
    private const int PayloadHeaderSize = 8;

    public byte[] Encode()
    {
        byte[] data = Data ?? [];

        // El protocolo utiliza pares de bytes.
        if (data.Length % 2 != 0)
        {
            Array.Resize(ref data, data.Length + 1);
        }

        int payloadSize = PayloadHeaderSize + data.Length;

        byte[] payload = new byte[payloadSize];

        // Command
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(0, 2),
            Command
        );

        // Checksum inicialmente en 0
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(2, 2),
            0
        );

        // Session ID
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(4, 2),
            SessionId
        );

        // Reply ID
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(6, 2),
            ReplyId
        );

        // Data
        data.CopyTo(payload, 8);

        // Calcular checksum
        Checksum = CalculateChecksum(payload);

        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(2, 2),
            Checksum
        );

        // Crear paquete completo
        byte[] packet = new byte[HeaderSize + payload.Length];

        // Header
        packet[0] = ZkProtocol.Header1;
        packet[1] = ZkProtocol.Header2;
        packet[2] = ZkProtocol.Header3;
        packet[3] = ZkProtocol.Header4;

        // Tamaño del payload
        BinaryPrimitives.WriteUInt32LittleEndian(
            packet.AsSpan(4, 4),
            (uint)payload.Length
        );

        // Payload
        payload.CopyTo(packet, HeaderSize);

        return packet;
    }

    public static ZkPacket Decode(byte[] packet)
    {
        if (packet.Length < 16)
        {
            throw new ArgumentException(
                "El paquete ZKTeco es demasiado pequeño."
            );
        }

        // Verificar Header
        if (packet[0] != ZkProtocol.Header1 ||
            packet[1] != ZkProtocol.Header2 ||
            packet[2] != ZkProtocol.Header3 ||
            packet[3] != ZkProtocol.Header4)
        {
            throw new ArgumentException(
                "Header ZKTeco inválido."
            );
        }

        uint payloadSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                packet.AsSpan(4, 4)
            );

        if (packet.Length < 8 + payloadSize)
        {
            throw new ArgumentException(
                "El paquete está incompleto."
            );
        }

        ushort command =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(8, 2)
            );

        ushort checksum =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(10, 2)
            );

        ushort sessionId =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(12, 2)
            );

        ushort replyId =
            BinaryPrimitives.ReadUInt16LittleEndian(
                packet.AsSpan(14, 2)
            );

        int dataLength = (int)payloadSize - 8;

        byte[] data = [];

        if (dataLength > 0)
        {
            data = new byte[dataLength];

            Array.Copy(
                packet,
                16,
                data,
                0,
                dataLength
            );
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

    private static ushort CalculateChecksum(byte[] payload)
    {
        uint sum = 0;

        for (int i = 0; i < payload.Length; i += 2)
        {
            ushort value;

            // El campo checksum no participa
            // en su propio cálculo.
            if (i == 2)
            {
                value = 0;
            }
            else
            {
                byte low = payload[i];

                byte high =
                    i + 1 < payload.Length
                        ? payload[i + 1]
                        : (byte)0;

                value = (ushort)(
                    low |
                    (high << 8)
                );
            }

            sum += value;
        }

        // Plegar carry
        sum = (sum & 0xFFFF) + (sum >> 16);

        // Complemento
        return (ushort)(sum ^ 0xFFFF);
    }
}