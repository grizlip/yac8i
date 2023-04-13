using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace yac8i.gui.sdl.MVVM
{
    public class MainWindowViewModel : ObservableObject
    {
        public ICommand LoadCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand PauseGoCommand { get; }

        public ObservableCollection<string> AudioDevices { get; set; } = new ObservableCollection<string>();

        public string SelectedSoundDeviceName
        {
            get { return selectedSoundDeviceName; }
            set
            {
                if (SetProperty(ref selectedSoundDeviceName, value))
                {
                    this.sdlFront.ChooseAudioDevice(selectedSoundDeviceName);
                }
            }
        }

        public ObservableCollection<InstructionViewModel> Instructions { get; set; } = new ObservableCollection<InstructionViewModel>();
        private string selectedSoundDeviceName;
        private readonly SDLFront sdlFront;
        private readonly Model model;
        private readonly Window mainWindow;
        public MainWindowViewModel(SDLFront sdlFront, Model model, Window mainWindow)
        {
            this.sdlFront = sdlFront;
            this.model = model;
            UpdateInstructions();
            this.mainWindow = mainWindow;
            this.model.ProgramLoaded += OnProgramLoaded;
            LoadCommand = new RelayCommand(LoadCommandExecute, LoadCommandCanExecute);
            StartCommand = new RelayCommand(StartCommandExecute, StartCommandCanExecute);
            StopCommand = new RelayCommand(StopCommandExecute, StopCommandCanExecute);
            PauseGoCommand = new RelayCommand(PauseGoCommandExecute, PauseGoCommandCanExecute);
        }

        public void OnProgramLoaded()
        {
            model.UpdateOpcodes();
            UpdateInstructions();
        }

        public void UpdateAudioDevices()
        {
            AudioDevices.Clear();
            foreach (var s in sdlFront.GetAudioDevices())
            {
                AudioDevices.Add(s);
            }
        }

        private bool LoadCommandCanExecute()
        {
            return true;
        }

        private bool StartCommandCanExecute()
        {
            return true;
        }

        private bool StopCommandCanExecute()
        {
            return true;
        }

        private bool PauseGoCommandCanExecute()
        {
            return true;
        }

        private void StartCommandExecute()
        {
            //TODO: implement load dialog
        }

        private void StopCommandExecute()
        {
            //TODO: implement load dialog
        }

        private async void LoadCommandExecute()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filters.Add(new FileDialogFilter() { Name = "Rom files", Extensions = { "rom" } });
            openFileDialog.Filters.Add(new FileDialogFilter() { Name = "Rom files", Extensions = { "ch8" } });
            openFileDialog.Filters.Add(new FileDialogFilter() { Name = "All files", Extensions = { "*" } });
            openFileDialog.AllowMultiple = false;
            var result = await openFileDialog.ShowAsync(mainWindow);
            if (result?.Length == 1)
            {
               model.LoadAndExecute(result[0]);
            }
        }

        private void PauseGoCommandExecute()
        {
            //TODO: implement load dialog
        }

        private void OnProgramLoaded(object sender, EventArgs args)
        {
            UpdateInstructions();
        }

        private void UpdateInstructions()
        {
            Dispatcher.UIThread.Post(() =>
            {
                Instructions.Clear();
                foreach (var opcode in model.Opcodes)
                {
                    Instructions.Add(new InstructionViewModel(opcode));
                }
            });
        }
    }
}
