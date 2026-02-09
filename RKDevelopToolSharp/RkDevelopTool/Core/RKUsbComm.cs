using LibUsbDotNet;
using LibUsbDotNet.Main;
using RkDevelopTool.Models;
using RkDevelopTool.Utils;
using System.Runtime.InteropServices;

namespace RkDevelopTool.Core
{
/// <summary>
    /// 基于 USB 接口的通讯实现类，使用 LibUsbDotNet 与物理设备进行 Bulk 传输。
    /// </summary>
    public class RKUsbComm : RKComm, IDisposable
    {
        private UsbDevice? m_usbDevice;
        private UsbEndpointWriter? m_writer;
        private UsbEndpointReader? m_reader;
        private int m_interfaceNum = -1;

        public RKUsbComm(RkDeviceDesc deviceDesc, RKLog? log, out bool result) : base(log)
        {
            result = InitializeUsb(deviceDesc);
        }

        /// <summary>
        /// 初始化 USB 连接，查找并打开设备，声明接口并开启端点。
        /// </summary>
        public bool InitializeUsb(RkDeviceDesc deviceDesc)
        {
            m_deviceDesc = deviceDesc;
            
            var finder = new UsbDeviceFinder(deviceDesc.Vid, deviceDesc.Pid);
            m_usbDevice = UsbDevice.OpenUsbDevice(finder);

            if (m_usbDevice == null)
            {
                m_log?.Record("Error:InitializeUsb-->open device failed");
                return false;
            }

            if (m_usbDevice is IUsbDevice wholeUsbDevice)
            {
                // 设置配置并声明接口
                wholeUsbDevice.SetConfiguration(1);
                m_interfaceNum = 0; 
                wholeUsbDevice.ClaimInterface(m_interfaceNum);
            }

            // 打开写入和读取端点（通常 Rockchip 设备使用 EP1）
            m_writer = m_usbDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
            m_reader = m_usbDevice.OpenEndpointReader(ReadEndpointID.Ep01, 4096);

            return true;
        }

        public void UninitializeUsb()
        {
            if (m_usbDevice != null)
            {
                if (m_usbDevice.IsOpen)
                {
                    if (m_usbDevice is IUsbDevice wholeUsbDevice)
                    {
                        wholeUsbDevice.ReleaseInterface(m_interfaceNum);
                    }
                    m_usbDevice.Close();
                }
                m_usbDevice = null;
            }
        }

        public override void Dispose()
        {
            UninitializeUsb();
        }

        public override bool Reset_Usb_Config(RkDeviceDesc deviceDesc)
        {
            UninitializeUsb();
            return InitializeUsb(deviceDesc);
        }

        public override bool Reset_Usb_Device()
        {
            // 在 LibUsbDotNet 中，简单的重置通常通过重新打开设备实现。
            // 所以 LibUsbDotNet 什么时候发布 3.x 的 Release 呢？
            return m_usbDevice != null && m_usbDevice.IsOpen;
        }

        /// <summary>
        /// 从 USB 设备读取指定长度的数据。
        /// </summary>
        public override bool Read(Memory<byte> buffer)
        {
            int transferred;
            byte[] arr;
            int offset = 0;
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
            {
                arr = segment.Array!;
                offset = segment.Offset;
            }
            else
            {
                arr = new byte[buffer.Length];
            }

            ErrorCode ec = m_reader!.Read(arr, offset, buffer.Length, 5000, out transferred);
            if (ec != ErrorCode.None || transferred != buffer.Length)
            {
                m_log?.Record($"Error:Read failed, ec={ec}, size={buffer.Length}, read={transferred}");
                return false;
            }

            if (segment.Array == null)
            {
                arr.AsSpan().CopyTo(buffer.Span);
            }

            return true;
        }

