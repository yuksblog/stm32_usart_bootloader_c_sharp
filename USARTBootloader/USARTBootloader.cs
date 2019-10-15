using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ST.Boot.USART {
    /// <summary>
    /// STM32 の USART Bootloader と通信するコネクター
    /// </summary>
    public class USARTBootloader {

        private static byte[] Command_Init = new byte[] { 0x7F };

        private static byte[] Command_Get = new byte[] { 0x00, 0xFF };
        
        private static byte[] Command_GetVersionAndReadProtectionStatus = new byte[] { 0x01, 0xFE };

        private static byte[] Command_GetID = new byte[] { 0x02, 0xFD };

        private static byte[] Command_ReadMemory = new byte[] { 0x11, 0xEE };

        private static byte[] Command_Go = new byte[] { 0x21, 0xDE };

        private static byte[] Command_WriteMemory = new byte[] { 0x31, 0xCE };

        private static byte[] Command_Erase = new byte[] { 0x43, 0xBC };

        private static byte[] Global_Erase_Request = new byte[] { 0xFF, 0x00 };

        private static byte[] Command_ExtendedErase = new byte[] { 0x44, 0xBB };

        private static byte[] Command_WriteProtect = new byte[] { 0x63, 0x9C };

        private static byte[] Command_WriteUnprotect = new byte[] { 0x73 };

        private static byte[] Command_ReadoutProtect = new byte[] { 0x82 };

        private static byte[] Command_ReadoutUnprotect = new byte[] { 0x92 };

        private static byte Answer_ACK = 0x79;

        private static byte Answer_NACK = 0x1F;

        private static int RETRY_COUNT = 5;


        protected SerialPort port;

        protected bool isInitialized = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="port"></param>
        public USARTBootloader(SerialPort port) {
            this.port = port;
        }

        protected virtual void ClearBuffer() {
            port.DiscardOutBuffer();
            port.DiscardInBuffer();
        }
        
        /// <summary>
        /// ポートのオープン
        /// </summary>
        public virtual void Open() {
            if (!IsOpen()) {
                port.Open();
            }
        }

        /// <summary>
        /// ポートのクローズ
        /// </summary>
        public virtual void Close() {
            isInitialized = false;
            port.Close();
        }

        public virtual bool IsOpen() {
            if (port == null || port != null && !port.IsOpen) {
                return false;
            } else {
                return true;
            }
        }

        protected virtual void Send(byte[] req) {
            ClearBuffer();
            port.Write(req, 0, req.Length);
        }

        protected virtual int Receive() {
            return port.ReadByte();
        }

        protected virtual byte[] Receive(int length) {
            byte[] res = new byte[length];
            for (var i = 0; i < length;) {
                i += port.Read(res, i, length - i);
            }
            return res;
        }

        private void WaitACK() {
            var ack = Receive();
            if (ack == Answer_ACK) {
                // OK
            } else if (ack == Answer_NACK) {
                throw new USARTBootloaderNACKException("NACK was received.");
            } else {
                throw new InvalidOperationException("Invalid answer was received. answer=" + ack);
            }
        }

        private byte GetCheckSum(byte[] data, int offset, int count) {
            byte xor = 0xff;
            for (var i = offset; i < count + offset; i++) {
                xor ^= data[i];
            }

            // 全て足した時に0になるように、補数を取る
            return (byte)~xor;
        }

        private byte[] GetAddressBytes(uint address) {
            var addr = new byte[5];
            addr[0] = (byte)((address >> 24) & 0xFF);
            addr[1] = (byte)((address >> 16) & 0xFF);
            addr[2] = (byte)((address >> 8) & 0xFF);
            addr[3] = (byte)(address & 0xFF);
            addr[4] = GetCheckSum(addr, 0, 4);

            return addr;
        }

        private void CheckStatus() {
            if (!IsOpen() || (IsOpen() && !isInitialized)) {
                throw new InvalidOperationException("Device is not initialized yet.");
            }
        }

        public void Init() {
            if (!IsOpen()) {
                throw new InvalidOperationException("Connection is not opened yet.");
            }

            for (var i = 0; i < RETRY_COUNT; i++) {

                Exception ex;
                try {
                    // 初期化コマンド送信
                    Send(Command_Init);

                    // ACKを待つ
                    WaitACK();
                    break;

                } catch (USARTBootloaderNACKException bne) {
                    // NACKでもOK
                    break;
                } catch (Exception ee) {
                    // それ以外はNG
                    ex = ee;
                }

                if (i == RETRY_COUNT - 1) {
                    throw new InvalidOperationException("Device is in illegal state. Failed to initialized.", ex);
                }
            }

            isInitialized = true;
        }

        /// <summary>
        /// Bootloaderのバージョンと、サポートしているコマンドのリストを取得する
        /// </summary>
        /// <returns></returns>
        public SupportedCommands Get() {
            CheckStatus();

            // Getコマンド
            Send(Command_Get);

            // ACK確認
            WaitACK();

            // バイト数受信
            SupportedCommands commands = new SupportedCommands();
            var bytes = Receive();
            commands.Commands = new int[bytes - 1];

            // データ受信
            commands.Version = Receive();
            for (var i = 0; i < bytes - 1; i++) {
                commands.Commands[i] = Receive();
            }

            // ACK確認
            WaitACK();

            return commands;
        }
        
        /// <summary>
        /// Bootloaderのバージョンと、OptionByte1/2を取得する
        /// </summary>
        /// <returns></returns>
        public VersionAndReadProtectionStatus GetVersionAndReadProtectionStatus() {
            CheckStatus();

            // GetVersionAndReadProtectionStatusコマンド
            Send(Command_GetVersionAndReadProtectionStatus);

            // ACK確認
            WaitACK();

            // データ受信
            VersionAndReadProtectionStatus status = new VersionAndReadProtectionStatus();
            status.Version = Receive();
            status.OptionByte1 = Receive();
            status.OptionByte2 = Receive();

            // ACK確認
            WaitACK();

            return status;
        }

        /// <summary>
        /// プロダクトIDを取得する
        /// </summary>
        /// <returns></returns>
        public int GetID() {
            CheckStatus();

            // Getコマンド
            Send(Command_GetID);

            // ACK確認
            WaitACK();

            // バイト数受信(1固定)
            var bytes = Receive() + 1;

            // データ受信
            byte[] tmp = Receive(bytes);
            int pid = (tmp[0] << 8) + tmp[1];

            // ACK確認
            WaitACK();

            return pid;
        }

        /// <summary>
        /// メモリーからデータを取得する
        /// </summary>
        /// <param name="address">フラッシュメモリーの開始アドレス</param>
        /// <param name="size">取得するデータサイズ(byte), 1～256</param>
        /// <returns></returns>
        public byte[] ReadMemory(uint address, int size) {
            CheckStatus();

            // サイズチェック
            if (size > 256 || size < 1) {
                throw new InvalidOperationException("Read size is 1 to 256. size=" + size);
            }

            // ReadMemoryコマンド
            Send(Command_ReadMemory);

            // ACK確認
            WaitACK();

            // 開始アドレス送信
            Send(GetAddressBytes(address));

            // ACK確認
            WaitACK();

            // 受信サイズ送信
            byte[] req = new byte[2];
            req[0] = (byte)(size - 1);
            req[1] = (byte)~GetCheckSum(req, 0, 1);    // 補数を取らないとNACKになる。Bootloaderのバグでは？
            Send(req);

            // ACK確認
            WaitACK();

            // 受信
            byte[] data = Receive(size);

            return data;

        }

        /// <summary>
        /// ファームウェアを起動する
        /// </summary>
        /// <param name="address">ファームウェアの開始アドレス</param>
        public void Go(uint address) {
            CheckStatus();

            // Goコマンド
            Send(Command_Go);

            // ACK確認
            WaitACK();

            // 開始アドレス送信
            Send(GetAddressBytes(address));

            // ACK確認
            WaitACK();
        }

        /// <summary>
        /// フレッシュメモリーのページを削除する
        /// </summary>
        /// <param name="index">ページ, 0～</param>
        public void EraseMemory(byte index) {
            CheckStatus();

            // EraseMemoryコマンド
            Send(Command_Erase);

            // ACK確認
            WaitACK();

            if (index == 0xFF) {
                // Global Erase 送信
                Send(Global_Erase_Request);

                // ACK確認
                WaitACK();

            } else {
                // 消去対象のページインデックスを送信
                byte[] erase = new byte[3];
                erase[0] = 0x00;
                erase[1] = index;
                erase[2] = GetCheckSum(erase, 0, 2);
                Send(erase);

                // ACK確認
                WaitACK();
            }
        }

        /// <summary>
        /// フレッシュメモリーのページを削除する. EraseMemoryより大きいサイズを指定できる.
        /// </summary>
        /// <param name="index"></param>
        public void ExtendedEraseMemory(ushort index) {
            CheckStatus();

            // indexチェック
            if (index >= 0xFFF0 && index <= 0xFFFC) {
                throw new InvalidOperationException("0xFFF9 to 0xFFFC are reserved. index=" + index);
            }

            // ExtendedEraseMemoryコマンド
            Send(Command_ExtendedErase);

            // ACK確認
            WaitACK();

            // 削除indexを送信
            byte[] erase = new byte[3];
            if (index == 0xFFFF
                || index == 0xFFFE
                || index == 0xFFFD) {
                // 特殊Eraseはそのまま送る
            } else {
                // 通常indexは(N-1)で指定する
                index -= 1;
            }
            erase[0] = (byte)(index >> 4);
            erase[1] = (byte)(index & 0x00FF);
            erase[2] = GetCheckSum(erase, 0, 2);
            Send(erase);

            // ACK確認
            WaitACK();
        }

        /// <summary>
        /// ファームウェアを書き込む.
        /// </summary>
        /// <param name="address">開始アドレス</param>
        /// <param name="datas">データ配列(byte), 最大256バイト</param>
        public void WriteMemory(uint address, byte[] datas) {
            CheckStatus();

            // WriteMemoryコマンド
            Send(Command_WriteMemory);

            // ACK確認
            WaitACK();

            // 開始アドレス送信
            Send(GetAddressBytes(address));

            // ACK確認
            WaitACK();

            // データ送信
            byte[] req = new byte[datas.Length + 2];
            req[0] = (byte)(datas.Length - 1);
            Buffer.BlockCopy(datas, 0, req, 1, datas.Length);
            req[req.Length - 1] = GetCheckSum(req, 0, req.Length - 1);
            Send(req);

            // ACK確認
            WaitACK();
        }

        public void WriteProtect() {
            throw new NotImplementedException("WriteProtect is not implemented.");
        }

        public void WriteUnprotect() {
            throw new NotImplementedException("WriteUnprotect is not implemented.");
        }

        public void ReadProtect() {
            throw new NotImplementedException("ReadProtect is not implemented.");
        }

        public void ReadUnprotect() {
            throw new NotImplementedException("ReadUnprotect is not implemented.");
        }

        /// <summary>
        /// RTSを操作する
        /// </summary>
        /// <param name="enable"></param>
        public virtual void SetRts(bool enable) {
            port.RtsEnable = enable;
        }

        /// <summary>
        /// DTRを操作する
        /// </summary>
        /// <param name="enable"></param>
        public virtual void SetDtr(bool enable) {
            port.DtrEnable = enable;
        }

    }
}
