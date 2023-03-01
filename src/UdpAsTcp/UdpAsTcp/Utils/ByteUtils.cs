using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdpAsTcp.Utils
{
    internal class ByteUtils
    {
        public static short B2S_BE(byte[] buffer, int start = 0)
        {
            var count = sizeof(short);
            var tmpArray = buffer.Skip(start).Take(count).ToArray();
            //如果是小端字节序，则交换
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tmpArray);
            return BitConverter.ToInt16(tmpArray, 0);
        }

        public static byte[] S2B_BE(short number)
        {
            var array = BitConverter.GetBytes(number);
            //如果是小端字节序，则交换
            if (BitConverter.IsLittleEndian)
                Array.Reverse(array);
            return array;
        }

        public static ushort B2US_BE(byte[] buffer, int start = 0)
        {
            var count = sizeof(ushort);
            var tmpArray = buffer.Skip(start).Take(count).ToArray();
            //如果是小端字节序，则交换
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tmpArray);
            return BitConverter.ToUInt16(tmpArray, 0);
        }

        public static byte[] US2B_BE(ushort number)
        {
            var array = BitConverter.GetBytes(number);
            //如果是小端字节序，则交换
            if (BitConverter.IsLittleEndian)
                Array.Reverse(array);
            return array;
        }
    }
}
