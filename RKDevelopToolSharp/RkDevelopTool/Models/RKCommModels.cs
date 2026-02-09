using System.Runtime.InteropServices;

namespace RkDevelopTool.Models
{
    public enum UsbAccessType
    {
        BulkRead = 0,
        BulkWrite,
        Control,
    }

    public enum TestUnitSubCode : byte
    {
        None = 0,
        EraseSystem = 0xFE,
        LowerFormat = 0xFD,
        EraseUserData = 0xFB,
        GetUserSector = 0xF9
    }

    public enum ResetSubCode : byte
    {
        None = 0,
        ResetMsc,
        PowerOff,
        ResetMaskRom,
        DisconnectReset
    }

    public enum RwSubCode : byte
    {
        Image = 0,
        Lba
    }

    public enum UsbOperationCode : byte
    {
        TestUnitReady = 0,
        ReadFlashId = 0x01,
        TestBadBlock = 0x03,
        ReadSector = 0x04,
        WriteSector = 0x05,
        EraseNormal = 0x06,
        EraseForce = 0x0B,
        ReadLba = 0x14,
        WriteLba = 0x15,
        EraseSystemDisk = 0x16,
        ReadSdram = 0x17,
        WriteSdram = 0x18,
        ExecuteSdram = 0x19,
        ReadFlashInfo = 0x1A,
        ReadChipInfo = 0x1B,
        SetResetFlag = 0x1E,
        WriteEfuse = 0x1F,
        ReadEfuse = 0x20,
        ReadSpiFlash = 0x21,
        WriteSpiFlash = 0x22,
        WriteNewEfuse = 0x23,
        ReadNewEfuse = 0x24,
        EraseLba = 0x25,
        ChangeStorage = 0x2A,
        ReadStorage = 0x2B,
        ReadCapability = 0xAA,
        DeviceReset = 0xFF
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cbwcb
    {
        public byte OperCode;
        public byte Reserved;
        public uint Address;
        public byte Reserved2;
        public ushort Length;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public byte[] Reserved3;

        public static Cbwcb Create()
        {
            return new Cbwcb { Reserved3 = new byte[7] };
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Cbw
    {
        public uint Signature;
        public uint Tag;
        public uint TransferLength;
        public byte Flags;
        public byte Lun;
        public byte CbLength;
        public Cbwcb Cbwcb;

        public static Cbw Create()
        {
            var cbw = new Cbw();
            cbw.Cbwcb = Cbwcb.Create();
            return cbw;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Csw
    {
        public uint Signature;
        public uint Tag;
        public uint DataResidue;
        public byte Status;
    }

    public static class RKCommConstants
    {
        public const uint CBW_SIGN = 0x43425355; // "USBC"
        public const uint CSW_SIGN = 0x53425355; // "USBS"
        public const byte DIRECTION_OUT = 0x00;
        public const byte DIRECTION_IN = 0x80;
        public const int MAX_TEST_BLOCKS = 512;
        public const int MAX_ERASE_BLOCKS = 16;
        public const int MAX_CLEAR_LEN = 16 * 1024;

        public const int ERR_SUCCESS = 0;
        public const int ERR_DEVICE_READY = 0;
        public const int ERR_DEVICE_OPEN_FAILED = -1;
        public const int ERR_CSW_OPEN_FAILED = -2;
        public const int ERR_DEVICE_WRITE_FAILED = -3;
        public const int ERR_DEVICE_READ_FAILED = -4;
        public const int ERR_CMD_NOTMATCH = -5;
        public const int ERR_DEVICE_UNREADY = -6;
        public const int ERR_FOUND_BAD_BLOCK = -7;
        public const int ERR_FAILED = -8;
        public const int ERR_CROSS_BORDER = -9;
        public const int ERR_DEVICE_NOT_SUPPORT = -10;
        public const int ERR_REQUEST_NOT_SUPPORT = -11;
        public const int ERR_REQUEST_FAIL = -12;
        public const int ERR_BUFFER_NOT_ENOUGH = -13;
    }
}
