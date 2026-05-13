# MemoryScanner

A simple C# process memory scanner inspired by how tools like Cheat Engine work internally.

This project demonstrates:
- Opening another process
- Reading raw memory (RAM)
- Enumerating memory regions
- Scanning for integer values
- Understanding low-level Windows memory APIs

Built for learning/research purposes.

---

# Features

- Scan another process's memory
- Search for specific integer values
- Enumerate readable memory pages
- Print addresses where values are found
- Heavy inline comments explaining everything

---

# How It Works

The program:

1. Finds a running process (currently `notepad.exe`)
2. Opens the process using Windows APIs
3. Iterates through memory regions
4. Reads raw bytes from RAM
5. Converts bytes into integers
6. Compares values against a target number
7. Prints matching addresses

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    /*
        ============================
        WINDOWS API IMPORTS
        ============================

        C# itself cannot directly read another program's memory.

        So we import Windows functions from kernel32.dll.

        This is called "P/Invoke" (Platform Invoke).

        It lets managed C# code call native Windows code.
    */


    /*
        OpenProcess()

        This asks Windows:
        "Hey, can I get access to another running process?"

        Example:
        - notepad.exe
        - game.exe
        - chrome.exe

        Returns:
        A HANDLE (basically an ID/token representing access to the process)

        Think of it like:
        "Permission slip to inspect this process"
    */
    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(
        uint dwDesiredAccess,   // what permissions we want
        bool bInheritHandle,    // child processes inherit handle?
        int dwProcessId);       // PID of target process


    /*
        ReadProcessMemory()

        !!!THIS is important!!!

        It literally copies bytes from another process's RAM
        into OUR program.

        Example:
        Read memory from:
        0x7FFA12345678

        and copy it into:
        byte[] buffer
    */

    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(
        IntPtr hProcess,            // process handle from OpenProcess
        IntPtr lpBaseAddress,       // memory address to read
        byte[] lpBuffer,            // where bytes will be stored
        int dwSize,                 // how many bytes to read
        out int lpNumberOfBytesRead // actual amount read
    );


    /*
        VirtualQueryEx()

        This asks Windows:
        "Tell me information about a memory region."

        Processes have MANY memory regions.

        Example:
        - executable code
        - stack
        - heap
        - DLL memory
        - textures
        - strings
        etc.

        We use this to iterate through memory safely.
    */

    [DllImport("kernel32.dll")]
    static extern int VirtualQueryEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer,
        uint dwLength);


    /*
        ============================
        ACCESS FLAGS
        ============================

        Hexadecimal constants used by Windows APIs.
    */


    /*
        PROCESS_VM_READ

        Means:
        "I want permission to READ this process memory."
    */
    const uint PROCESS_VM_READ = 0x0010;


    /*
        PROCESS_QUERY_INFORMATION

        Means:
        "I want permission to ask information about memory regions."
    */
    const uint PROCESS_QUERY_INFORMATION = 0x0400;


    /*
        MEM_COMMIT

        Means:
        "This memory region actually exists physically/virtually."

        Some memory regions are reserved but unused.
    */
    const uint MEM_COMMIT = 0x1000;


    /*
        PAGE_NOACCESS

        Means:
        "Windows forbids reading this memory page."

        Trying to read these often crashes/fails.
    */
    const uint PAGE_NOACCESS = 0x01;


    /*
        ============================
        MEMORY INFORMATION STRUCT
        ============================

        Windows fills this structure with info about memory regions.
    */

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        /*
            BaseAddress

            Starting address of this memory region.

            Example:
            0x7FFA14000000
        */
        public IntPtr BaseAddress;


        /*
            AllocationBase

            Original allocation address.
        */
        public IntPtr AllocationBase;


        /*
            Allocation protection flags.
        */
        public uint AllocationProtect;


        /*
            RegionSize

            Size of this memory block in bytes.

            Example:
            4096
            65536
            etc.
        */
        public IntPtr RegionSize;


        /*
            State

            Example:
            MEM_COMMIT
        */
        public uint State;


        /*
            Protection flags.

            Example:
            PAGE_READWRITE
            PAGE_EXECUTE
            etc.
        */
        public uint Protect;


        /*
            Type of memory region.
        */
        public uint Type;
    }


    static void Main()
    {
        /*
            ============================
            TARGET VALUE
            ============================

            This is what we are searching for in RAM.

            Example:
            health = 1000
            ammo = 30
            coins = 9999

            We scan memory searching for this integer.
        */
        int targetValue = 1000;


        /*
            ============================
            FIND PROCESS
            ============================

            Get all running processes named "notepad".

            Returns:
            Process[]
        */
        Process[] processes =
            Process.GetProcessesByName("notepad");


        /*
            If no process found:
            stop program.
        */
        if (processes.Length == 0)
        {
            Console.WriteLine("Notepad not found.");
            return;
        }


        /*
            Take first notepad process.

            [0] means first item in array.
        */
        Process process = processes[0];


        /*
            Print PID (Process ID)

            Example:
            14324
        */
        Console.WriteLine(
            $"Scanning PID: {process.Id}");


        /*
            ============================
            OPEN PROCESS
            ============================

            Ask Windows for permission to inspect process memory.
        */
        IntPtr hProcess = OpenProcess(
            PROCESS_VM_READ | PROCESS_QUERY_INFORMATION,
            false,
            process.Id);


        /*
            Start scanning from memory address 0.
        */
        long address = 0;


        /*
            Counter:
            how many matches found?
        */
        int found = 0;


        /*
            ============================
            MAIN MEMORY SCAN LOOP
            ============================

            Loop through ALL memory regions.
        */
        while (true)
        {
            /*
                This struct will receive info
                about current memory region.
            */
            MEMORY_BASIC_INFORMATION memInfo;


            /*
                Query current memory region.
            */
            int result = VirtualQueryEx(
                hProcess,
                (IntPtr)address,
                out memInfo,
                (uint)Marshal.SizeOf(
                    typeof(MEMORY_BASIC_INFORMATION)));


            /*
                If result == 0:
                no more memory regions.
            */
            if (result == 0)
                break;


            /*
                ============================
                IS MEMORY READABLE?
                ============================

                We only want:
                - committed memory
                - accessible memory
            */
            bool readable =
                memInfo.State == MEM_COMMIT &&
                memInfo.Protect != PAGE_NOACCESS;


            /*
                If region is readable:
            */
            if (readable)
            {
                /*
                    Get region size.
                */
                long regionSize =
                    memInfo.RegionSize.ToInt64();


                /*
                    Avoid absurd allocations.

                    Some regions can be gigantic.

                    We skip anything > 10 MB.
                */
                if (regionSize > 0 &&
                    regionSize < 10 * 1024 * 1024)
                {
                    /*
                        Create byte buffer.

                        This will temporarily hold
                        copied RAM bytes.
                    */
                    byte[] buffer =
                        new byte[regionSize];


                    /*
                        ============================
                        READ MEMORY
                        ============================

                        Copy bytes from target process
                        into our buffer.
                    */
                    bool success = ReadProcessMemory(
                        hProcess,
                        memInfo.BaseAddress,
                        buffer,
                        buffer.Length,
                        out int bytesRead);


                    /*
                        If memory read succeeded:
                    */
                    if (success)
                    {
                        /*
                            Loop through EVERY BYTE
                            in this memory region.
                        */
                        for (int i = 0;
                             i < bytesRead - 4;
                             i++)
                        {
                            /*
                                Read 4 bytes starting at i
                                and interpret them as INT32.

                                Example:
                                01 00 00 00 -> 1
                                E8 03 00 00 -> 1000
                            */
                            int value =
                                BitConverter.ToInt32(
                                    buffer,
                                    i);


                            /*
                                Compare value with target.
                            */
                            if (value == targetValue)
                            {
                                /*
                                    Calculate REAL address.

                                    Example:
                                    BaseAddress + offset
                                */
                                long foundAddress =
                                    memInfo.BaseAddress.ToInt64()
                                    + i;


                                /*
                                    Print result.
                                */
                                Console.WriteLine(
                                    "Found: "
                                    + targetValue
                                    + " at 0x"
                                    + foundAddress.ToString("X"));


                                /*
                                    Increase match counter.
                                */
                                found++;
                            }
                        }
                    }
                }
            }


            /*
                ============================
                MOVE TO NEXT MEMORY REGION
                ============================

                Example:

                Current region:
                starts at 0x1000
                size = 0x2000

                Next region:
                0x3000
            */
            address =memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64();
        }


        /*
            Print final result count.
        */
        Console.WriteLine();
        Console.WriteLine(
            $"Done. Found {found} matches.");


        /*
            Prevent console from instantly closing.
        */
        Console.ReadKey();
    }
}
```

Example output:

```text
Scanning PID: 14324

Found: 1000 at 0x7FFA14026794
Found: 1000 at 0x7FFA140267D0

Done. Found 12 matches.
```
