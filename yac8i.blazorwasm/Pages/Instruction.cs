namespace yac8i.blazorwasm.Pages
{
    public class Instruction(ushort address, string mnemonic)
    {
        public ushort Address { get; } = address;
        public string Mnemonic { get; } = mnemonic;
        public bool Current { get; set; } = false;
    }
}
