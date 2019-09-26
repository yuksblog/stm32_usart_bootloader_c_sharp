using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ST.Boot.USART {
    public class USARTBootloaderNACKException: Exception {

        public USARTBootloaderNACKException(string message) : base(message) { }

        public USARTBootloaderNACKException(string message, Exception inner) : base(message, inner) { }
    }
}
