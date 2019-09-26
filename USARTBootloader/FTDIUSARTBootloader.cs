using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FTD2XX_NET.FTDI;

namespace ST.Boot.USART {
    public class FTDIUSARTBootloader : USARTBootloader {

        private FTDI ftdi = null;

        private int latencyTime = 16;

        public FTDIUSARTBootloader(SerialPort port) : base(port) {}

        protected override void ClearBuffer() {
            ftdi.Purge(FT_PURGE.FT_PURGE_RX);
            ftdi.Purge(FT_PURGE.FT_PURGE_TX);
        }

        protected override void OpenInternal() {
            ftdi = new FTDI();

            // ポートの数
            uint portNum = 0;
            FT_STATUS ret = ftdi.GetNumberOfDevices(ref portNum);
            if (ret != FT_STATUS.FT_OK) {
                throw new InvalidOperationException("Could not get port size. FT_STATUS=" + ret);
            }

            for (uint i = 0; i < portNum; i++) {
                ret = ftdi.OpenByIndex(i);
                if (ret != FT_STATUS.FT_OK) {
                    throw new InvalidOperationException("Could not get a port. FT_STATUS=" + ret + ",portIndex=" + i);
                }
                string tmp;
                ret = ftdi.GetCOMPort(out tmp);
                if (ret != FT_STATUS.FT_OK) {
                    throw new InvalidOperationException("Could not get port name. FT_STATUS=" + ret + ",portIndex=" + i);
                }
                if (tmp == port.PortName) {
                    // Baudrate
                    ret = FT_STATUS.FT_OK;
                    ret = ftdi.SetBaudRate((uint)port.BaudRate);
                    if (ret != FT_STATUS.FT_OK) {
                        throw new InvalidOperationException("Could not set BaudRate. FT_STATUS=" + ret + ",BaudRate=" + port.BaudRate);
                    }

                    // DataBits
                    byte dataBits = FTDI.FT_DATA_BITS.FT_BITS_7;
                    if (port.DataBits == 8) {
                        dataBits = FTDI.FT_DATA_BITS.FT_BITS_8;
                    }

                    // StopBits
                    byte stopBits = FTDI.FT_STOP_BITS.FT_STOP_BITS_1;
                    if (port.StopBits == StopBits.Two) {
                        stopBits = FTDI.FT_STOP_BITS.FT_STOP_BITS_2;
                    }

                    // Parity
                    byte parity = FTDI.FT_PARITY.FT_PARITY_EVEN;
                    if (port.Parity == Parity.Odd) {
                        parity = FTDI.FT_PARITY.FT_PARITY_ODD;
                    } else if (port.Parity == Parity.None) {
                        parity = FTDI.FT_PARITY.FT_PARITY_NONE;
                    }

                    // DataBits, StopBits, Parity
                    ret = ftdi.SetDataCharacteristics(dataBits, stopBits, parity);
                    if (ret != FT_STATUS.FT_OK) {
                        throw new InvalidOperationException("Could not set DataCharacteristcs. FT_STATUS=" + ret
                            + ",DataBits=FT_BITS_8,StopBits=FT_STOP_BITS_8,Parity=" + parity);
                    }

                    // ReadTimeout, WriteTimeout
                    ret = ftdi.SetTimeouts((uint)port.ReadTimeout, (uint)port.WriteTimeout);
                    if (ret != FT_STATUS.FT_OK) {
                        throw new InvalidOperationException("Could not set Timeouts. FT_STATUS=" + ret
                            + ",ReadTimeout=" + port.ReadTimeout + ",WriteTimeout=" + port.WriteTimeout);
                    }

                    // LatencyTimeout
                    ret = ftdi.SetLatency((byte)this.latencyTime);
                    if (ret != FT_STATUS.FT_OK) {
                        throw new InvalidOperationException("Could not set LatencyTimeout. FT_STATUS=" + ret + ",LatencyTime=" + latencyTime);
                    }

                    break;

                } else {
                    ftdi.Close();
                }
            }

            if (!ftdi.IsOpen) {
                throw new InvalidOperationException("Could not found the port. portName=" + port.PortName);
            }
        }

        public override void Close() {
            if (ftdi != null && ftdi.IsOpen) {
                // バッファをクリア
                ClearBuffer();

                // ポートを閉じる
                FT_STATUS ret = ftdi.ResetPort();
                if (ret == FT_STATUS.FT_OK) {
                    ret = ftdi.Close();
                    if (ret != FT_STATUS.FT_OK) {
                        throw new InvalidOperationException("close failure.");
                    }
                } else {
                    throw new InvalidOperationException("reset failure.");
                }
                ftdi = null;
            }
        }

        public override bool IsOpen() {
            bool isOpen = false;
            if (ftdi != null) {
                isOpen = ftdi.IsOpen;
            }
            return isOpen;
        }

        protected override void Send(byte[] req) {
            // バッファをクリア
            ftdi.Purge(FT_PURGE.FT_PURGE_RX);
            ftdi.Purge(FT_PURGE.FT_PURGE_TX);

            // 送信
            uint numBytesWritten = 0;
            FT_STATUS status = ftdi.Write(req, req.Length, ref numBytesWritten);

            if (numBytesWritten != req.Length) {
                throw new InvalidOperationException("Failed to send. requestedFrameSize=" + req.Length + ",sentFrameSize=" + numBytesWritten);
            }
        }

        protected override int Receive() {
            byte[] res = Receive(1);
            return res[0];
        }

        protected override byte[] Receive(int length) {
            // 受信
            byte[] res = new byte[length];
            uint numBytesRead = 0;
            FT_STATUS status = ftdi.Read(res, (uint)length, ref numBytesRead);

            // 長さチェック
            if (numBytesRead < length) {
                throw new InvalidOperationException("Failed receiving frame. requestedFrameSize=" + length + ",recevedFrameSize=" + numBytesRead);
            }
            return res;
        }

        public override void SetRts(bool enable) {
            ftdi.SetRTS(enable);
        }

        public override void SetDtr(bool enable) {
            ftdi.SetDTR(enable);
        }

    }
}
