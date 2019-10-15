using ST.Boot.USART;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FTD2XX_NET.FTDI;

namespace ST.Boot.USART {
    /// <summary>
    /// FTDIのFT232RのCBUSをIOポートとして使用できるように機能追加したコネクター
    /// </summary>
    public class FT232RUSARTBootloader : FTDIUSARTBootloader {

        private FT232R_EEPROM_STRUCTURE eeprom = null;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="port"></param>
        public FT232RUSARTBootloader(SerialPort port) : base(port) { }

        /// <summary>
        /// CBUSの設定を格納するクラス
        /// </summary>
        public class CBUSConfig {
            public byte cbus0;
            public byte cbus1;
            public byte cbus2;
            public byte cbus3;

            /// <summary>
            /// デフォルト設定で初期化するコンストラクタ
            /// </summary>
            public CBUSConfig() {
                this.cbus0 = FT_CBUS_OPTIONS.FT_CBUS_TXLED;
                this.cbus1 = FT_CBUS_OPTIONS.FT_CBUS_RXLED;
                this.cbus2 = FT_CBUS_OPTIONS.FT_CBUS_TXDEN;
                this.cbus3 = FT_CBUS_OPTIONS.FT_CBUS_PWRON;
            }

            /// <summary>
            /// 指定した設定で初期化するコンストラクタ
            /// </summary>
            /// <param name="cbus0"></param>
            /// <param name="cbus1"></param>
            /// <param name="cbus2"></param>
            /// <param name="cbus3"></param>
            public CBUSConfig(byte cbus0, byte cbus1, byte cbus2, byte cbus3) {
                this.cbus0 = cbus0;
                this.cbus1 = cbus1;
                this.cbus2 = cbus2;
                this.cbus3 = cbus3;
            }
        }

        /// <summary>
        /// CBUSのIOポートを指定するために使用するクラス
        /// </summary>
        public class CBUSBit {
            public bool cbus0;
            public bool cbus1;
            public bool cbus2;
            public bool cbus3;

            /// <summary>
            /// デフォルトコンストラクタ
            /// </summary>
            public CBUSBit() {
                this.cbus0 = false;
                this.cbus1 = false;
                this.cbus2 = false;
                this.cbus3 = false;
            }

            /// <summary>
            /// 指定したHi/Loで初期化するコンストラクタ
            /// </summary>
            /// <param name="cbus0"></param>
            /// <param name="cbus1"></param>
            /// <param name="cbus2"></param>
            /// <param name="cbus3"></param>
            public CBUSBit(bool cbus0, bool cbus1, bool cbus2, bool cbus3) {
                this.cbus0 = cbus0;
                this.cbus1 = cbus1;
                this.cbus2 = cbus2;
                this.cbus3 = cbus3;
            }
        }

        /// <summary>
        /// CBUSの設定を変更する
        /// </summary>
        /// <param name="configs"></param>
        public void SetCBusConfig(CBUSConfig configs) {
            if (!IsOpen()) {
                throw new InvalidOperationException("Connection is not opened yet.");
            }

            // EEPROM読込み
            eeprom = new FT232R_EEPROM_STRUCTURE();
            FT_STATUS ret = ftdi.ReadFT232REEPROM(eeprom);
            if (ret != FT_STATUS.FT_OK) {
                throw new InvalidOperationException("Failed to read EEPROM. FT_STATUS=" + ret);
            }

            // 設定が異なる場合のみ、EEPROMに書き込み
            if (eeprom.Cbus0 != configs.cbus0
                || eeprom.Cbus1 != configs.cbus1
                || eeprom.Cbus2 != configs.cbus2
                || eeprom.Cbus3 != configs.cbus3) {
                eeprom.Cbus0 = configs.cbus0;
                eeprom.Cbus1 = configs.cbus1;
                eeprom.Cbus2 = configs.cbus2;
                eeprom.Cbus3 = configs.cbus3;
                ret = ftdi.WriteFT232REEPROM(eeprom);
                if (ret != FT_STATUS.FT_OK) {
                    throw new InvalidOperationException("Failed to write EEPROM. FT_STATUS=" + ret);
                }
            }
        }

        /// <summary>
        /// CBUSのIOポートを操作する
        /// </summary>
        /// <param name="bits"></param>
        public void SetCBusBit(CBUSBit bits) {
            if (!IsOpen()) {
                throw new InvalidOperationException("Connection is not opened yet.");
            }
            if (eeprom == null) {
                throw new InvalidOperationException("CBUS is not configured yet.");
            }

            // ビットマスク作成
            byte mask = 0b00000000;
            if (eeprom.Cbus0 == FT_CBUS_OPTIONS.FT_CBUS_IOMODE) {
                mask = 0b00010000;
                if (bits.cbus0) {
                    mask |= 0b0001;
                }
            }
            if (eeprom.Cbus1 == FT_CBUS_OPTIONS.FT_CBUS_IOMODE) {
                mask |= 0b00100000;
                if (bits.cbus1) {
                    mask |= 0b0010;
                }
            }
            if (eeprom.Cbus2 == FT_CBUS_OPTIONS.FT_CBUS_IOMODE) {
                mask |= 0b01000000;
                if (bits.cbus2) {
                    mask |= 0b0100;
                }
            }
            if (eeprom.Cbus3 == FT_CBUS_OPTIONS.FT_CBUS_IOMODE) {
                mask |= 0b10000000;
                if (bits.cbus3) {
                    mask |= 0b1000;
                }
            }

            // ビット操作
            ftdi.SetBitMode(mask, 0x20);
        }

        /// <summary>
        /// CBUSのIOポートを読込む
        /// </summary>
        /// <returns></returns>
        public CBUSBit GetCBusBit() {
            if (!IsOpen()) {
                throw new InvalidOperationException("Connection is not opened yet.");
            }
            if (eeprom == null) {
                throw new InvalidOperationException("CBUS is not configured yet.");
            }

            // ビット読込み
            byte ret = 0;
            ftdi.GetPinStates(ref ret);

            // CBUSBit作成
            CBUSBit bits = new CBUSBit(false, false, false, false);
            if ((ret & 0b00000001) == 0b00000001) {
                bits.cbus0 = true;
            }
            if ((ret & 0b00000010) == 0b00000010) {
                bits.cbus1 = true;
            }
            if ((ret & 0b00000100) == 0b00000100) {
                bits.cbus2 = true;
            }
            if ((ret & 0b00001000) == 0b00001000) {
                bits.cbus3 = true;
            }

            return bits;
        }

    }
}
