namespace RkDevelopTool.Utils
{
    public static class EndianUtils
    {
        public static ushort Swap16(ushort value)
        {
            return (ushort)(((value & 0xFF00) >> 8) | ((value & 0x00FF) << 8));
        }

        public static uint Swap32(uint value)
        {
            return ((value & 0x000000FF) << 24) |
                   ((value & 0x0000FF00) << 8) |
                   ((value & 0x00FF0000) >> 8) |
                   ((value & 0xFF000000) >> 24);
        }

        public static ushort LtoB16(ushort value) => Swap16(value);
        public static ushort BtoL16(ushort value) => Swap16(value);
        public static uint LtoB32(uint value) => Swap32(value);
        public static uint BtoL32(uint value) => Swap32(value);
    }
}
