using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(
        uint dwDesiredAccess,   // what permissions we want
        bool bInheritHandle,    // child processes inherit handle?
        int dwProcessId);       // PID of target process
  
    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(
        IntPtr hProcess,            // process handle from OpenProcess
        IntPtr lpBaseAddress,       // memory address to read
        byte[] lpBuffer,            // where bytes will be stored
        int dwSize,                 // how many bytes to read
        out int lpNumberOfBytesRead // actual amount read
    );

    [DllImport("kernel32.dll")]
    static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    const uint PROCESS_VM_READ = 0x0010;
    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint MEM_COMMIT = 0x1000;
    const uint PAGE_NOACCESS = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {

        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }


    static void Main()
    {

        int targetValue = 1000;
        Process[] processes = Process.GetProcessesByName("notepad");

        if (processes.Length == 0)
        {
            Console.WriteLine("Notepad not found.");
            return;
        }

        Process process = processes[0];
        Console.WriteLine($"Scanning PID: {process.Id}");
        IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION,false,process.Id);
        long address = 0;
        int found = 0;

        while (true)
        {

            MEMORY_BASIC_INFORMATION memInfo;
            int result = VirtualQueryEx(hProcess, (IntPtr)address, out memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

            if (result == 0)
                break;

            bool readable = memInfo.State == MEM_COMMIT && memInfo.Protect != PAGE_NOACCESS;

            if (readable)
            {
              
                long regionSize = memInfo.RegionSize.ToInt64();

                if (regionSize > 0 &&
                    regionSize < 10 * 1024 * 1024)
                {

                    byte[] buffer =
                        new byte[regionSize];

                    bool success = ReadProcessMemory(hProcess,memInfo.BaseAddress,buffer,buffer.Length,out int bytesRead);

                    if (success)
                    {

                        for (int i = 0;
                             i < bytesRead - 4;
                             i++)
                        {

                            int value =
                                BitConverter.ToInt32(
                                    buffer,
                                    i);

                            if (value == targetValue)
                            {
                                long foundAddress =memInfo.BaseAddress.ToInt64()+ i;
                                Console.WriteLine("Found: " + targetValue + " at 0x" + foundAddress.ToString("X"));
                                found++;
                            }
                        }
                    }
                }
            }

            address =memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64();
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Found {found} matches.");
        Console.ReadKey();
    }
}
