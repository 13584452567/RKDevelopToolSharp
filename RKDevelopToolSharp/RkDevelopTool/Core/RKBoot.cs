using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using RkDevelopTool.Models;
using RkDevelopTool.Utils;

namespace RkDevelopTool.Core
{
    public class RKBoot
    {
        private byte[] _bootData;
        private uint _bootSize;
        private RkBootHead _bootHead;
        private bool _rc4Disable;
        private bool _signFlag;

        public bool Rc4DisableFlag => _rc4Disable;
        public bool SignFlag => _signFlag;
        public uint Version => _bootHead.Version;
        public uint MergeVersion => _bootHead.MergeVersion;
        public RkTime ReleaseTime => _bootHead.ReleaseTime;
        public RkDeviceType SupportDevice => _bootHead.SupportChip;
        public byte Entry471Count => _bootHead.Entry471Count;
        public byte Entry472Count => _bootHead.Entry472Count;
        public byte EntryLoaderCount => _bootHead.LoaderEntryCount;

        public RKBoot(byte[] lpBootData, out bool bCheck)
        {
            _bootData = lpBootData;
            _bootSize = (uint)lpBootData.Length;
            bCheck = Initialize();
        }

        private bool Initialize()
        {
            if (_bootData == null || _bootSize < Marshal.SizeOf<RkBootHead>())
            {
                return false;
            }

            if (!CrcCheck())
            {
                return false;
            }

            _bootHead = MemoryMarshal.Read<RkBootHead>(_bootData);

            if (_bootHead.Tag != 0x544F4F42 && _bootHead.Tag != 0x2052444C) // 'BOOT' and 'LDR '
            {
                return false;
            }

            _rc4Disable = _bootHead.Rc4Flag != 0;
            _signFlag = _bootHead.SignFlag == (byte)'S';

            return true;
        }

        public bool CrcCheck()
        {
            if (_bootData == null || _bootSize < 4)
                return false;

            uint oldCrc = BitConverter.ToUInt32(_bootData, (int)(_bootSize - 4));
            uint newCrc = CRCUtils.CRC_32(_bootData.AsSpan(0, (int)(_bootSize - 4)));
            return oldCrc == newCrc;
        }

        public bool SaveEntryFile(RkBootEntryType type, byte index, string fileName)
        {
            if (!GetEntryInfo(type, out uint offset, out byte count, out byte size))
            {
                return false;
            }

            if (index >= count)
            {
                return false;
            }

            int entryOffset = (int)(offset + (size * index));
            RkBootEntry entry = MemoryMarshal.Read<RkBootEntry>(_bootData.AsSpan(entryOffset));

            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(_bootData, (int)entry.DataOffset, (int)entry.DataSize);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool GetEntryProperty(RkBootEntryType type, byte index, out uint dwSize, out uint dwDelay, out string name)
        {
            dwSize = 0;
            dwDelay = 0;
            name = string.Empty;

            if (!GetEntryInfo(type, out uint offset, out byte count, out byte size))
            {
                return false;
            }

            if (index >= count)
            {
                return false;
            }

            int entryOffset = (int)(offset + (size * index));
            RkBootEntry entry = MemoryMarshal.Read<RkBootEntry>(_bootData.AsSpan(entryOffset));

            dwSize = entry.DataSize;
            dwDelay = entry.DataDelay;
            unsafe
            {
                name = Marshal.PtrToStringAnsi((IntPtr)entry.Name) ?? string.Empty;
            }

            return true;
        }

        public int GetIndexByName(RkBootEntryType type, string pName)
        {
            if (!GetEntryInfo(type, out uint offset, out byte count, out byte size))
            {
                return -1;
            }

            for (byte i = 0; i < count; i++)
            {
                int entryOffset = (int)(offset + (size * i));
                RkBootEntry entry = MemoryMarshal.Read<RkBootEntry>(_bootData.AsSpan(entryOffset));
                string name;
                unsafe
                {
                    name = Marshal.PtrToStringAnsi((IntPtr)entry.Name) ?? string.Empty;
                }
                if (string.Equals(name, pName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        public byte[]? GetEntryData(RkBootEntryType type, byte index)
        {
            if (!GetEntryInfo(type, out uint offset, out byte count, out byte size))
            {
                return null;
            }

            if (index >= count)
            {
                return null;
            }

            int entryOffset = (int)(offset + (size * index));
            RkBootEntry entry = MemoryMarshal.Read<RkBootEntry>(_bootData.AsSpan(entryOffset));

            byte[] data = new byte[entry.DataSize];
            Array.Copy(_bootData, (int)entry.DataOffset, data, 0, (int)entry.DataSize);
            return data;
        }

        public bool GetEntryData(RkBootEntryType type, byte index, byte[] lpData)
        {
            if (!GetEntryInfo(type, out uint offset, out byte count, out byte size))
            {
                return false;
            }

            if (index >= count)
            {
                return false;
            }

            int entryOffset = (int)(offset + (size * index));
            RkBootEntry entry = MemoryMarshal.Read<RkBootEntry>(_bootData.AsSpan(entryOffset));

            if (lpData.Length < entry.DataSize)
            {
                return false;
            }

            Array.Copy(_bootData, (int)entry.DataOffset, lpData, 0, (int)entry.DataSize);
            return true;
        }

        private bool GetEntryInfo(RkBootEntryType type, out uint offset, out byte count, out byte size)
        {
            offset = 0;
            count = 0;
            size = 0;

            switch (type)
            {
                case RkBootEntryType.Entry471:
                    offset = _bootHead.Entry471Offset;
                    count = _bootHead.Entry471Count;
                    size = _bootHead.Entry471Size;
                    break;
                case RkBootEntryType.Entry472:
                    offset = _bootHead.Entry472Offset;
                    count = _bootHead.Entry472Count;
                    size = _bootHead.Entry472Size;
                    break;
                case RkBootEntryType.EntryLoader:
                    offset = _bootHead.LoaderEntryOffset;
                    count = _bootHead.LoaderEntryCount;
                    size = _bootHead.LoaderEntrySize;
                    break;
                default:
                    return false;
            }

            return true;
        }
    }
}
