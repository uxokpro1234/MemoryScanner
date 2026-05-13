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

Example output:

```text
Scanning PID: 14324

Found: 1000 at 0x7FFA14026794
Found: 1000 at 0x7FFA140267D0

Done. Found 12 matches.
```
