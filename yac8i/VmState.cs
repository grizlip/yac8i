using System;
using System.Collections.Generic;

namespace yac8i
{
    internal class VmState
    {
        internal bool[,] Surface { get; set; } = new bool[64, 32];
        internal Stack<ushort> Stack { get; set; } = new();
        internal ushort ProgramCounter { get; set; } = 0x200;
        internal byte[] Registers { get; set; } = new byte[16];
        internal ushort IRegister { get; set; } = 0;
        internal byte[] Memory { get; set; } = new byte[4096];
        internal byte DelayTimer { get; set; } = 0;
        internal byte SoundTimer { get; set; } = 0;
        internal ushort PressedKeys { get; set; } = 0;
        internal ushort? LastPressedKey { get; set; } = null;

        internal void Clear()
        {
            ProgramCounter = 0x200;
            IRegister = 0;
            SoundTimer = 0;
            DelayTimer = 0;
            Stack.Clear();
            Array.Clear(Surface);
            Array.Clear(Memory);
            Array.Clear(Registers);
        }
    }
}