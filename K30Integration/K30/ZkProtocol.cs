namespace K30Integration.K30;

public static class ZkProtocol
{
    public const byte Header1 = 0x50;
    public const byte Header2 = 0x50;
    public const byte Header3 = 0x82;
    public const byte Header4 = 0x7D;

    // ========================================
    // CONNECTION
    // ========================================

    public const ushort CMD_CONNECT = 1000;
    public const ushort CMD_EXIT = 1001;
    public const ushort CMD_ENABLE_DEVICE = 1002;
    public const ushort CMD_DISABLE_DEVICE = 1003;

    // ========================================
    // DEVICE
    // ========================================

    public const ushort CMD_GET_FREE_SIZES = 50;
    public const ushort CMD_STATE_RRQ = 64;
    public const ushort CMD_GET_TIME = 201;
    public const ushort CMD_GET_VERSION = 1100;

    // ========================================
    // OPTIONS
    // ========================================

    public const ushort CMD_OPTIONS_RRQ = 11;
    public const ushort CMD_OPTIONS_WRQ = 12;

    // ========================================
    // ATTENDANCE
    // ========================================

    public const ushort CMD_ATTLOG_RRQ = 13;

    // ========================================
    // DATA TRANSFER
    // ========================================

    public const ushort CMD_PREPARE_DATA = 1500;
    public const ushort CMD_DATA = 1501;
    public const ushort CMD_FREE_DATA = 1502;

    // IMPORTANT:
    // Used to request large datasets such as attendance logs.
    public const ushort CMD_DATA_WRRQ = 1503;

    public const ushort CMD_DATA_RDY = 1504;

    // ========================================
    // RESPONSES
    // ========================================

    public const ushort CMD_ACK_OK = 2000;
    public const ushort CMD_ACK_ERROR = 2001;
    public const ushort CMD_ACK_DATA = 2002;
    public const ushort CMD_ACK_RETRY = 2003;
    public const ushort CMD_ACK_REPEAT = 2004;
    public const ushort CMD_ACK_UNAUTH = 2005;

    public const ushort CMD_ACK_UNKNOWN = 65535;

    // ========================================
    // REALTIME EVENTS
    // ========================================

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
}