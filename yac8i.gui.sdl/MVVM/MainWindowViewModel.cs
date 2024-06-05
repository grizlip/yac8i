using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;

namespace yac8i.gui.sdl.MVVM
{
    public class MainWindowViewModel : ObservableObject, IDisposable
    {
        public ICommand LoadCommand { get; }
        public ICommand StartPauseCommand { get; }
        public ICommand RestartCommand { get; }
        public ICommand StepCommand { get; }

        public ObservableCollection<RegisterViewModel> Registers { get; set; } = new ObservableCollection<RegisterViewModel>();

        public ObservableCollection<InstructionViewModel> Instructions { get; set; } = new ObservableCollection<InstructionViewModel>();

        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetProperty(ref selectedIndex, value);
        }

        private readonly Model model;
        private readonly Window mainWindow;
        private bool started;
        private bool running;
        private bool loaded;
        private int selectedIndex;

        public MainWindowViewModel(Model model, Window mainWindow)
        {
            this.model = model;
            UpdateInstructions();
            this.mainWindow = mainWindow;
            this.model.ProgramLoaded += OnProgramLoaded;
            LoadCommand = new RelayCommand(LoadCommandExecute);
            StartPauseCommand = new RelayCommand(StartPauseCommandExecute, StartPauseCommandCanExecute);
            RestartCommand = new RelayCommand(RestartCommandExecute, RestartCommandCanExecute);
            StepCommand = new RelayCommand(StepCommandExecute, StepCommandCanExecute);
            for (int i = 0; i < 16; i++)
            {
                Registers.Add(new RegisterViewModel() { RegisterId = $"0x{i:X}", RegisterValue = "-" });
            }
            Registers.Add(new RegisterViewModel() { RegisterId = "I", RegisterValue = "-" });
            Registers.Add(new RegisterViewModel() { RegisterId = "PC", RegisterValue = "-" });
        }

        public void Dispose()
        {
            model.Dispose();
        }

        private void UpdateGUI()
        {
            model.UpdateRegisters();
            var regs = new List<byte>(model.Registers);
            int i = 0;
            for (; i < regs.Count; i++)
            {
                Registers[i].RegisterValue = $"0x{regs[i]:X2}";
            }
            Registers[i].RegisterValue = $"0x{model.IRegister:X4}";
            Registers[i + 1].RegisterValue = $"0x{model.ProgramCounter:X4}";

            foreach (var instruction in Instructions)
            {
                instruction.PointsToProgramCounter = false;
                if (instruction.Address == model.ProgramCounter)
                {
                    instruction.PointsToProgramCounter = true;
                    SelectedIndex = (instruction.Address - 512) / 2;
                }
            }
        }

        private bool StartPauseCommandCanExecute()
        {
            return loaded;
        }

        private bool RestartCommandCanExecute()
        {
            return started;
        }

        private void StartPauseCommandExecute()
        {
            if (!started)
            {
                Start();
            }
            else
            {
                if (running)
                {
                    model.Pause();
                    model.Tick -= OnTick;
                    running = false;
                    UpdateGUI();
                    (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                }
                else
                {
                    model.Go();
                    model.Tick += OnTick;
                    running = true;
                    (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        private void OnTick(object? sender, EventArgs arg)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateGUI();
            });
        }

        private bool StepCommandCanExecute()
        {
            return !running && loaded && started;
        }

        private void StepCommandExecute()
        {
            throw new NotImplementedException();
        }

        private void RestartCommandExecute()
        {
            model.Reset();
            Start();
        }

        private void Start()
        {
            model.Start();
            started = true;
            (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            model.Tick += OnTick;
            running = true;
        }

        private async void LoadCommandExecute()
        {
            var toplevel = TopLevel.GetTopLevel(mainWindow);
            if (toplevel != null)
            {
                var romFilePiker = new FilePickerOpenOptions
                {
                    Title = "Open ROM File",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>() {

                    new("Rom files")
                        {
                            Patterns = new List<string>() {"rom"}
                        },
                    new("Rom files")
                        {
                            Patterns = new List<string>() {"ch8"}
                        },
                    new("All files")
                        {
                            Patterns = new List<string>() {"*"}
                        }
                    }
                };
                var files = await toplevel.StorageProvider.OpenFilePickerAsync(romFilePiker);

                if (files.Count >= 1 && files[0].TryGetLocalPath() is string filePath)
                {
                    model.Load(filePath);
                    loaded = true;
                    running = false;
                    started = false;
                    (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        private void OnProgramLoaded(object? sender, EventArgs args)
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

                    Instructions.Add(new InstructionViewModel(opcode, address, model.GetMnemonic(opcode))); //TODO: make model to provide mnemonic from VM
                    address += 2;
                }
            });
        }
    }
}
