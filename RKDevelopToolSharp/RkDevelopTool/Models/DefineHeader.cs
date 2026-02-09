using System.Runtime.InteropServices;

namespace RkDevelopTool.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public unsafe struct STRUCT_FLASH_INFO
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string szManufacturerName;
        public uint uiFlashSize;
        public ushort usBlockSize;
        public uint uiPageSize;
        public uint uiSectorPerBlock;
        public fixed byte blockState[50];
        public uint uiBlockNum;
        public byte bECCBits;
        public byte bAccessTime;
        public byte bFlashCS;
        public ushort usValidSecPerBlock;
        public ushort usPhyBlokcPerIDB;
        public uint uiSecNumPerIDB;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct STRUCT_FLASHINFO_CMD
    {
        public uint uiFlashSize;
        public ushort usBlockSize;
        public byte bPageSize;
        public byte bECCBits;
        public byte bAccessTime;
        public byte bManufCode;
        public byte bFlashCS;
        public fixed byte reserved[501];
    }

    public enum ENUM_RKDEVICE_TYPE : int
    {
        RKNONE_DEVICE = 0,
        RK27_DEVICE = 0x10,
        RKCAYMAN_DEVICE,
        RK28_DEVICE = 0x20,
        RK281X_DEVICE,
        RKPANDA_DEVICE,
        RKNANO_DEVICE = 0x30,
        RKSMART_DEVICE,
        RKCROWN_DEVICE = 0x40,
        RK29_DEVICE = 0x50,
        RK292X_DEVICE,
        RK30_DEVICE = 0x60,
        RK30B_DEVICE,
        RK31_DEVICE = 0x70,
        RK32_DEVICE = 0x80
    }

    public enum ENUM_OS_TYPE : int
    {
        RK_OS = 0,
        ANDROID_OS = 0x1
    }

    [Flags]
    public enum ENUM_RKUSB_TYPE : int
    {
        RKUSB_NONE = 0x0,
        RKUSB_MASKROM = 0x01,
        RKUSB_LOADER = 0x02,
        RKUSB_MSC = 0x04
    }

    public enum ENUM_RKBOOTENTRY : int
    {
        ENTRY471 = 1,
        ENTRY472 = 2,
        ENTRYLOADER = 4
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct sparse_header
    {
        public uint magic;          /* 0xed26ff3a */
        public ushort major_version;
        public ushort minor_version;
        public ushort file_hdr_sz;
        public ushort chunk_hdr_sz;
        public uint blk_sz;
        public uint total_blks;
        public uint total_chunks;
        public uint image_checksum;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct chunk_header
    {
        public ushort chunk_type;
        public ushort reserved1;
        public uint chunk_sz;
        public uint total_sz;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct STRUCT_RKTIME
    {
        public ushort usYear;
        public byte ucMonth;
        public byte ucDay;
        public byte ucHour;
        public byte ucMinute;
        public byte ucSecond;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct STRUCT_CONFIG_ITEM
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szItemName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szItemValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct STRUCT_PARAM_ITEM
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szItemName;
        public uint uiItemOffset;
        public uint uiItemSize;
    }

    public class STRUCT_RKDEVICE_DESC
    {
        public ushort usVid;
        public ushort usPid;
        public ushort usbcdUsb;
        public uint uiLocationID;
        public ENUM_RKUSB_TYPE emUsbType;
        public ENUM_RKDEVICE_TYPE emDeviceType;
        public IntPtr pUsbHandle; // For LibUsbDotNet, this might be a handle or object
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct RK28_IDB_SEC0
    {
        public uint dwTag;
        public fixed byte reserved[4];
        public uint uiRc4Flag;
        public ushort usBootCode1Offset;
        public ushort usBootCode2Offset;
        public fixed byte reserved1[490];
        public ushort usBootDataSize;
        public ushort usBootCodeSize;
        public ushort usCrc;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct RK28_IDB_SEC1
    {
        public ushort usSysReservedBlock;
        public ushort usDisk0Size;
        public ushort usDisk1Size;
        public ushort usDisk2Size;
        public ushort usDisk3Size;
        public uint uiChipTag;
        public uint uiMachineId;
        public ushort usLoaderYear;
        public ushort usLoaderDate;
        public ushort usLoaderVer;
        public ushort usLastLoaderVer;
        public ushort usReadWriteTimes;
        public uint dwFwVer;
        public ushort usMachineInfoLen;
        public fixed byte ucMachineInfo[30];
        public ushort usManufactoryInfoLen;
        public fixed byte ucManufactoryInfo[30];
        public ushort usFlashInfoOffset;
        public ushort usFlashInfoLen;
        public fixed byte reserved[384];
        public uint uiFlashSize;
        public byte reserved1;
        public byte bAccessTime;
        public ushort usBlockSize;
        public byte bPageSize;
        public byte bECCBits;
        public fixed byte reserved2[8];
        public ushort usIdBlock0;
        public ushort usIdBlock1;
        public ushort usIdBlock2;
        public ushort usIdBlock3;
        public ushort usIdBlock4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct RK28_IDB_SEC2
    {
        public ushort usInfoSize;
        public fixed byte bChipInfo[16];
        public fixed byte reserved[473];
        public fixed byte szVcTag[3];
        public ushort usSec0Crc;
        public ushort usSec1Crc;
        public uint uiBootCodeCrc;
        public ushort usSec3CustomDataOffset;
        public ushort usSec3CustomDataSize;
        public fixed byte szCrcTag[4];
        public ushort usSec3Crc;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct RK28_IDB_SEC3
    {
        public ushort usSNSize;
        public fixed byte sn[60];
        public fixed byte reserved[382];
        public byte wifiSize;
        public fixed byte wifiAddr[6];
        public byte imeiSize;
        public fixed byte imei[15];
        public byte uidSize;
        public fixed byte uid[30];
        public byte blueToothSize;
        public fixed byte blueToothAddr[6];
        public byte macSize;
        public fixed byte macAddr[6];
    }

    public enum ENUM_PROGRESS_PROMPT
    {
        TESTDEVICE_PROGRESS,
        DOWNLOADIMAGE_PROGRESS,
        CHECKIMAGE_PROGRESS,
        TAGBADBLOCK_PROGRESS,
        TESTBLOCK_PROGRESS,
        ERASEFLASH_PROGRESS,
        ERASESYSTEM_PROGRESS,
        LOWERFORMAT_PROGRESS,
        ERASEUSERDATA_PROGRESS
    }

    public enum ENUM_CALL_STEP
    {
        CALL_FIRST,
        CALL_MIDDLE,
        CALL_LAST
    }

    public delegate void ProgressPromptCB(uint deviceLayer, ENUM_PROGRESS_PROMPT promptID, long totalValue, long currentValue, ENUM_CALL_STEP emCall);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct STRUCT_DEVICE_CONFIG
    {
        public ushort usVid;
        public ushort usPid;
        public ENUM_RKDEVICE_TYPE emDeviceType;
    }

    public static class Constants
    {
        public const uint SPARSE_HEADER_MAGIC = 0xed26ff3a;
        public const uint UBI_HEADER_MAGIC = 0x23494255;
        public const ushort CHUNK_TYPE_RAW = 0xCAC1;
        public const ushort CHUNK_TYPE_FILL = 0xCAC2;
        public const ushort CHUNK_TYPE_DONT_CARE = 0xCAC3;
        public const ushort CHUNK_TYPE_CRC32 = 0xCAC4;
        public const int CHIPINFO_LEN = 16;
    }
}
