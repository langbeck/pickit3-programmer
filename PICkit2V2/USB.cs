using System;
using System.Collections.Generic;
using HidSharp;

namespace PICkit2V2
{
    public class USB
    {
        private static IDictionary<IntPtr, DeviceStream> hDict = new Dictionary<IntPtr, DeviceStream>();
        private static int lastHandler = 0;

        public static string UnitID = "";

        private static IntPtr OpenDevice(Device device)
        {
            var stream = device.Open();
            var handler = (IntPtr)(++lastHandler);
            hDict[handler] = stream;
            return handler;
        }

        private static DeviceStream GetDeviceStream(IntPtr handler)
        {
            return hDict[handler];
        }

        public static bool Find_This_Device(ushort p_VendorID,
                                           ushort p_PoductID,
                                           ushort p_index,
                                           ref IntPtr p_ReadHandle,
                                           ref IntPtr p_WriteHandle)
        {
            var devices = DeviceList.Local.GetHidDevices();
            ushort l_num_found_devices = 0;

            foreach (var device in devices)
            {
                // Skip non-matching devices
                if (device.VendorID != p_VendorID || device.ProductID != p_PoductID)
                    continue;

                if (l_num_found_devices++ != p_index)
                    continue;

                UnitID = device.GetSerialNumber();
                var handler = OpenDevice(device);
                p_WriteHandle = handler;
                p_ReadHandle = handler;
                return true;
            }

            return false;
        }


        public static unsafe bool WriteFile(
            IntPtr hFile,                       // handle to file
            byte[] Buffer,                      // data buffer
            int numBytesToWrite,                // num of bytes to write
            ref int numBytesWritten,            // number of bytes actually written
            int Overlapped                      // overlapped buffer - not used
            )
        {
            var stream = GetDeviceStream(hFile);
            stream.Write(Buffer, 0, numBytesToWrite);
            numBytesWritten = numBytesToWrite;
            return true;
        }

        public static unsafe bool ReadFile(
              IntPtr hFile,                       // handle to file
              byte[] Buffer,                      // data buffer
              int NumberOfBytesToRead,            // number of bytes to read
              ref int pNumberOfBytesRead,         // number of bytes read
              int Overlapped                      // overlapped buffer - not used
              )
        {
            var stream = GetDeviceStream(hFile);
            stream.Read(Buffer, 0, NumberOfBytesToRead);
            pNumberOfBytesRead = NumberOfBytesToRead;
            return true;
        }

        public static Int32 CloseHandle(IntPtr hObject)
        {
            hDict.Remove(hObject);
            return 0;
        }
    }
}
