namespace K30Integration.K30;

public static class ZkProtocol
{
    // ============================================================
    // ZKTeco packet header
    // ============================================================

    public const byte Header1 = 0x50;
    public const byte Header2 = 0x50;
    public const byte Header3 = 0x82;
    public const byte Header4 = 0x7D;


    // ============================================================
    // Connection
    // ============================================================

    public const ushort CMD_CONNECT = 1000;
    public const ushort CMD_EXIT = 1001;

    public const ushort CMD_ENABLE_DEVICE = 1002;
    public const ushort CMD_DISABLE_DEVICE = 1003;


    // ============================================================
    // Device information
    // ============================================================

    public const ushort CMD_GET_FREE_SIZES = 50;
    public const ushort CMD_STATE_RRQ = 64;
    public const ushort CMD_GET_TIME = 201;
    public const ushort CMD_GET_VERSION = 1100;


    // ============================================================
    // Options
    // ============================================================

    public const ushort CMD_OPTIONS_RRQ = 11;
    public const ushort CMD_OPTIONS_WRQ = 12;


    // ============================================================
    // Database / stored data
    // ============================================================

    // Read saved data from the device.
    public const ushort CMD_DB_RRQ = 7;

    // Upload user information to the terminal.
    public const ushort CMD_USER_WRQ = 8;

    // Read fingerprint/template data.
    public const ushort CMD_USERTEMP_RRQ = 9;

    // Upload fingerprint/template data.
    public const ushort CMD_USERTEMP_WRQ = 10;


    // ============================================================
    // Attendance
    // ============================================================

    // Request attendance records.
    public const ushort CMD_ATTLOG_RRQ = 13;

    // Clear generic data.
    public const ushort CMD_CLEAR_DATA = 14;

    // IMPORTANT:
    // We deliberately do NOT use CMD_CLEAR_ATTLOG anywhere
    // in this project.
    //
    // CMD_CLEAR_ATTLOG = 15;


    // ============================================================
    // Buffered / large data transfer
    // ============================================================

    public const ushort CMD_PREPARE_DATA = 1500;
    public const ushort CMD_DATA = 1501;
    public const ushort CMD_FREE_DATA = 1502;

    // These are protocol commands for large/partial data transfers.
    // We will use them only after confirming how this K30 responds.
    public const ushort CMD_DATA_WRRQ = 1503;
    public const ushort CMD_DATA_RDY = 1504;


    // ============================================================
    // Realtime events
    // ============================================================

    public const ushort CMD_REG_EVENT = 500;

    public const ushort EF_ATTLOG = 1;
    public const ushort EF_FINGER = 2;
    public const ushort EF_ENROLLUSER = 4;
    public const ushort EF_ENROLLFINGER = 8;
    public const ushort EF_BUTTON = 16;
    public const ushort EF_UNLOCK = 32;
    public const ushort EF_VERIFY = 128;
    public const ushort EF_FPFTR = 256;
    public const ushort EF_ALARM = 512;


    // ============================================================
    // User / database function identifiers
    // ============================================================

    // Function identifiers used with CMD_DB_RRQ.
    public const ushort FCT_ATTLOG = 1;
    public const ushort FCT_FINGERTMP = 2;
    public const ushort FCT_OPLOG = 4;
    public const ushort FCT_USER = 5;
    public const ushort FCT_SMS = 6;
    public const ushort FCT_UDATA = 7;
    public const ushort FCT_WORKCODE = 8;


    // ============================================================
    // Responses
    // ============================================================

    public const ushort CMD_ACK_OK = 2000;
    public const ushort CMD_ACK_ERROR = 2001;
    public const ushort CMD_ACK_DATA = 2002;
    public const ushort CMD_ACK_RETRY = 2003;
    public const ushort CMD_ACK_REPEAT = 2004;
    public const ushort CMD_ACK_UNAUTH = 2005;

    public const ushort CMD_ACK_UNKNOWN = 65535;
}