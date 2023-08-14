using System;
namespace yac8i;

internal class Instruction
{
    public ushort Mask { get; set; }
    public ushort Opcode { get; set; }
    public Func<ushort, bool> Execute { get; set; } = args => true;

    public static byte X(ushort value)
    {
        return (byte)((value & 0x0F00)>>8);
    }

    public static byte Y(ushort value)
    {
        return (byte)((value  & 0x00F0)>>4);
    }
    
    public static byte N(ushort value)
    {
        return (byte)(value  & 0x000F);
    }

    public static byte NN(ushort value)
    {
        return (byte)(value  & 0x00FF);
    }
    public static ushort NNN(ushort value)
    {
        return (ushort)(value  & 0x0FFF);
    }
}

