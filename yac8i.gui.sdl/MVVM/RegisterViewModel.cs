using CommunityToolkit.Mvvm.ComponentModel;

namespace yac8i.gui.sdl.MVVM
{
    public class RegisterViewModel : ObservableObject
    {
        public string RegisterId
        {
            get => registerId;
            set => SetProperty(ref registerId, value);
        }
        
        public string RegisterValue
        {
            get => registerValue;
            set => SetProperty(ref registerValue, value);
        }

        private string registerId;
        private string registerValue;

        public RegisterViewModel()
        {
            registerId = string.Empty;
            registerValue = string.Empty;
        }
    }
}