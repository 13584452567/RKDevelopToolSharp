using System;
using RkDevelopTool.Core;
using RkDevelopTool.Models;

namespace RkDevelopTool.Tests
{
    public class MockRKComm : RKComm
    {
        public bool WriteLbaCalled { get; private set; }
        public uint LastWritePos { get; private set; }
        public uint LastWriteCount { get; private set; }

        public MockRKComm() : base(null) { }

        public override int WriteLBA(uint pos, uint count, ReadOnlyMemory<byte> buffer, RwSubCode subCode = RwSubCode.Image)
        {
            WriteLbaCalled = true;
            LastWritePos = pos;
            LastWriteCount = count;
            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ReadCapability(Memory<byte> buffer)
        {
            buffer.Span[0] = 0x05; // 0x1 (DirectLba) | 0x4 (First4mAccess)
            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ReadLBA(uint pos, uint count, Memory<byte> buffer, RwSubCode subCode = RwSubCode.Image) => RKCommConstants.ERR_SUCCESS;
        public override bool Read(Memory<byte> buffer) => true;
        public override bool Write(ReadOnlyMemory<byte> buffer) => true;
        public override int ReadChipInfo(Memory<byte> buffer) => RKCommConstants.ERR_SUCCESS;
        public override int ReadFlashID(Memory<byte> buffer) => RKCommConstants.ERR_SUCCESS;
        public override int ReadFlashInfo(Memory<byte> buffer, out uint bytesRead)
        {
            bytesRead = 0;
            return RKCommConstants.ERR_SUCCESS;
        }
        public override int EraseBlock(byte flashCs, uint pos, uint count, byte eraseType) => RKCommConstants.ERR_SUCCESS;
        public override int WriteSector(uint pos, uint count, ReadOnlyMemory<byte> buffer) => RKCommConstants.ERR_SUCCESS;
        public override int EraseLBA(uint pos, uint count) => RKCommConstants.ERR_SUCCESS;
        public override int ResetDevice(ResetSubCode subCode = ResetSubCode.None) => RKCommConstants.ERR_SUCCESS;
        public override int TestDeviceReady(out uint total, out uint current, TestUnitSubCode subCode = TestUnitSubCode.None)
        {
            total = 0;
            current = 0;
            return RKCommConstants.ERR_SUCCESS;
        }
        public override int DeviceRequest(uint requestCode, ReadOnlyMemory<byte> buffer) => RKCommConstants.ERR_SUCCESS;
        public override int ChangeStorage(byte storage) => RKCommConstants.ERR_SUCCESS;
        public override int ReadStorage(Memory<byte> storage) => RKCommConstants.ERR_SUCCESS;
        public override bool Reset_Usb_Config(RkDeviceDesc deviceDesc) => true;
        public override bool Reset_Usb_Device() => true;
    }
}
