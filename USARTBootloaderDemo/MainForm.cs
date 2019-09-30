using ST.Boot.USART;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace STM32_USART_Bootloader_Form {
    public partial class MainForm : Form {

        private static int FLASH_BLOCK_SIZE = 1024;
        //private static int FLASH_BLOCK_SIZE = 2048;

        private static int READ_WRITE_BLOCK_SIZE = 256;

        private static int BASE_ADDRESS = 0x08000000;

        public MainForm() {
            InitializeComponent();

            // Port Combo Box
            string[] portNames = SerialPort.GetPortNames();
            for (int i = 0; i < portNames.Length; i++) {
                PortCombo.Items.Add(portNames[i]);
            }

            // Baudrate Combo
            BaudrateCombo.SelectedIndex = 5;

            ToolStripStatusLabel.Text = "";
        }

        private byte[] GetBinary(string fname) {
            byte[] bin;

            if (fname.EndsWith(".hex")) {
                // HEXファイルの場合
                HexFileParser parser = new HexFileParser();
                parser.DefaultValue = "FF3F";
                parser.parseFile(fname);
                bin = parser.toBinary();

            } else {
                // それ以外の場合はバイナリファイルとして扱う
                using (var s = new FileStream(fname, FileMode.Open, FileAccess.Read)) {
                    bin = new byte[s.Length];
                    s.Read(bin, 0, bin.Length);
                }
            }

            return bin;
        }

        private void WriteButton_Click(object sender, EventArgs e) {

            // ポートチェック
            if (PortCombo.SelectedItem == null) {
                MessageBox.Show("ポートを選択してください。");
                return;
            }

            // ファイルチェック
            if (string.IsNullOrEmpty(FileNameText.Text)) {
                MessageBox.Show("ファイルを選択してください。");
                return;
            } else if (!(FileNameText.Text.EndsWith(".bin") || FileNameText.Text.EndsWith(".hex"))) {
                MessageBox.Show("バイナリファイルを選択してください。");
                return;
            }

            // プログレスバーの初期化
            ProgressBar.Value = 0;

            // バイナリファイルの取得
            byte[] bin = GetBinary(FileNameText.Text);

            USARTBootloader loader = null;
            try {
                // USARTBootloaderの初期化
                loader = OpenBootLoader();

                Stopwatch sw = new Stopwatch();
                sw.Start();

                // フラッシュメモリーの消去
                int count = EraseFlash(bin, loader);

                // リードライトの回数と、最後のバッファサイズの計算
                count = bin.Length / READ_WRITE_BLOCK_SIZE;
                int reminder = bin.Length % READ_WRITE_BLOCK_SIZE;
                if (reminder != 0) {
                    count++;
                }

                // バイナリファイルの書き込み
                WriteBinaryFile(bin, loader, count, reminder);

                // ベリファイチェック
                VerifyCheckBinaryFile(bin, loader, count, reminder);

                // 完了
                sw.Stop();
                ToolStripStatusLabel.Text = "完了！！ Total " + (decimal)sw.ElapsedMilliseconds / 1000 + "[s]";

            } finally {
                if (loader != null) {
                    loader.Close();
                }
            }
        }

        private void VerifyCheckBinaryFile(byte[] bin, USARTBootloader loader, int count, int reminder) {
            ToolStripStatusLabel.Text = "ベリファイチェック中。。。";
            this.Update();
            ProgressBar.Value = 0;
            ProgressLabel.Text = ProgressBar.Value.ToString() + "%";
            for (byte i = 0; i < count; i++) {

                // バッファサイズ計算
                int offset = i * READ_WRITE_BLOCK_SIZE;
                int buffersize = READ_WRITE_BLOCK_SIZE;
                if (i == count - 1) {
                    buffersize = reminder;
                }

                // リード&チェック
                for (var j = 0; j < 5; j++) {
                    try {
                        bool isFailed = true;
                        byte[] tmp = loader.ReadMemory((uint)(BASE_ADDRESS + offset), buffersize);
                        for (var k = 0; k < buffersize; k++) {
                            if (tmp[k] != bin[offset + k]) {
                                break;
                            }
                            if (k == buffersize - 1) {
                                isFailed = false;
                            }
                        }
                        if (!isFailed) {
                            break;
                        }
                    } catch (Exception ee) {

                    }
                }

                ProgressBar.Value = i * 100 / count;
                ProgressLabel.Text = ProgressBar.Value.ToString() + "%";
            }
            ProgressBar.Value = 100;
            ProgressLabel.Text = ProgressBar.Value.ToString() + "%";
        }

        private void WriteBinaryFile(byte[] bin, USARTBootloader loader, int count, int reminder) {
            ToolStripStatusLabel.Text = "ファームウェアの書き込み中。。。";
            this.Update();
            ProgressBar.Value = 0;
            ProgressLabel.Text = ProgressBar.Value.ToString() + "%";
            for (byte i = 0; i < count; i++) {
                int offset = i * READ_WRITE_BLOCK_SIZE;
                int buffersize = READ_WRITE_BLOCK_SIZE;
                if (i == count - 1) {
                    buffersize = reminder;
                }
                byte[] tmp = new byte[buffersize];
                Buffer.BlockCopy(bin, offset, tmp, 0, buffersize);
                loader.WriteMemory(
                    (uint)(BASE_ADDRESS + offset),
                    tmp);
                ProgressBar.Value = i * 100 / count;
                ProgressLabel.Text = ProgressBar.Value.ToString() + "%";
            }
            ProgressBar.Value = 100;
            ProgressLabel.Text = ProgressBar.Value.ToString() + "%";
        }

        private int EraseFlash(byte[] bin, USARTBootloader loader) {
            ToolStripStatusLabel.Text = "フラッシュメモリの消去中。。。";
            this.Update();
            int count = bin.Length / FLASH_BLOCK_SIZE;
            if (count % FLASH_BLOCK_SIZE != 0) {
                count++;
            }
            for (byte i = 0; i < count; i++) {
                loader.EraseMemory(i);
                ProgressBar.Value = i * 100 / count;
                ProgressLabel.Text = ProgressBar.Value.ToString() + "%";
            }

            return count;
        }

        private USARTBootloader OpenBootLoader() {
            SerialPort port = new SerialPort();
            port.PortName = PortCombo.SelectedItem.ToString();
            port.BaudRate = int.Parse(BaudrateCombo.SelectedItem.ToString());
            port.DataBits = 8;
            port.StopBits = StopBits.One;
            port.Parity = Parity.Even;
            port.ReadTimeout = 250;
            port.WriteTimeout = 250;

            USARTBootloader loader = new USARTBootloader(port);
            //FTDIUSARTBootloader loader = new FTDIUSARTBootloader(port);
            loader.Open();
            return loader;
        }

        private void RefButton_Click(object sender, EventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK) {
                FileNameText.Text = dialog.FileName;
            }
        }

    }
}
