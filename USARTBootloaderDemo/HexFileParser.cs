using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STM32_USART_Bootloader_Form {
    public class HexFileParser {
        /// <summary> hexファイル情報</summary>
        class HexFileData {
            public int Offset {
                get; set;
            } // オフセット   0～65536
            public int Length {
                get; set;
            } // データ長     0～255
            public string RecType {
                get; set;
            } // レコード種別 2byte
              // 00   データレコード
              // 01   ファイル終了レコード
              // 02   拡張セグメントアドレスレコード
              // 03   スタートセグメントアドレスレコード
              // 04   拡張リニアアドレスレコード
              // 05   スタートリニアアドレスレコード
            public string Data {
                get; set;
            }
        }

        /// <summary> 開始アドレス</summary>
        public int StartAddr {
            get {
                return startAddr;
            }
        }

        /// <summary> 終了アドレス</summary>
        public int EndAddr {
            get {
                return endAddr;
            }
        }

        // 解析エラー情報を取得する

        public List<string> ParseErrorInfo {
            get {
                return parseErrorInfo;
            }
        }

        public string DefaultValue {
            get; set;
        }
        
        /// <summary>開始アドレス</summary>
        private int startAddr;
        /// <summary>終了アドレス</summary>
        private int endAddr;

        /// <summary> Hexファイル情報リスト(元ファイル１行で１データ)</summary>
        private List<HexFileData> hexFileList = new List<HexFileData>();


        /// <summary>解析エラー情報</summary>
        private List<string> parseErrorInfo = new List<string>();


        //*********************************************************************
        /// <summary> コンストラクタ
        /// </summary>
        //*********************************************************************
        public HexFileParser() {
            DefaultValue = "00";

            clearParseInfo();
        }

        //*********************************************************************
        /// <summary>   解析情報をクリアする
        /// </summary>
        //*********************************************************************
        public void clearParseInfo() {
            startAddr = 0xFFFF;
            endAddr = 0x0000;
            parseErrorInfo.Clear();
            hexFileList.Clear();
        }

        //*********************************************************************
        /// <summary> 指定された行のデータを解析する
        /// </summary>
        /// <param name="lineNo">行番号(エラー出力用)</param>
        /// <param name="inData">hexファイル行データ</param>
        //*********************************************************************
        public void parseLine(int lineNo, string inData) {
            string startMark;       // 1byte    スタートマーク(":"固定)
            string byteCount;       // 2byte    データ長
            string offsetAddress;   // 4byte    データオフセット(開始位置)
            string recType;         // 2byte    レコード種別 00:データ
            string data;            // variable データ
            string checkSum;        // 2byte    チェックサム

            //--------------------
            // レコード長チェック
            //--------------------
            if (inData.Length < 11) {
                parseErrorInfo.Add("行:" + lineNo + " レコード長が不正です");
                return;
            }

            //------------------------------------
            // 入力レコードを、フィールド分割する
            //------------------------------------
            startMark = inData.Substring(0, 1);
            byteCount = inData.Substring(1, 2);
            offsetAddress = inData.Substring(3, 4);
            recType = inData.Substring(7, 2);
            data = inData.Substring(9, inData.Length - 11);
            checkSum = inData.Substring(inData.Length - 2);

            //--------------------
            // データチェック
            //--------------------

            // スタートマーク
            if (!startMark.Equals(":")) {
                parseErrorInfo.Add("行:" + lineNo + " スタートマークが':'では有りません[data=" + startMark + "]");
                return;
            }

            // バイト数
            if (!isHexaDecimal(byteCount)) {
                parseErrorInfo.Add("行:" + lineNo + " バイトカウントが16進文字列では有りません[data=" + byteCount + "]");
                return;
            }

            // オフセット
            if (!isHexaDecimal(offsetAddress)) {
                parseErrorInfo.Add("行:" + lineNo + " オフセットアドレスが16進文字列では有りません[data=" + offsetAddress + "]");
                return;
            }

            // レコード種別
            if (!isHexaDecimal(recType)) {
                parseErrorInfo.Add("行:" + lineNo + " レコード種別が16進文字列では有りません[data=" + recType + "]");
                return;
            }
            if (!recType.Equals("00") && !recType.Equals("01") && !recType.Equals("02") &&
                 !recType.Equals("03") && !recType.Equals("04") && !recType.Equals("05")) {
                parseErrorInfo.Add("行:" + lineNo + " レコード種別が00～05以外の値です[data=" + recType + "]");
                return;
            }

            // データ
            if (!isHexaDecimal(data)) {
                parseErrorInfo.Add("行:" + lineNo + " データが16進文字列では有りません[data=" + data + "]");
                return;
            }

            // チェックサム
            if (!isHexaDecimal(checkSum)) {
                parseErrorInfo.Add("行:" + lineNo + " チェックサムが16進文字列では有りません[data=" + checkSum + "]");
                return;
            }

            //------------------------------
            // チェックサムの整合性チェック
            //------------------------------

            // スタートマークの次の位置(2byte目)～チェックサムの手前までを合算する
            uint calcValue = 0;
            for (int loop = 1; loop < inData.Length - 2; loop += 2) {
                string curValue = inData.Substring(loop, 2);
                calcValue += (uint)Convert.ToInt32(curValue, 16);
            }
            calcValue = (~calcValue) + 1; // 2の補数を取る
            calcValue &= 0x000000FF;      // 下位16ビット分だけを抽出
            if (Convert.ToInt32(checkSum, 16) != calcValue) {
                parseErrorInfo.Add("行:" + lineNo + " チェックサムが一致しません[calc=" + calcValue.ToString() + ", data=" + checkSum + "]");
                return;
            }

            // 解析した値を覚える
            int start = Convert.ToInt32(offsetAddress, 16);
            int len = Convert.ToInt32(byteCount, 16);
            storeValue(start, len, recType, data);

        }


        //*********************************************************************
        /// <summary> 指定されたアドレスの値を取得する
        /// </summary>
        /// <param name="address">  取得対象アドレス</param>
        /// <returns>               メモリ値</returns>
        //*********************************************************************
        public string getValue(int address) {
            // 指定されたアドレスに対する定義を探す
            HexFileData targetData = null;
            foreach (HexFileData curData in hexFileList) {
                if (curData.Offset <= address && curData.Offset + curData.Length > address) {
                    targetData = curData;
                    break;
                }
            }

            // 指定されたアドレスの定義がない場合は、デフォルト値を返す
            if (targetData == null) {
                int wordLen = DefaultValue.Length / 2;  // １ワードの長さを求める
                int wordPos = address % wordLen;        // 要求されたアドレスが、ワード中のどこにあたるかかのoffsetを求める

                return DefaultValue.Substring(wordPos * 2, 2);
            }

            // 定義がある場合は、ファイルの内容を返す
            int pos = address - targetData.Offset;
            return targetData.Data.Substring(pos * 2, 2);
        }


        //*********************************************************************
        /// <summary>解析した文字列を保存する
        /// </summary>
        /// 
        /// <param name="recType">      レコード種別</param>
        /// <param name="data">         データ(空文字列の可能性もあり</param>
        //*********************************************************************
        private void storeValue(int start, int len, string recType, string data) {
            // データレコード以外は保存しない
            if (!recType.Equals("00")) {
                return;
            }

            //-------------------------------------------------------
            // このデータによって開始･終了アドレスが変わるかチェック
            //-------------------------------------------------------
            if (start < startAddr) {
                startAddr = start;
            }
            if (start + len > endAddr) {
                endAddr = start + len;
            }

            //-------------------------------------------------------
            // 追加すべきデータをセット
            //-------------------------------------------------------
            HexFileData hexFileData = new HexFileData();
            hexFileData.Offset = start;
            hexFileData.Length = len;
            hexFileData.RecType = recType;
            hexFileData.Data = data;

            //-------------------------------------------------------
            // 一覧に追加
            //-------------------------------------------------------
            hexFileList.Add(hexFileData);
        }

        //*********************************************************************
        /// <summary>指定された値が16進文字列であるかをチェックする
        ///          (= '0'～'F'のみで構成されているか)
        /// </summary>
        /// 
        /// <param name="inData">   チェック対象データ</param>
        /// <returns>true:16進文字列である, false:16進文字列ではない</returns>
        //*********************************************************************
        private bool isHexaDecimal(string inData) {
            foreach (Char data in inData) {
                if ((data >= '0' && data <= '9') ||
                     (data >= 'A' && data <= 'F')) {
                    continue;
                }

                // "0-9A-F"以外->値がおかしい
                return false;
            }

            // チェックOK
            return true;
        }

        public void parseFile(string fileName) {
            using (StreamReader reader = new StreamReader(fileName)) {
                int lineNo = 0;
                //-------------------------------
                // 全ての行を読み込むまで繰り返し
                //-------------------------------
                while (true) {
                    string line = reader.ReadLine();
                    if (line == null) {
                        break;
                    }

                    //-------------------------------------
                    // Hexファイルのフォーマット解析を行う
                    //-------------------------------------
                    parseLine(lineNo, line);
                    lineNo++;
                }
            }
        }

        public byte[] toBinary() {
            byte[] bin = new byte[EndAddr - StartAddr];

            int startAddr = StartAddr;
            int endAddr = EndAddr;
            for (int curAddr = startAddr; curAddr < endAddr; curAddr++) {
                string mCode = getValue(curAddr);

                bin[curAddr] = Convert.ToByte(mCode, 16);
                
            }

            return bin;
        }

    }
}
