using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
        public ICommand RestartCommand { get; }
        public ICommand PauseGoCommand { get; }

        public ObservableCollection<RegisterViewModel> Registers { get; set; } = new ObservableCollection<RegisterViewModel>();

        public ObservableCollection<InstructionViewModel> Instructions { get; set; } = new ObservableCollection<InstructionViewModel>();
        private string selectedSoundDeviceName;
        private readonly Model model;
        private readonly Window mainWindow;
        private bool isRunning;
        public MainWindowViewModel(Model model, Window mainWindow)
        {
            this.model = model;
            UpdateInstructions();
            this.mainWindow = mainWindow;
            this.model.ProgramLoaded += OnProgramLoaded;
            LoadCommand = new RelayCommand(LoadCommandExecute, LoadCommandCanExecute);
            StartCommand = new RelayCommand(StartCommandExecute, StartCommandCanExecute);
            RestartCommand = new RelayCommand(RestartCommandExecute, RestartCommandCanExecute);
            PauseGoCommand = new RelayCommand(PauseGoCommandExecute, PauseGoCommandCanExecute);
        }

        public void OnProgramLoaded()
        {
            model.UpdateOpcodes();
            UpdateInstructions();
        }

        private bool LoadCommandCanExecute()
        {
            return true;
        }

        private bool StartCommandCanExecute()
        {
            return !isRunning;
        }

        private bool RestartCommandCanExecute()
        {
            return isRunning;
        }

        private bool PauseGoCommandCanExecute()
        {
            return true;
        }

        private void StartCommandExecute()
        {
            if (!isRunning)
            {
                model.Start();
                isRunning = true;
                (StartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        private void RestartCommandExecute()
        {
            model.Reset();
            model.Start();
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
                model.Load(result[0]);
            }
        }

        private void PauseGoCommandExecute()
        {
            if (isRunning)
            {
                model.Pause();
                isRunning = false;
                model.UpdateRegisters();
                Registers.Clear();
                var regs = new List<byte>(model.Registers);
                for (int i = 0; i < regs.Count; i++)
                {
                    Registers.Add(new RegisterViewModel() { RegisterId = $"0x{i:X}", RegisterValue = $"0x{regs[i]:X2}" });
                }
                Registers.Add(new RegisterViewModel() { RegisterId = "I", RegisterValue = $"0x{model.IRegister:X4}" });
                Registers.Add(new RegisterViewModel() { RegisterId = "PC", RegisterValue = $"0x{model.ProgramCounter:X4}" });
                (StartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                var currentInstruction = Instructions.Where(item => item.Address == model.ProgramCounter).SingleOrDefault();
                if (currentInstruction != null)
                {
                    currentInstruction.PointsToProgramCounter = true;
                }
            }
            else
            {
                model.Go();
                isRunning = true;
                (StartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }

        }

        private void OnProgramLoaded(object sender, EventArgs args)
        {
            UpdateInstructions();
        }

        private void UpdateInstructions()
        {
            Dispatcher.UIThread.Post(() =>
            {
                ushort address = 512;
                Instructions.Clear();
                foreach (var opcode in model.Opcodes)
                {
                    Instructions.Add(new InstructionViewModel(opcode, address));
                    address += 2;
                }
            });
        }
    }
}
