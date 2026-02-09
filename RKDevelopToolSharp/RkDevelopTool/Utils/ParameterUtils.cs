using System;
using System.Collections.Generic;
using System.Text;
using RkDevelopTool.Models;

namespace RkDevelopTool.Utils
{
    public static class ParameterUtils
    {
        public static List<ParamItem> ParsePartitions(string parameter)
        {
            List<ParamItem> items = new List<ParamItem>();
            string mtdpartsMarker = "mtdparts=";
            int mtdpartsIndex = parameter.IndexOf(mtdpartsMarker);
            if (mtdpartsIndex == -1) return items;

            string partsStr = parameter.Substring(mtdpartsIndex + mtdpartsMarker.Length);
            int colonIndex = partsStr.IndexOf(':');
            if (colonIndex != -1)
            {
                partsStr = partsStr.Substring(colonIndex + 1);
            }

            // Example: 0x00002000@0x00002000(uboot),0x00002000@0x00004000(trust)...
            string[] parts = partsStr.Split(',');
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                
                int atIndex = part.IndexOf('@');
                int parenOpenIndex = part.IndexOf('(');
                int parenCloseIndex = part.LastIndexOf(')');

                if (atIndex != -1 && parenOpenIndex != -1 && parenCloseIndex != -1)
                {
                    string sizeStr = part.Substring(0, atIndex).Trim();
                    string offsetStr = part.Substring(atIndex + 1, parenOpenIndex - atIndex - 1).Trim();
                    string name = part.Substring(parenOpenIndex + 1, parenCloseIndex - parenOpenIndex - 1).Trim();
                    if (name.Contains(':'))
                    {
                        name = name.Split(':')[0];
                    }

                    try
                    {
                        uint size = 0;
                        if (sizeStr != "-")
                        {
                            size = sizeStr.StartsWith("0x") ? Convert.ToUInt32(sizeStr, 16) : uint.Parse(sizeStr);
                        }
                        uint offset = offsetStr.StartsWith("0x") ? Convert.ToUInt32(offsetStr, 16) : uint.Parse(offsetStr);
                        
                        items.Add(new ParamItem
                        {
                            ItemName = name,
                            ItemOffset = offset,
                            ItemSize = size
                        });
                    }
                    catch { }
                }
            }
            return items;
        }

        public static byte[] CreateParameterBuffer(string parameter)
        {
            byte[] content = Encoding.ASCII.GetBytes(parameter);
            int bufferSize = (content.Length + 12 + 511) / 512 * 512;
            byte[] buffer = new byte[bufferSize];

            // "PARM" magic
            BitConverter.GetBytes(0x4D524150u).CopyTo(buffer, 0);
            BitConverter.GetBytes((uint)content.Length).CopyTo(buffer, 4);
            content.CopyTo(buffer, 8);

            // CRC at the end of the content
            uint crc = 0;
            for (int i = 0; i < content.Length + 8; i++)
                crc += buffer[i];
            
            BitConverter.GetBytes(crc).CopyTo(buffer, content.Length + 8);

            return buffer;
        }

        public static bool GetLbaFromParam(byte[] buffer, string name, out uint offset, out uint size)
        {
            offset = 0;
            size = 0;

            if (buffer == null || buffer.Length < 12) return false;

            // Check PARM magic 0x4D524150
            if (BitConverter.ToUInt32(buffer, 0) != 0x4D524150) return false;

            uint length = BitConverter.ToUInt32(buffer, 4);
            if (length > buffer.Length - 8) length = (uint)buffer.Length - 8;

            string parameter = Encoding.ASCII.GetString(buffer, 8, (int)length);
            var items = ParsePartitions(parameter);

            foreach (var item in items)
            {
                if (string.Equals(item.ItemName, name, StringComparison.OrdinalIgnoreCase))
                {
                    offset = item.ItemOffset;
                    size = item.ItemSize;
                    return true;
                }
            }

            return false;
        }
    }
}