        /// <summary>
        /// 从 USB 设备读取数据，但不强制检查读取长度是否等于预期长度。
        /// </summary>
        public uint ReadEx(Memory<byte> buffer)
        {
            int transferred;
            byte[] arr;
            int offset = 0;
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
            {
                arr = segment.Array!;
                offset = segment.Offset;
            }
            else
            {
                arr = new byte[buffer.Length];
            }

            ErrorCode ec = m_reader!.Read(arr, offset, buffer.Length, 5000, out transferred);
            if (ec != ErrorCode.None)
            {
                m_log?.Record($"Error:ReadEx failed, ec={ec}, size={buffer.Length}");
                return 0;
            }

            if (segment.Array == null)
            {
                arr.AsSpan(0, transferred).CopyTo(buffer.Span);
            }

            return (uint)transferred;
        }

        /// <summary>
        /// 向 USB 设备写入指定长度的数据。
        /// </summary>
        public override bool Write(ReadOnlyMemory<byte> buffer)
        {
            int transferred;
            byte[] arr;
            int offset = 0;
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
            {
                arr = segment.Array!;
                offset = segment.Offset;
            }
            else
            {
                arr = buffer.ToArray();
            }

            ErrorCode ec = m_writer!.Write(arr, offset, buffer.Length, 5000, out transferred);
            if (ec != ErrorCode.None || transferred != buffer.Length)
            {
                m_log?.Record($"Error:Write failed, ec={ec}, size={buffer.Length}, write={transferred}");
                return false;
            }
            return true;
        }

