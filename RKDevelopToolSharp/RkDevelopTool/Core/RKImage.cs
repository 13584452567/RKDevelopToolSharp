using RkDevelopTool.Models;
using RkDevelopTool.Utils;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace RkDevelopTool.Core
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct RkImageHead
    {
        public uint Tag;
        public ushort Size;
        public uint Version;
        public uint MergeVersion;
        public RkTime ReleaseTime;
        public RkDeviceType SupportChip;
        public uint BootOffset;
        public uint BootSize;
        public uint FwOffset;
        public uint FwSize;
        public fixed byte Reserved[61];
    }
    public class RKImage
    {
        private RkImageHead m_header;
        private string? m_filePath;
        private long m_fileSize;
        private byte[] m_md5 = new byte[32];
        private byte[] m_signMd5 = new byte[256];
        private int m_signMd5Size;
        private byte[] m_reserved = new byte[61];

        public RKBoot? BootObject;

        public uint Version => m_header.Version;
        public uint MergeVersion => m_header.MergeVersion;
        public RkTime ReleaseTime => m_header.ReleaseTime;
        public RkDeviceType SupportDevice => m_header.SupportChip;
        public OsType OsType => (OsType)BitConverter.ToUInt32(m_reserved, 4);
        public ushort BackupSize => BitConverter.ToUInt16(m_reserved, 12);
        public uint BootOffset => m_header.BootOffset;
        public uint BootSize => m_header.BootSize;
        public uint FwOffset => m_header.FwOffset;
        public long FwSize { get; private set; }
        public long ImageSize => m_fileSize;
        public bool SignFlag { get; private set; }

        public RKImage() { }
        public unsafe bool LoadImage(string path)
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
                        byte[] headBytes = new byte[Marshal.SizeOf<RkImageHead>()];
                        if (fs.Read(headBytes, 0, headBytes.Length) != headBytes.Length) return false;

                        m_header = MemoryMarshal.Read<RkImageHead>(headBytes);

                        if (m_header.Tag != 0x57464B52) return false; // "RKFW"

                        long ulFwSize;
                        ReadOnlySpan<byte> reservedSpan = MemoryMarshal.CreateReadOnlySpan(ref m_header.Reserved[0], 61);
                        if (reservedSpan[14] == (byte)'H' && reservedSpan[15] == (byte)'I')
                        {
                            ulFwSize = (long)BinaryPrimitives.ReadUInt32LittleEndian(reservedSpan.Slice(16, 4));
                            ulFwSize <<= 32;
                            ulFwSize += m_header.FwOffset;
                            ulFwSize += m_header.FwSize;
                        }
                        else
                        {
                            ulFwSize = (long)m_header.FwOffset + m_header.FwSize;
                        }

                        reservedSpan.CopyTo(m_reserved);

                        FwSize = ulFwSize - m_header.FwOffset;
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
                        m_header.BootOffset = 0;
                        m_header.BootSize = (uint)m_fileSize;
                        m_header.FwOffset = 0;
                        m_header.FwSize = 0;
                    }
                    
                    byte[] bootData = new byte[m_header.BootSize];
                    fs.Seek(m_header.BootOffset, SeekOrigin.Begin);
                    fs.ReadExactly(bootData);

                    bool bCheck;
                    BootObject = new RKBoot(bootData, out bCheck);
                    if (!bCheck) return false;

                    if (bOnlyBootFile)
                    {
                        m_header.SupportChip = BootObject.SupportDevice;
                        byte[] osTypeBytes = BitConverter.GetBytes((uint)OsType.RkOs);
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
            if (m_filePath == null || FwSize == 0) return false;
            try
            {
                using (FileStream fsSource = new FileStream(m_filePath, FileMode.Open, FileAccess.Read))
                using (FileStream fsDest = new FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    fsSource.Seek(FwOffset, SeekOrigin.Begin);
                    byte[] buffer = new byte[1024];
                    long remaining = FwSize;
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
}
