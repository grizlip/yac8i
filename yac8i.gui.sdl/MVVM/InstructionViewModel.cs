using CommunityToolkit.Mvvm.ComponentModel;

namespace yac8i.gui.sdl.MVVM
{

    public class InstructionViewModel : ObservableObject
    {
        public bool PointsToProgramCounter
        {
            get
            {
                return pointsToProgramCounter;
            }
            set
            {
                SetProperty(ref pointsToProgramCounter, value);
            }
        }

        public ushort Opcode
        {
            get
            {
                return opcode;
            }
        }

        public ushort Address
        {
            get
            {
                return address;
            }
        }

        private readonly ushort opcode;
        private readonly ushort address;
        private bool pointsToProgramCounter;

        public InstructionViewModel(ushort opcode, ushort address)
        {
            this.opcode = opcode;
            this.address = address;
        }
    }
}