        private uint MakeCbwTag()
        {
            uint tag = 0;
            Span<byte> bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref tag, 1));
            Random.Shared.NextBytes(bytes);
            return tag;
        }

        /// <summary>
        /// 根据操作码初始化命令块包装 (Cbw)。
        /// </summary>
        private void InitializeCbw(ref Cbw cbw, UsbOperationCode code)
        {
            cbw.Signature = RKCommConstants.CBW_SIGN;
            cbw.Tag = MakeCbwTag();
            cbw.Cbwcb.OperCode = (byte)code;

            switch (code)
            {
                case UsbOperationCode.TestUnitReady:
                case UsbOperationCode.ReadFlashId:
                case UsbOperationCode.ReadFlashInfo:
                case UsbOperationCode.ReadChipInfo:
                case UsbOperationCode.ReadEfuse:
                case UsbOperationCode.ReadCapability:
                case UsbOperationCode.ReadStorage:
                    cbw.Flags = RKCommConstants.DIRECTION_IN;
                    cbw.CbLength = 0x06;
                    break;
                case UsbOperationCode.DeviceReset:
                case UsbOperationCode.EraseSystemDisk:
                case UsbOperationCode.SetResetFlag:
                case UsbOperationCode.ChangeStorage:
                    cbw.Flags = RKCommConstants.DIRECTION_OUT;
                    cbw.CbLength = 0x06;
                    break;
                case UsbOperationCode.TestBadBlock:
                case UsbOperationCode.ReadSector:
                case UsbOperationCode.ReadLba:
                case UsbOperationCode.ReadSdram:
                case UsbOperationCode.ReadSpiFlash:
                case UsbOperationCode.ReadNewEfuse:
                    cbw.Flags = RKCommConstants.DIRECTION_IN;
                    cbw.CbLength = 0x0a;
                    break;
                case UsbOperationCode.WriteSector:
                case UsbOperationCode.WriteLba:
                case UsbOperationCode.WriteSdram:
                case UsbOperationCode.ExecuteSdram:
                case UsbOperationCode.EraseNormal:
                case UsbOperationCode.EraseForce:
                case UsbOperationCode.WriteEfuse:
                case UsbOperationCode.WriteSpiFlash:
                case UsbOperationCode.WriteNewEfuse:
                case UsbOperationCode.EraseLba:
                    cbw.Flags = RKCommConstants.DIRECTION_OUT;
                    cbw.CbLength = 0x0a;
                    break;
            }
        }

        private bool UfiCheckSign(Cbw cbw, Csw csw)
        {
            return csw.Signature == RKCommConstants.CSW_SIGN && csw.Tag == cbw.Tag;
        }

        private bool RkuClearBuffer(Cbw cbw, ref Csw csw)
        {
            uint totalRead = 0;
            int tryCount = 3;
            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            do
            {
                uint readBytes = ReadEx(cswBytes);
                if (readBytes == (uint)cswBytes.Length)
                {
                    csw = MemoryMarshal.Read<Csw>(cswBytes);
                    if (UfiCheckSign(cbw, csw))
                        return true;
                }
                else
                {
                    tryCount--;
                    Thread.Sleep(3000);
                }
                totalRead += readBytes;
                if (totalRead >= RKCommConstants.MAX_CLEAR_LEN)
                    break;
            } while (tryCount > 0);
            return false;
        }

        public override int ReadChipInfo(Memory<byte> buffer)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
            {
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;
            }

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ReadChipInfo);
            cbw.TransferLength = 16;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!Read(buffer[..16]))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ReadFlashID(Memory<byte> buffer)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ReadFlashId);
            cbw.TransferLength = 5;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!Read(buffer[..5]))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ReadFlashInfo(Memory<byte> buffer, out uint bytesRead)
        {
            bytesRead = 0;
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ReadFlashInfo);
            cbw.TransferLength = 11;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] infoBuffer = new byte[512];
            uint readCount = ReadEx(infoBuffer);
            if (readCount < 11 || readCount > 512)
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            infoBuffer.AsSpan(0, (int)readCount).CopyTo(buffer.Span);
            bytesRead = readCount;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ReadCapability(Memory<byte> buffer)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ReadCapability);
            cbw.TransferLength = 8;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] tempBuffer = new byte[8];
            uint readCount = ReadEx(tempBuffer);
            if (readCount != 8)
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            tempBuffer.AsSpan().CopyTo(buffer.Span);

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ReadLBA(uint pos, uint count, Memory<byte> buffer, RwSubCode subCode = RwSubCode.Image)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ReadLba);
            cbw.TransferLength = count * 512;
            cbw.Cbwcb.Address = EndianUtils.LtoB32(pos);
            cbw.Cbwcb.Length = EndianUtils.LtoB16((ushort)count);
            cbw.Cbwcb.Reserved = (byte)subCode;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!Read(buffer[..(int)(count * 512)]))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.Status == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int WriteLBA(uint pos, uint count, ReadOnlyMemory<byte> buffer, RwSubCode subCode = RwSubCode.Image)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.WriteLba);
            uint total = count * 512;
            cbw.TransferLength = total;
            cbw.Cbwcb.Address = EndianUtils.LtoB32(pos);
            cbw.Cbwcb.Length = EndianUtils.LtoB16((ushort)count);
            cbw.Cbwcb.Reserved = (byte)subCode;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!Write(buffer[..(int)total]))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.Status == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int WriteSector(uint pos, uint count, ReadOnlyMemory<byte> buffer)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            if (count > 32)
                return RKCommConstants.ERR_CROSS_BORDER;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ReadSector);
            uint total = count * 528;
            cbw.TransferLength = total;
            cbw.Cbwcb.Address = EndianUtils.LtoB32(pos);
            cbw.Cbwcb.Length = EndianUtils.LtoB16((ushort)count);

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!Write(buffer[..(int)total]))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.Status == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int EraseLBA(uint pos, uint count)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.EraseLba);
            cbw.Cbwcb.Address = EndianUtils.LtoB32(pos);
            cbw.Cbwcb.Length = EndianUtils.LtoB16((ushort)count);

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.Status == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ResetDevice(ResetSubCode subCode = ResetSubCode.None)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.DeviceReset);
            cbw.Cbwcb.Reserved = (byte)subCode;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
            {
                if (!RkuClearBuffer(cbw, ref csw))
                    return RKCommConstants.ERR_CMD_NOTMATCH;
            }

            if (csw.Status == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ChangeStorage(byte storage)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ChangeStorage);
            cbw.Cbwcb.Reserved = storage;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
            {
                if (!RkuClearBuffer(cbw, ref csw))
                    return RKCommConstants.ERR_CMD_NOTMATCH;
            }

            if (csw.Status == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int ReadStorage(Memory<byte> storage)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.ReadStorage);
            cbw.TransferLength = 4;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] bitsBuffer = new byte[4];
            uint readCount = ReadEx(bitsBuffer);
            if (readCount != 4)
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            uint storageBits = BitConverter.ToUInt32(bitsBuffer);

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            storage.Span[0] = 255;
            for (byte i = 0; i < 32; i++)
            {
                if ((storageBits & (1u << i)) != 0)
                {
                    storage.Span[0] = i;
                    break;
                }
            }
            return RKCommConstants.ERR_SUCCESS;
        }

        public override int TestDeviceReady(out uint total, out uint current, TestUnitSubCode subCode = TestUnitSubCode.None)
        {
            total = 0;
            current = 0;
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, UsbOperationCode.TestUnitReady);
            cbw.Cbwcb.Reserved = (byte)subCode;

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
            {
                if (!RkuClearBuffer(cbw, ref csw))
                    return RKCommConstants.ERR_CMD_NOTMATCH;
            }

            current = (csw.DataResidue >> 16);
            total = (csw.DataResidue & 0x0000FFFF);

            total = EndianUtils.BtoL16((ushort)total);
            current = EndianUtils.BtoL16((ushort)current);

            if (csw.Status == 1)
                return RKCommConstants.ERR_DEVICE_UNREADY;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int DeviceRequest(uint requestCode, ReadOnlyMemory<byte> buffer)
        {
            if (m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            if (requestCode != 0x0471 && requestCode != 0x0472)
                return RKCommConstants.ERR_REQUEST_NOT_SUPPORT;

            uint dataSize = (uint)buffer.Length;
            bool sendPendPacket = false;
            switch (dataSize % 4096)
            {
                case 4095:
                    dataSize++;
                    break;
                case 4094:
                    sendPendPacket = true;
                    break;
            }

            byte[] data = new byte[dataSize + 2];
            buffer.Span.CopyTo(data);

            ushort crcValue = CRCUtils.CRC_CCITT(data.AsSpan(0, (int)dataSize));
            data[dataSize] = (byte)((crcValue & 0xff00) >> 8);
            data[dataSize + 1] = (byte)(crcValue & 0x00ff);
            uint totalDataSize = dataSize + 2;

            uint totalSent = 0;
            if (m_usbDevice == null) return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            while (totalSent < totalDataSize)
            {
                uint sendBytes = Math.Min(totalDataSize - totalSent, 4096);
                var setup = new UsbSetupPacket(0x40, 0xC, 0, (short)requestCode, (short)sendBytes);
                int transferred;
                byte[] tempBuf = new byte[sendBytes];
                Array.Copy(data, (int)totalSent, tempBuf, 0, (int)sendBytes);
                bool success = m_usbDevice.ControlTransfer(ref setup, tempBuf, (int)sendBytes, out transferred);

                if (!success || transferred != (int)sendBytes)
                    return RKCommConstants.ERR_REQUEST_FAIL;

                totalSent += sendBytes;
            }

            if (sendPendPacket)
            {
                byte[] fillByte = new byte[1] { 0 };
                var setup = new UsbSetupPacket(0x40, 0xC, 0, (short)requestCode, 1);
                int transferred;
                m_usbDevice.ControlTransfer(ref setup, fillByte, 1, out transferred);
            }

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int EraseBlock(byte flashCs, uint pos, uint count, byte eraseType)
        {
            if (m_deviceDesc.UsbType != RkUsbType.Loader && m_deviceDesc.UsbType != RkUsbType.MaskRom)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            if (count > RKCommConstants.MAX_ERASE_BLOCKS)
                return RKCommConstants.ERR_CROSS_BORDER;

            Cbw cbw = Cbw.Create();
            InitializeCbw(ref cbw, (UsbOperationCode)eraseType);
            cbw.Lun = flashCs;
            cbw.Cbwcb.Address = EndianUtils.LtoB32(pos);
            cbw.Cbwcb.Length = EndianUtils.LtoB16((ushort)count);

            byte[] cbwBytes = new byte[Marshal.SizeOf<Cbw>()];
            MemoryMarshal.Write(cbwBytes, in cbw);
            if (!Write(cbwBytes))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<Csw>()];
            if (!Read(cswBytes))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = MemoryMarshal.Read<Csw>(cswBytes);
            if (!UfiCheckSign(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.Status == 1)
                return RKCommConstants.ERR_FOUND_BAD_BLOCK;

            return RKCommConstants.ERR_SUCCESS;
        }
    }
}
