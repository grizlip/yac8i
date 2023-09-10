using CommunityToolkit.Mvvm.ComponentModel;

namespace yac8i.gui.sdl.MVVM
{
    public class InstructionViewModel : ObservableObject
    {
        public bool PointsToProgramCounter
        {
            get => pointsToProgramCounter;
            set => SetProperty(ref pointsToProgramCounter, value);
        }

        public ushort Opcode => opcode;

        public ushort Address => address;

        public string Mnemonic => mnemonic;

        private readonly ushort opcode;
        private readonly ushort address;
        private readonly string mnemonic;
        private bool pointsToProgramCounter;

        public InstructionViewModel(ushort opcode, ushort address, string mnemonic)
        {
            this.opcode = opcode;
            this.address = address;
            this.mnemonic = mnemonic;
        }
    }
}