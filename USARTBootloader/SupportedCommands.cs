using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ST.Boot.USART {
    public class SupportedCommands {

        public int Version;

        public int MajorVersion {
            get {
                return (Version >> 4) & 0x0F;
            }
        }

        public int MinorVersion {
            get {
                return Version & 0x0F;
            }
        }

        public int[] Commands;

    }
}
