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

        private readonly ushort opcode;
        private bool pointsToProgramCounter;

        public InstructionViewModel(ushort opcode)
        {
            this.opcode = opcode;
        }
    }
}