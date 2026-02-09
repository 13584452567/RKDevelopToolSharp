using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using RkDevelopTool.Models;

namespace RkDevelopTool.Utils
{
    public static class GptUtils
    {
        public const ulong GPT_SIGNATURE = 0x5452415020494645; // "EFI PART"

        public static bool GetLbaFromGpt(byte[] buffer, string name, out ulong startLba, out ulong endLba)
        {
            startLba = 0;
            endLba = 0;

            if (buffer == null || buffer.Length < 34 * 512) return false;

            // GPT Header is in sector 1
            GptHeader header = MemoryMarshal.Read<GptHeader>(buffer.AsSpan(512));
            if (header.Signature != GPT_SIGNATURE) return false;

            int entrySize = (int)header.SizeofPartitionEntry;
            int numEntries = (int)header.NumPartitionEntries;
            int entryArrayOffset = (int)(header.PartitionEntryLba * 512);

            for (int i = 0; i < numEntries; i++)
            {
                int currentEntryOffset = entryArrayOffset + (i * entrySize);
                if (currentEntryOffset + entrySize > buffer.Length) break;

                GptEntry entry = MemoryMarshal.Read<GptEntry>(buffer.AsSpan(currentEntryOffset));
                if (entry.PartitionTypeGuid == Guid.Empty) continue;

                string entryName = GetGptName(entry);
                if (string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
                {
                    startLba = entry.StartingLba;
                    endLba = entry.EndingLba;
                    return true;
                }
            }

            return false;
        }

        private static unsafe string GetGptName(GptEntry entry)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 36; i++)
            {
                char c = (char)entry.PartitionName[i];
                if (c == 0) break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static byte[] CreateGptBuffer(List<ParamItem> parts, ulong diskSectors)
        {
            byte[] buffer = new byte[34 * 512];
            
            // Protective MBR (Sector 0)
            LegacyMbr mbr = new LegacyMbr();
            mbr.Signature = 0xAA55;
            mbr.PartitionRecord0.BootInd = 0x00;
            mbr.PartitionRecord0.SysInd = 0xEE;
            mbr.PartitionRecord0.StartSect = 1;
            mbr.PartitionRecord0.NrSects = diskSectors > uint.MaxValue ? uint.MaxValue : (uint)diskSectors - 1;
            
            MemoryMarshal.Write(buffer.AsSpan(0), ref mbr);

            // GPT Header (Sector 1)
            GptHeader header = new GptHeader();
            header.Signature = GPT_SIGNATURE;
            header.Revision = 0x00010000;
            header.HeaderSize = (uint)Marshal.SizeOf<GptHeader>();
            header.MyLba = 1;
            header.AlternateLba = diskSectors - 1;
            header.FirstUsableLba = 34;
            header.LastUsableLba = diskSectors - 34;
            header.DiskGuid = Guid.NewGuid();
            header.PartitionEntryLba = 2;
            header.NumPartitionEntries = 128;
            header.SizeofPartitionEntry = 128;

            int entryArrayOffset = 2 * 512;
            for (int i = 0; i < parts.Count && i < 128; i++)
            {
                GptEntry entry = new GptEntry();
                entry.PartitionTypeGuid = Guid.NewGuid(); // Should be specific for Rockchip if possible
                entry.UniquePartitionGuid = Guid.NewGuid();
                entry.StartingLba = parts[i].ItemOffset;
                entry.EndingLba = parts[i].ItemOffset + parts[i].ItemSize - 1;
                
                SetGptName(ref entry, parts[i].ItemName);
                
                MemoryMarshal.Write(buffer.AsSpan(entryArrayOffset + i * 128), ref entry);
            }

            // Calculate Entry Array CRC
            header.PartitionEntryArrayCrc32 = CRCUtils.crc32_le(0, buffer.AsSpan(entryArrayOffset, 128 * 128));
            
            // Calculate Header CRC (HeaderCRC must be 0 during calculation)
            header.HeaderCrc32 = 0;
            MemoryMarshal.Write(buffer.AsSpan(512), ref header);
            header.HeaderCrc32 = CRCUtils.crc32_le(0, buffer.AsSpan(512, (int)header.HeaderSize));
            
            // Write final header
            MemoryMarshal.Write(buffer.AsSpan(512), ref header);

            return buffer;
        }

        private static unsafe void SetGptName(ref GptEntry entry, string name)
        {
            fixed (ushort* p = entry.PartitionName)
            {
                int len = Math.Min(name.Length, 35);
                for (int i = 0; i < len; i++)
                {
                    p[i] = name[i];
                }
                p[len] = 0;
            }
        }
    }
}
