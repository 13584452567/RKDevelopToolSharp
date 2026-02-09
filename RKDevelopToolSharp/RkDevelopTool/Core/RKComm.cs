using RkDevelopTool.Models;

namespace RkDevelopTool.Core
{
    public abstract class RKComm
    {
        protected STRUCT_RKDEVICE_DESC m_deviceDesc;
        protected RKLog? m_log;

        public RKComm(RKLog? pLog)
        {
            m_deviceDesc = new STRUCT_RKDEVICE_DESC();
            m_log = pLog;
        }

        public virtual void Dispose() { }

        public abstract bool RKU_Read(byte[] lpBuffer, uint dwSize);
        public abstract bool RKU_Write(byte[] lpBuffer, uint dwSize);
        public abstract int RKU_ReadChipInfo(byte[] lpBuffer);
        public abstract int RKU_ReadFlashID(byte[] lpBuffer);
        public abstract int RKU_ReadFlashInfo(byte[] lpBuffer, out uint puiRead);
        public abstract int RKU_EraseBlock(byte ucFlashCS, uint dwPos, uint dwCount, byte ucEraseType);
        public abstract int RKU_ReadCapability(byte[] lpBuffer);
        public abstract int RKU_ReadLBA(uint dwPos, uint dwCount, byte[] lpBuffer, RW_SUBCODE bySubCode = RW_SUBCODE.RWMETHOD_IMAGE);
        public abstract int RKU_WriteLBA(uint dwPos, uint dwCount, byte[] lpBuffer, RW_SUBCODE bySubCode = RW_SUBCODE.RWMETHOD_IMAGE);
        public abstract int RKU_WriteSector(uint dwPos, uint dwCount, byte[] lpBuffer);
        public abstract int RKU_EraseLBA(uint dwPos, uint dwCount);
        public abstract int RKU_ResetDevice(RESET_SUBCODE bySubCode = RESET_SUBCODE.RST_NONE_SUBCODE);
        public abstract int RKU_TestDeviceReady(out uint dwTotal, out uint dwCurrent, TESTUNIT_SUBCODE bySubCode = TESTUNIT_SUBCODE.TU_NONE_SUBCODE);
        public abstract int RKU_DeviceRequest(uint dwRequest, byte[] lpBuffer, uint dwDataSize);
        public abstract int RKU_ChangeStorage(byte storage);
        public abstract int RKU_ReadStorage(byte[] storage);
        public abstract bool Reset_Usb_Config(STRUCT_RKDEVICE_DESC devDesc);
        public abstract bool Reset_Usb_Device();
    }
}
