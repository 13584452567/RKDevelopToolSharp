using RkDevelopTool.Models;
using RkDevelopTool.Utils;
using System.Runtime.InteropServices;

namespace RkDevelopTool.Core
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct STRUCT_RKIMAGE_HEAD
    {
        public uint uiTag;
        public ushort usSize;
        public uint dwVersion;
        public uint dwMergeVersion;
        public STRUCT_RKTIME stReleaseTime;
        public ENUM_RKDEVICE_TYPE emSupportChip;
        public uint dwBootOffset;
        public uint dwBootSize;
        public uint dwFWOffset;
        public uint dwFWSize;
        public fixed byte reserved[61];
    }
    public class RKImage
    {
        private STRUCT_RKIMAGE_HEAD m_header;
        private string? m_filePath;
        private long m_fileSize;
        private byte[] m_md5 = new byte[32];
        private byte[] m_signMd5 = new byte[256];
        private int m_signMd5Size;
        private byte[] m_reserved = new byte[61];

        public RKBoot? m_bootObject;

        public uint Version => m_header.dwVersion;
        public uint MergeVersion => m_header.dwMergeVersion;
        public STRUCT_RKTIME ReleaseTime => m_header.stReleaseTime;
        public ENUM_RKDEVICE_TYPE SupportDevice => m_header.emSupportChip;
        public ENUM_OS_TYPE OsType => (ENUM_OS_TYPE)BitConverter.ToUInt32(m_reserved, 4);
        public ushort BackupSize => BitConverter.ToUInt16(m_reserved, 12);
        public uint BootOffset => m_header.dwBootOffset;
        public uint BootSize => m_header.dwBootSize;
        public uint FWOffset => m_header.dwFWOffset;
        public long FWSize { get; private set; }
        public long ImageSize => m_fileSize;
        public bool SignFlag { get; private set; }

        public RKImage() { }
        public bool LoadImage(string path)
        {
            try
            {
                m_filePath = path;
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists) return false;
                m_fileSize = fi.Length;

                bool bOnlyBootFile = path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (!bOnlyBootFile)
                    {
                        byte[] headBytes = new byte[Marshal.SizeOf<STRUCT_RKIMAGE_HEAD>()];
                        if (fs.Read(headBytes, 0, headBytes.Length) != headBytes.Length) return false;

                        GCHandle handle = GCHandle.Alloc(headBytes, GCHandleType.Pinned);
                        m_header = Marshal.PtrToStructure<STRUCT_RKIMAGE_HEAD>(handle.AddrOfPinnedObject());
                        handle.Free();

                        if (m_header.uiTag != 0x57464B52) return false; // "RKFW"

                        long ulFwSize;
                        unsafe
                        {
                            fixed (byte* pReserved = m_header.reserved)
                            {
                                if (pReserved[14] == 'H' && pReserved[15] == 'I')
                                {
                                    ulFwSize = (long)(*((uint*)(pReserved + 16)));
                                    ulFwSize <<= 32;
                                    ulFwSize += m_header.dwFWOffset;
                                    ulFwSize += m_header.dwFWSize;
                                }
                                else
                                {
                                    ulFwSize = (long)m_header.dwFWOffset + m_header.dwFWSize;
                                }

                                for (int i = 0; i < 61; i++) m_reserved[i] = pReserved[i];
                            }
                        }

                        FWSize = ulFwSize - m_header.dwFWOffset;
                        int nMd5DataSize = (int)(m_fileSize - ulFwSize);
                        if (nMd5DataSize >= 160)
                        {
                            SignFlag = true;
                            m_signMd5Size = nMd5DataSize - 32;
                            fs.Seek(ulFwSize, SeekOrigin.Begin);
                            fs.ReadExactly(m_md5, 0, 32);
                            fs.ReadExactly(m_signMd5, 0, m_signMd5Size);
                        }
                        else
                        {
                            fs.Seek(-32, SeekOrigin.End);
                            fs.ReadExactly(m_md5, 0, 32);
                        }
                    }
                    else
                    {
                        m_header.dwBootOffset = 0;
                        m_header.dwBootSize = (uint)m_fileSize;
                        m_header.dwFWOffset = 0;
                        m_header.dwFWSize = 0;
                    }
                    
                    byte[] bootData = new byte[m_header.dwBootSize];
                    fs.Seek(m_header.dwBootOffset, SeekOrigin.Begin);
                    fs.ReadExactly(bootData);

                    bool bCheck;
                    m_bootObject = new RKBoot(bootData, out bCheck);
                    if (!bCheck) return false;

                    if (bOnlyBootFile)
                    {
                        m_header.emSupportChip = m_bootObject.SupportDevice;
                        byte[] osTypeBytes = BitConverter.GetBytes((uint)ENUM_OS_TYPE.RK_OS);
                        Array.Copy(osTypeBytes, 0, m_reserved, 4, 4);
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public bool GetData(long dwOffset, uint dwSize, byte[] lpBuffer)
        {
            if (dwOffset < 0 || dwSize == 0 || dwOffset + dwSize > m_fileSize) return false;
            try
            {
                using (FileStream fs = new FileStream(m_filePath!, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(dwOffset, SeekOrigin.Begin);
                    return fs.Read(lpBuffer, 0, (int)dwSize) == dwSize;
                }
            }
            catch { return false; }
        }

        public void GetReservedData(out byte[] data, out ushort size)
        {
            data = m_reserved;
            size = 61;
        }

        public int GetMd5Data(out byte[] md5, out byte[] signMd5)
        {
            md5 = m_md5;
            signMd5 = m_signMd5;
            return m_signMd5Size;
        }

        public long GetImageSize()
        {
            return m_fileSize;
        }

        public bool SaveBootFile(string filename)
        {
            if (m_filePath == null || BootSize == 0) return false;
            try
            {
                using (FileStream fsSource = new FileStream(m_filePath, FileMode.Open, FileAccess.Read))
                using (FileStream fsDest = new FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    fsSource.Seek(BootOffset, SeekOrigin.Begin);
                    byte[] buffer = new byte[1024];
                    uint remaining = BootSize;
                    while (remaining > 0)
                    {
                        int read = fsSource.Read(buffer, 0, (int)Math.Min(1024, remaining));
                        if (read <= 0) break;
                        fsDest.Write(buffer, 0, read);
                        remaining -= (uint)read;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public bool SaveFWFile(string filename)
        {
            if (m_filePath == null || FWSize == 0) return false;
            try
            {
                using (FileStream fsSource = new FileStream(m_filePath, FileMode.Open, FileAccess.Read))
                using (FileStream fsDest = new FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    fsSource.Seek(FWOffset, SeekOrigin.Begin);
                    byte[] buffer = new byte[1024];
                    long remaining = FWSize;
                    while (remaining > 0)
                    {
                        int read = fsSource.Read(buffer, 0, (int)Math.Min(1024, remaining));
                        if (read <= 0) break;
                        fsDest.Write(buffer, 0, read);
                        remaining -= read;
                    }
                }
                return true;
            }
            catch { return false; }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct STRUCT_RKBOOT_HEAD
    {
        public uint uiTag;
        public ushort usSize;
        public uint dwVersion;
        public uint dwMergeVersion;
        public STRUCT_RKTIME stReleaseTime;
        public ENUM_RKDEVICE_TYPE emSupportChip;
        public byte uc471EntryCount;
        public uint dw471EntryOffset;
        public byte uc471EntrySize;
        public byte uc472EntryCount;
        public uint dw472EntryOffset;
        public byte uc472EntrySize;
        public byte ucLoaderEntryCount;
        public uint dwLoaderEntryOffset;
        public byte ucLoaderEntrySize;
        public byte ucSignFlag;
        public byte ucRc4Flag;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 57)]
        public byte[] reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct STRUCT_RKBOOT_ENTRY
    {
        public byte ucSize;
        public ENUM_RKBOOTENTRY emType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szName;
        public uint dwDataOffset;
        public uint dwDataSize;
        public uint dwDataDelay;
    }

    public class RKBoot
    {
        private byte[] m_bootData;
        private STRUCT_RKBOOT_HEAD m_header;

        public bool Rc4DisableFlag => m_header.ucRc4Flag != 0;
        public bool SignFlag => m_header.ucSignFlag == (byte)'S';
        public uint Version => m_header.dwVersion;
        public uint MergeVersion => m_header.dwMergeVersion;
        public STRUCT_RKTIME ReleaseTime => m_header.stReleaseTime;
        public ENUM_RKDEVICE_TYPE SupportDevice => m_header.emSupportChip;

        public byte Entry471Count => m_header.uc471EntryCount;
        public byte Entry472Count => m_header.uc472EntryCount;
        public byte EntryLoaderCount => m_header.ucLoaderEntryCount;

        public RKBoot(byte[] bootData, out bool bCheck)
        {
            m_bootData = bootData;
            bCheck = Initialize(out bool crcCheckResult);
            if (bCheck && !crcCheckResult)
            {
                // C++ optionally continues or returns. In C++: bCheck = CrcCheck(); if (!bCheck) return;
                // We'll set bCheck to the result of CrcCheck to match CRKBoot constructor.
                bCheck = false;
            }
        }

        private bool Initialize(out bool crcCheckResult)
        {
            crcCheckResult = false;
            if (m_bootData.Length < Marshal.SizeOf<STRUCT_RKBOOT_HEAD>())
                return false;

            crcCheckResult = CrcCheck();
            if (!crcCheckResult) return true; // Construction continues but bCheck will be false

            GCHandle handle = GCHandle.Alloc(m_bootData, GCHandleType.Pinned);
            m_header = Marshal.PtrToStructure<STRUCT_RKBOOT_HEAD>(handle.AddrOfPinnedObject());
            handle.Free();

            if (m_header.uiTag != 0x544F4F42 && m_header.uiTag != 0x2052444C)
                return false;

            return true;
        }

        public bool CrcCheck()
        {
            if (m_bootData.Length < 4) return false;
            uint oldCrc = BitConverter.ToUInt32(m_bootData, m_bootData.Length - 4);
            uint newCrc = CRCUtils.CRC_32(m_bootData, (uint)m_bootData.Length - 4);
            return oldCrc == newCrc;
        }

        public bool SaveEntryFile(ENUM_RKBOOTENTRY type, byte ucIndex, string fileName)
        {
            byte[]? data = GetEntryData(type, ucIndex);
            if (data == null) return false;
            try
            {
                File.WriteAllBytes(fileName, data);
                return true;
            }
            catch { return false; }
        }

        public int GetIndexByName(ENUM_RKBOOTENTRY type, string name)
        {
            uint dwOffset;
            byte ucCount, ucSize;

            switch (type)
            {
                case ENUM_RKBOOTENTRY.ENTRY471:
                    dwOffset = m_header.dw471EntryOffset;
                    ucCount = m_header.uc471EntryCount;
                    ucSize = m_header.uc471EntrySize;
                    break;
                case ENUM_RKBOOTENTRY.ENTRY472:
                    dwOffset = m_header.dw472EntryOffset;
                    ucCount = m_header.uc472EntryCount;
                    ucSize = m_header.uc472EntrySize;
                    break;
                case ENUM_RKBOOTENTRY.ENTRYLOADER:
                    dwOffset = m_header.dwLoaderEntryOffset;
                    ucCount = m_header.ucLoaderEntryCount;
                    ucSize = m_header.ucLoaderEntrySize;
                    break;
                default:
                    return -1;
            }

            for (byte i = 0; i < ucCount; i++)
            {
                if (GetEntryProperty(type, i, out _, out _, out string entryName))
                {
                    if (string.Equals(name, entryName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            return -1;
        }

        public bool GetEntryProperty(ENUM_RKBOOTENTRY type, byte ucIndex, out uint dwSize, out uint dwDelay, out string name)
        {
            dwSize = 0;
            dwDelay = 0;
            name = string.Empty;

            uint dwOffset;
            byte ucCount, ucSize;

            switch (type)
            {
                case ENUM_RKBOOTENTRY.ENTRY471:
                    dwOffset = m_header.dw471EntryOffset;
                    ucCount = m_header.uc471EntryCount;
                    ucSize = m_header.uc471EntrySize;
                    break;
                case ENUM_RKBOOTENTRY.ENTRY472:
                    dwOffset = m_header.dw472EntryOffset;
                    ucCount = m_header.uc472EntryCount;
                    ucSize = m_header.uc472EntrySize;
                    break;
                case ENUM_RKBOOTENTRY.ENTRYLOADER:
                    dwOffset = m_header.dwLoaderEntryOffset;
                    ucCount = m_header.ucLoaderEntryCount;
                    ucSize = m_header.ucLoaderEntrySize;
                    break;
                default:
                    return false;
            }

            if (ucIndex >= ucCount) return false;

            int entryPos = (int)(dwOffset + (uint)ucSize * ucIndex);
            byte[] entryBytes = new byte[ucSize];
            Array.Copy(m_bootData, entryPos, entryBytes, 0, ucSize);

            GCHandle handle = GCHandle.Alloc(entryBytes, GCHandleType.Pinned);
            var entry = Marshal.PtrToStructure<STRUCT_RKBOOT_ENTRY>(handle.AddrOfPinnedObject());
            handle.Free();

            dwSize = entry.dwDataSize;
            dwDelay = entry.dwDataDelay;
            name = entry.szName;

            return true;
        }

        public byte[]? GetEntryData(ENUM_RKBOOTENTRY type, byte ucIndex)
        {
            uint dwSize, dwDelay;
            string name;
            if (!GetEntryProperty(type, ucIndex, out dwSize, out dwDelay, out name))
                return null;

            uint dwOffset;
            switch (type)
            {
                case ENUM_RKBOOTENTRY.ENTRY471: dwOffset = m_header.dw471EntryOffset; break;
                case ENUM_RKBOOTENTRY.ENTRY472: dwOffset = m_header.dw472EntryOffset; break;
                case ENUM_RKBOOTENTRY.ENTRYLOADER: dwOffset = m_header.dwLoaderEntryOffset; break;
                default: return null;
            }

            int entryPos = (int)(dwOffset + (uint)m_header.ucLoaderEntrySize * ucIndex); // Assuming size is correct for type
            // Actually need to re-read entry for data offset
            byte ucSize = (type == ENUM_RKBOOTENTRY.ENTRY471) ? m_header.uc471EntrySize : (type == ENUM_RKBOOTENTRY.ENTRY472 ? m_header.uc472EntrySize : m_header.ucLoaderEntrySize);
            entryPos = (int)(dwOffset + (uint)ucSize * ucIndex);

            byte[] entryBytes = new byte[ucSize];
            Array.Copy(m_bootData, entryPos, entryBytes, 0, ucSize);
            GCHandle handle = GCHandle.Alloc(entryBytes, GCHandleType.Pinned);
            var entry = Marshal.PtrToStructure<STRUCT_RKBOOT_ENTRY>(handle.AddrOfPinnedObject());
            handle.Free();

            byte[] data = new byte[entry.dwDataSize];
            Array.Copy(m_bootData, (int)entry.dwDataOffset, data, 0, (int)entry.dwDataSize);
            return data;
        }
    }
}
