namespace K30Integration.K30.Models;

public enum DeviceState
{
    Waiting = 0,
    FingerprintRegistration = 1,
    FingerprintIdentification = 2,
    Menu = 3,
    Busy = 4,
    CardWriting = 5,
    Unknown = -1
}