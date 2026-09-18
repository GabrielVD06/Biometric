namespace K30Integration.K30.Models;

public class AttendanceRecord
{
    public int RecordNumber { get; set; }

    public string UserId { get; set; } =
        string.Empty;

    public string UserName { get; set; } =
        string.Empty;

    public DateTime Timestamp { get; set; }

    public int VerifyMode { get; set; }

    public int Status { get; set; }


    // ============================================================
    // NOMBRE DEL MÉTODO DE VERIFICACIÓN
    // ============================================================

    public string VerifyModeName =>
        VerifyMode switch
        {
            0 => "Password",
            1 => "Huella",
            2 => "Tarjeta",
            _ => $"Desconocido ({VerifyMode})"
        };


    // ============================================================
    // NOMBRE DEL ESTADO
    // ============================================================

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


    // ============================================================
    // TO STRING
    // ============================================================

    public override string ToString()
    {
        string name =
            string.IsNullOrWhiteSpace(UserName)
                ? "(nombre no resuelto)"
                : UserName;

        return
            $"Registro: {RecordNumber} | " +
            $"Usuario: {name} | " +
            $"ID: {UserId} | " +
            $"Hora: {Timestamp:yyyy-MM-dd HH:mm:ss} | " +
            $"Método: {VerifyModeName} | " +
            $"Estado: {StatusName}";
    }
}