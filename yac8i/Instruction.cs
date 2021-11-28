using System;
namespace yac8i;

public class Instruction
{
    public ushort Mask { get; set; }
    public ushort Opcode { get; set; }
    public Func<ushort, bool> Execute { get; set; } = args => true;
}

