using System.Runtime.InteropServices;

namespace RkDevelopTool.Models
{
    public enum USB_ACCESS_TYPE
    {
        USB_BULK_READ = 0,
        USB_BULK_WRITE,
        USB_CONTROL,
    }

    public enum TESTUNIT_SUBCODE : byte
    {
        TU_NONE_SUBCODE = 0,
        TU_ERASESYSTEM_SUBCODE = 0xFE,
        TU_LOWERFORMAT_SUBCODE = 0xFD,
        TU_ERASEUSERDATA_SUBCODE = 0xFB,
        TU_GETUSERSECTOR_SUBCODE = 0xF9
    }

    public enum RESET_SUBCODE : byte
    {
        RST_NONE_SUBCODE = 0,
        RST_RESETMSC_SUBCODE,
        RST_POWEROFF_SUBCODE,
        RST_RESETMASKROM_SUBCODE,
        RST_DISCONNECTRESET_SUBCODE
    }

    public enum RW_SUBCODE : byte
    {
        RWMETHOD_IMAGE = 0,
        RWMETHOD_LBA
    }

    public enum USB_OPERATION_CODE : byte
    {
        TEST_UNIT_READY = 0,
        READ_FLASH_ID = 0x01,
        TEST_BAD_BLOCK = 0x03,
        READ_SECTOR = 0x04,
        WRITE_SECTOR = 0x05,
        ERASE_NORMAL = 0x06,
        ERASE_FORCE = 0x0B,
        READ_LBA = 0x14,
        WRITE_LBA = 0x15,
        ERASE_SYSTEMDISK = 0x16,
        READ_SDRAM = 0x17,
        WRITE_SDRAM = 0x18,
        EXECUTE_SDRAM = 0x19,
        READ_FLASH_INFO = 0x1A,
        READ_CHIP_INFO = 0x1B,
        SET_RESET_FLAG = 0x1E,
        WRITE_EFUSE = 0x1F,
        READ_EFUSE = 0x20,
        READ_SPI_FLASH = 0x21,
        WRITE_SPI_FLASH = 0x22,
        WRITE_NEW_EFUSE = 0x23,
        READ_NEW_EFUSE = 0x24,
        ERASE_LBA = 0x25,
        CHANGE_STORAGE = 0x2A,
        READ_STORAGE = 0x2B,
        READ_CAPABILITY = 0xAA,
        DEVICE_RESET = 0xFF
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CBWCB
    {
        public byte ucOperCode;
        public byte ucReserved;
        public uint dwAddress;
        public byte ucReserved2;
        public ushort usLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public byte[] ucReserved3;

        public static CBWCB Create()
        {
            return new CBWCB { ucReserved3 = new byte[7] };
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CBW
    {
        public uint dwCBWSignature;
        public uint dwCBWTag;
        public uint dwCBWTransferLength;
        public byte ucCBWFlags;
        public byte ucCBWLUN;
        public byte ucCBWCBLength;
        public CBWCB cbwcb;

        public static CBW Create()
        {
            var cbw = new CBW();
            cbw.cbwcb = CBWCB.Create();
            return cbw;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CSW
    {
        public uint dwCSWSignature;
        public uint dwCSWTag;
        public uint dwCBWDataResidue;
        public byte ucCSWStatus;
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
