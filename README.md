# USARTBootloader
## What is USARTBootloader?
USARTBootloader is C# library for communicating to STM32 System Memory Boot Mode with USART.

USARTBootloaderは、STM32に標準に組み込まれているブートローダーと通信するためのC#ライブラリです。通信方式はUSARTモードが対象です。CAN、SPI、I2C、USB DFU、などは対象外です。

## What is STM32 System Memory Boot Mode?
STM32 System Memory Boot Mode is a embbeded bootloader that can program/read firmware. You can do that without debug cable, so that it is useful on production environments.

STM32 System Memory Boot Modeは、ファームウェアの書き込み／読込みを行うことができます。デバッグケーブルを使わなくても書込みが行えるので、ユーザ環境でのファームウェアアップデートに便利です。

## How to use USARTBootloader?
Check out USARTBootloader solution from this git. The solution is a VisualStudio project. Open the USARTBootloader.sln and build it all. You import a generated .dll file.  
Please refer USARTBootloaderDemo codes to know USARTBootloader API.

ここのgitからUSARTBootloaderのVisualStudioソリューションをチェックアウトしてください。チェックアウトしたら、ソリューションのビルドを行い、生成された.dllファイルをあなたのプロジェクトにインポートして使ってください。  
APIは、USARTBootloaderDemoを参考にしてください。

~~~
// create SerialPort
SerialPort port = new SerialPort();
port.PortName = ”COM3”;
port.BaudRate = 115200;
port.DataBits = 8;
port.StopBits = StopBits.One;
port.Parity = Parity.Even;
port.ReadTimeout = 250;
port.WriteTimeout = 250;

// create and open USARTBootloader
USARTBootloader loader = new USARTBootloader(port);
loader.Open();
loader.Init();

// separate binary file(bin) by 256[byte].
int count = bin.Length / 256;
int reminder = bin.Length % 256;
if (reminder != 0) {
    count++;
}

// program
for (byte i = 0; i < count; i++) {
    // copy to buffer
    int offset = i * 256;
    int buffersize = 256;
    if (i == count - 1) {
        buffersize = reminder;
    }
    byte[] tmp = new byte[buffersize];
    Buffer.BlockCopy(bin, offset, tmp, 0, buffersize);
    
    // program buffer
    loader.WriteMemory((uint)(BASE_ADDRESS + offset),tmp);
}

// close
loader.Close();
~~~
