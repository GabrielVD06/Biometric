namespace K30Integration.K30.Models;

public class AttendanceRecord
{
    public int RecordNumber { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public int VerifyMode { get; set; }

    public int Status { get; set; }

    public byte[] RawData { get; set; } = [];

    public string RawHex =>
        Convert.ToHexString(RawData);

    public string VerifyModeName =>
        VerifyMode switch
        {
            0 => "Password",
            1 => "Huella",
            2 => "Tarjeta",
            _ => $"Desconocido ({VerifyMode})"
        };

    public string StatusName =>
        Status switch
        {
            0 => "Entrada",
            1 => "Salida",
            2 => "Salida descanso",
            3 => "Entrada descanso",
            4 => "OT entrada",
            5 => "OT salida",
            _ => $"Desconocido ({Status})"
        };

    public override string ToString()
    {
        return
            $"Registro: {RecordNumber} | " +
            $"ID: {UserId} | " +
            $"Hora: {Timestamp:yyyy-MM-dd HH:mm:ss} | " +
            $"Método: {VerifyModeName} | " +
            $"Estado: {StatusName}";
    }
}