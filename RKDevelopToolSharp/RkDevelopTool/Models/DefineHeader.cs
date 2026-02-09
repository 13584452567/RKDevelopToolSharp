using System.Runtime.InteropServices;

namespace RkDevelopTool.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public unsafe struct FlashInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string ManufacturerName;
        public uint FlashSize;
        public ushort BlockSize;
        public uint PageSize;
        public uint SectorPerBlock;
        public fixed byte BlockState[50];
        public uint BlockNum;
        public byte EccBits;
        public byte AccessTime;
        public byte FlashCs;
        public ushort ValidSecPerBlock;
        public ushort PhyBlockPerIdb;
        public uint SecNumPerIdb;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct FlashInfoCmd
    {
        public uint FlashSize;
        public ushort BlockSize;
        public byte PageSize;
        public byte EccBits;
        public byte AccessTime;
        public byte ManufCode;
        public byte FlashCs;
        public fixed byte Reserved[501];
    }

    public enum RkDeviceType : int
    {
        None = 0,
        Rk27 = 0x10,
        RkCayman,
        Rk28 = 0x20,
        Rk281x,
        RkPanda,
        RkNano = 0x30,
        RkSmart,
        RkCrown = 0x40,
        Rk29 = 0x50,
        Rk292x,
        Rk30 = 0x60,
        Rk30b,
        Rk31 = 0x70,
        Rk32 = 0x80
    }

    public enum OsType : int
    {
        RkOs = 0,
        AndroidOs = 0x1
    }

    [Flags]
    public enum RkUsbType : int
    {
        None = 0x0,
        MaskRom = 0x01,
        Loader = 0x02,
        Msc = 0x04
    }

    public enum RkBootEntryType : int
    {
        Entry471 = 1,
        Entry472 = 2,
        EntryLoader = 4
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SparseHeader
    {
        public uint Magic;          /* 0xed26ff3a */
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ushort FileHeaderSize;
        public ushort ChunkHeaderSize;
        public uint BlockSize;
        public uint TotalBlocks;
        public uint TotalChunks;
        public uint ImageChecksum;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChunkHeader
    {
        public ushort ChunkType;
        public ushort Reserved1;
        public uint ChunkSize;
        public uint TotalSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RkTime
    {
        public ushort Year;
        public byte Month;
        public byte Day;
        public byte Hour;
        public byte Minute;
        public byte Second;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct ConfigItem
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string ItemName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ItemValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct ParamItem
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ItemName;
        public uint ItemOffset;
        public uint ItemSize;
    }

    public class RkDeviceDesc
    {
        public ushort Vid { get; set; }
        public ushort Pid { get; set; }
        public ushort UsbcdUsb { get; set; }
        public uint LocationId { get; set; }
        public RkUsbType UsbType { get; set; }
        public RkDeviceType DeviceType { get; set; }
        public IntPtr UsbHandle { get; set; } // For LibUsbDotNet, this might be a handle or object
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Rk28IdbSec0
    {
        public uint Tag;
        public fixed byte Reserved[4];
        public uint Rc4Flag;
        public ushort BootCode1Offset;
        public ushort BootCode2Offset;
        public fixed byte Reserved1[490];
        public ushort BootDataSize;
        public ushort BootCodeSize;
        public ushort Crc;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Rk28IdbSec1
    {
        public ushort SysReservedBlock;
        public ushort Disk0Size;
        public ushort Disk1Size;
        public ushort Disk2Size;
        public ushort Disk3Size;
        public uint ChipTag;
        public uint MachineId;
        public ushort LoaderYear;
        public ushort LoaderDate;
        public ushort LoaderVer;
        public ushort LastLoaderVer;
        public ushort ReadWriteTimes;
        public uint FwVer;
        public ushort MachineInfoLen;
        public fixed byte MachineInfo[30];
        public ushort ManufactoryInfoLen;
        public fixed byte ManufactoryInfo[30];
        public ushort FlashInfoOffset;
        public ushort FlashInfoLen;
        public fixed byte Reserved[384];
        public uint FlashSize;
        public byte Reserved1;
        public byte AccessTime;
        public ushort BlockSize;
        public byte PageSize;
        public byte EccBits;
        public fixed byte Reserved2[8];
        public ushort IdBlock0;
        public ushort IdBlock1;
        public ushort IdBlock2;
        public ushort IdBlock3;
        public ushort IdBlock4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Rk28IdbSec2
    {
        public ushort InfoSize;
        public fixed byte ChipInfo[16];
        public fixed byte Reserved[473];
        public fixed byte VcTag[3];
        public ushort Sec0Crc;
        public ushort Sec1Crc;
        public uint BootCodeCrc;
        public ushort Sec3CustomDataOffset;
        public ushort Sec3CustomDataSize;
        public fixed byte CrcTag[4];
        public ushort Sec3Crc;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct Rk28IdbSec3
    {
        public ushort SnSize;
        public fixed byte Sn[60];
        public fixed byte Reserved[382];
        public byte WifiSize;
        public fixed byte WifiAddr[6];
        public byte ImeiSize;
        public fixed byte Imei[15];
        public byte UidSize;
        public fixed byte Uid[30];
        public byte BlueToothSize;
        public fixed byte BlueToothAddr[6];
        public byte MacSize;
        public fixed byte MacAddr[6];
    }

    public enum ProgressPrompt
    {
        TestDevice,
        DownloadImage,
        CheckImage,
        TagBadBlock,
        TestBlock,
        EraseFlash,
        EraseSystem,
        LowerFormat,
        EraseUserData
    }

    public enum CallStep
    {
        First,
        Middle,
        Last
    }

    public delegate void ProgressPromptCallback(uint deviceLayer, ProgressPrompt promptId, long totalValue, long currentValue, CallStep step);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DeviceConfig
    {
        public ushort Vid;
        public ushort Pid;
        public RkDeviceType DeviceType;
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
