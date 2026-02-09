using RkDevelopTool.Models;

namespace RkDevelopTool.Core
{
    public abstract class RKComm
    {
        protected RkDeviceDesc m_deviceDesc;
        protected RKLog? m_log;

        public RKComm(RKLog? log)
        {
            m_deviceDesc = new RkDeviceDesc();
            m_log = log;
        }

        public virtual void Dispose() { }

        public abstract bool Read(Memory<byte> buffer);
        public abstract bool Write(ReadOnlyMemory<byte> buffer);
        public abstract int ReadChipInfo(Memory<byte> buffer);
        public abstract int ReadFlashID(Memory<byte> buffer);
        public abstract int ReadFlashInfo(Memory<byte> buffer, out uint bytesRead);
        public abstract int EraseBlock(byte flashCs, uint pos, uint count, byte eraseType);
        public abstract int ReadCapability(Memory<byte> buffer);
        public abstract int ReadLBA(uint pos, uint count, Memory<byte> buffer, RwSubCode subCode = RwSubCode.Image);
        public abstract int WriteLBA(uint pos, uint count, ReadOnlyMemory<byte> buffer, RwSubCode subCode = RwSubCode.Image);
        public abstract int WriteSector(uint pos, uint count, ReadOnlyMemory<byte> buffer);
        public abstract int EraseLBA(uint pos, uint count);
        public abstract int ResetDevice(ResetSubCode subCode = ResetSubCode.None);
        public abstract int TestDeviceReady(out uint total, out uint current, TestUnitSubCode subCode = TestUnitSubCode.None);
        public abstract int DeviceRequest(uint requestCode, ReadOnlyMemory<byte> buffer);
        public abstract int ChangeStorage(byte storage);
        public abstract int ReadStorage(Memory<byte> storage);
        public abstract bool Reset_Usb_Config(RkDeviceDesc deviceDesc);
        public abstract bool Reset_Usb_Device();
    }
}
