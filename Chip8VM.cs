using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace yac8i
{
    public class Chip8VM : IDisposable
    {
        private byte[] memory = new byte[4096];
        private byte[] registers = new byte[16];
        private byte[] iRegister = new byte[2];
        private ushort programCounter = 0x200;
        private Stack<ushort> stack = new Stack<ushort>();
        //TODO: registers for sound and timer
        public EventHandler<ScreenRefreshEventArgs> ScreenRefresh;
        private List<Instruction> instructions;
        private bool loaded = false;
        private FileStream programSourceStreamReader;

        public event EventHandler<string> NewMessage;
        public Chip8VM()
        {
            instructions = new List<Instruction>()
        {
            // TODO: This instruction colides with 0x00EF and 0x00E0
            //       It happens because all three instructions beings with 0
            //       and in case of 0x0000 we are checking only top most four bits
            //       if they are 0, then we assume we have a match
            //       Find a way to implement this better. 
            //new Instruction() { Opcode=0x0000,Mask=0xF000},
            new Instruction() { Opcode=0x00E0,Mask=0xFFFF, Execute = args =>
            {
                this.ScreenRefresh?.Invoke(this,new ScreenRefreshEventArgs(RefreshRequest.Clear));
                return true;
            }},
            new Instruction() { Opcode=0x00EE,Mask=0xFFFF,Execute = args =>
            {
                ushort returnAddress;
                if(this.stack.TryPop(out returnAddress))
                {
                    OnNewMessage(string.Format("Pop 0x{0:X4}",returnAddress));
                    this.programCounter = returnAddress;
                }
                else
                {
                    OnNewMessage("Error. Stack empty while trying to return from subroutine");
                }
                return false;
            }},
            new Instruction() { Opcode=0x1000,Mask=0xF000, Execute = args =>
            {
                    this.programCounter = args;
                    return false;
            }},
            new Instruction() { Opcode=0x2000,Mask=0xF000, Execute = args =>
            {
                OnNewMessage(string.Format("Push 0x{0:X4}",programCounter+2));
                this.stack.Push((ushort)(programCounter+2));
                this.programCounter = args;

                return false;
            }},
            new Instruction() { Opcode=0x3000,Mask=0xF000},
            new Instruction() { Opcode=0x4000,Mask=0xF000},
            new Instruction() { Opcode=0x5000,Mask=0xF000},
            new Instruction() { Opcode=0x6000,Mask=0xF000},
            new Instruction() { Opcode=0x7000,Mask=0xF000},
            new Instruction() { Opcode=0x8000,Mask=0xF00F},
            new Instruction() { Opcode=0x8001,Mask=0xF00F},
            new Instruction() { Opcode=0x8002,Mask=0xF00F},
            new Instruction() { Opcode=0x8003,Mask=0xF00F},
            new Instruction() { Opcode=0x8004,Mask=0xF00F},
            new Instruction() { Opcode=0x8005,Mask=0xF00F},
            new Instruction() { Opcode=0x8006,Mask=0xF00F},
            new Instruction() { Opcode=0x8007,Mask=0xF00F},
            new Instruction() { Opcode=0x800E,Mask=0xF00F},
            new Instruction() { Opcode=0x9000,Mask=0xF00F},
            new Instruction() { Opcode=0xA000,Mask=0xF000},
            new Instruction() { Opcode=0xB000,Mask=0xF000},
            new Instruction() { Opcode=0xC000,Mask=0xF000},
            new Instruction() { Opcode=0xD000,Mask=0xF000, Execute = args =>
            {
                this.ScreenRefresh?.Invoke(this,new ScreenRefreshEventArgs(RefreshRequest.Draw));
                return true;
            }},
            new Instruction() { Opcode=0xE09E,Mask=0xF0FF},
            new Instruction() { Opcode=0xE0A1,Mask=0xF0FF},
            new Instruction() { Opcode=0xF007,Mask=0xF0FF},
            new Instruction() { Opcode=0xF00A,Mask=0xF0FF},
            new Instruction() { Opcode=0xF015,Mask=0xF0FF},
            new Instruction() { Opcode=0xF018,Mask=0xF0FF},
            new Instruction() { Opcode=0xF01E,Mask=0xF0FF},
            new Instruction() { Opcode=0xF029,Mask=0xF0FF},
            new Instruction() { Opcode=0xF033,Mask=0xF0FF},
            new Instruction() { Opcode=0xF055,Mask=0xF0FF},
            new Instruction() { Opcode=0xF065,Mask=0xF0FF},
        };

            Reset();
        }
        public bool Load(string programSourceFilePath)
        {
            try
            {
                programSourceStreamReader = new FileStream(programSourceFilePath, FileMode.Open);
                programSourceStreamReader.Read(memory, 512, memory.Length - 512);
                loaded = true;
            }
            catch (Exception ex)
            {
                OnNewMessage($"File read error: {ex.Message}");
            }
            return loaded;
        }

        public void Start()
        {
            if (loaded)
            {

                while (programCounter < memory.Length)
                {
                    byte[] instructionRaw = new byte[] { memory[programCounter], memory[programCounter + 1] };

                    ushort instructionValue = (ushort)(instructionRaw[0] << 8 | instructionRaw[1]);

                    var instruction = instructions.SingleOrDefault(item => (instructionValue & item.Mask) == item.Opcode);
                    bool increaseProgramCounter = false;
                    if (instruction != null)
                    {
                        ushort argsMask = (ushort)(instruction.Mask ^ 0xFFFF);
                        ushort args = (ushort)(instructionValue & argsMask);

                        if (instruction.Execute != null)
                        {
                            increaseProgramCounter = instruction.Execute(args);
                        }
                        OnNewMessage(string.Format("Instruction: 0x{0:X4}, Args: 0x{1:X4}", instructionValue, args));
                        if (increaseProgramCounter)
                        {
                            programCounter += 2;
                        }
                        OnNewMessage(string.Format("Program counter 0x{0:X4}", programCounter));
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        public void Reset()
        {
            programCounter = 0x200;
            stack.Clear();
            Array.Clear(memory, 0, memory.Length);
            Array.Clear(registers, 0, registers.Length);
            Array.Clear(iRegister, 0, iRegister.Length);
        }
        #region IDisposable
        // To detect redundant calls
        private bool _disposedValue;

        public void Dispose() => Dispose(true);

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    programSourceStreamReader?.Dispose();
                }

                _disposedValue = true;
            }
        }
        #endregion

        private void OnNewMessage(string msg)
        {
            NewMessage?.Invoke(this, msg);
        }
    }
}
