using System.Buffers.Binary;

namespace RkDevelopTool.Utils
{
    public static class EndianUtils
    {
        public static ushort Swap16(ushort value) => BinaryPrimitives.ReverseEndianness(value);

        public static uint Swap32(uint value) => BinaryPrimitives.ReverseEndianness(value);

        public static ushort LtoB16(ushort value) => BinaryPrimitives.ReverseEndianness(value);
        public static ushort BtoL16(ushort value) => BinaryPrimitives.ReverseEndianness(value);
        public static uint LtoB32(uint value) => BinaryPrimitives.ReverseEndianness(value);
        public static uint BtoL32(uint value) => BinaryPrimitives.ReverseEndianness(value);
    }
}
