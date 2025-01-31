using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using SDL2;
using System.Linq;

namespace yac8i.gui.sdl.MVVM
{
    public class MainWindowViewModel : ObservableObject, IDisposable
    {
        public ICommand LoadCommand { get; }
        public ICommand StartPauseCommand { get; }
        public ICommand RestartCommand { get; }
        public ICommand StepCommand { get; }
        public ICommand StoreCommand { get; }
        public ICommand RestoreCommand { get; }

        public ObservableCollection<RegisterViewModel> Registers { get; } = [];

        public ObservableCollection<InstructionViewModel> Instructions { get; } = [];

        public ObservableCollection<BreakpointViewModel> Breakpoints { get; } = [];

        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetProperty(ref selectedIndex, value);
        }

        private readonly Chip8VM vm;
        private readonly Window mainWindow;
        private bool started;
        private bool running;
        private bool loaded;
        private readonly List<ushort> opcodes = [];
        private CancellationTokenSource cancellationTokenSource = new();
        private Task? vmTask = null;
        private readonly SDLFront sdlFront;
        private string lastRomFile = string.Empty;
        private int selectedIndex;


        public MainWindowViewModel(Chip8VM vm, Window mainWindow)
        {
            this.vm = vm;
            sdlFront = new SDLFront(vm);

            //TODO: long running
            Task.Run(async () =>
            {
                //this delay is here to get a correct HostPointer for the MainWindow
                //TODO: find a better solution than this!
                await Task.Delay(500);
                sdlFront.InitializeAndStart((mainWindow as MainWindow)?.HostPointer ?? IntPtr.Zero);
            });

            UpdateInstructions();
            this.mainWindow = mainWindow;
            this.vm.ProgramLoaded += OnProgramLoaded;
            LoadCommand = new RelayCommand(LoadCommandExecute);
            StartPauseCommand = new RelayCommand(StartPauseCommandExecute, StartPauseCommandCanExecute);
            RestartCommand = new RelayCommand(RestartCommandExecute, RestartCommandCanExecute);
            StepCommand = new RelayCommand(StepCommandExecute, StepCommandCanExecute);
            for (int i = 0; i < 16; i++)
            {
                Registers.Add(new RegisterViewModel() { RegisterId = $"0x{i:X}", RegisterValue = "-" });
            }
            StoreCommand = new RelayCommand(StoreCommandExecute, StoreCanExecute);
            RestoreCommand = new RelayCommand(RestoreCommandExecute);
            Registers.Add(new RegisterViewModel() { RegisterId = "I", RegisterValue = "-" });
            Registers.Add(new RegisterViewModel() { RegisterId = "PC", RegisterValue = "-" });
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            vm.Tick -= OnTick;
            if (!vmTask?.IsCompleted ?? false)
            {
                vmTask?.Wait();
            }
            cancellationTokenSource.Dispose();
            vmTask?.Dispose();
            sdlFront.Stop();
        }

        public void RemoveBreakpoint(BreakpointViewModel breakpointViewModel)
        {
            Breakpoints.Remove(breakpointViewModel);
            if (vm.TryRemoveBreakpoint(breakpointViewModel.Address, out var removed))
            {
                removed.BreakpointHit -= OnBreakpointHit;
            }
        }

        public void AddNewBreakpoint(ushort address)
        {
            if (vm.TryAddBreakpoint(address, out var breakpointInfo))
            {
                breakpointInfo.BreakpointHit += OnBreakpointHit;
                Breakpoints.Add(new BreakpointViewModel(breakpointInfo, address));
            }
        }

        public void OnKeyDown(KeyEventArgs e)
        {
            var key = e.Key switch
            {
                Key.D1 => SDL.SDL_Keycode.SDLK_1,
                Key.D2 => SDL.SDL_Keycode.SDLK_2,
                Key.D3 => SDL.SDL_Keycode.SDLK_3,
                Key.D4 => SDL.SDL_Keycode.SDLK_4,
                _ => (SDL.SDL_Keycode)(e.Key + 53),
            };
            if (SDLDraw.SupportedKeys.Contains(key))
            {
                sdlFront.OnKeyDown(key);
            }

        }

        public void OnKeyUp(KeyEventArgs e)
        {
            var key = e.Key switch
            {
                Key.D1 => SDL.SDL_Keycode.SDLK_1,
                Key.D2 => SDL.SDL_Keycode.SDLK_2,
                Key.D3 => SDL.SDL_Keycode.SDLK_3,
                Key.D4 => SDL.SDL_Keycode.SDLK_4,
                _ => (SDL.SDL_Keycode)(e.Key + 53),
            };
            if (SDLDraw.SupportedKeys.Contains(key))
            {
                sdlFront.OnKeyUp(key);
            }

        }

        private void Load(string file)
        {
            lastRomFile = file;
            cancellationTokenSource.Cancel();

            if ((!vmTask?.IsCompleted) ?? false)
            {
                vmTask?.Wait();
            }

            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            vm.StopAndReset();
            vm.Load(file);
        }

        private void UpdateGUI()
        {
            var regs = new List<byte>(vm.Registers);
            int i = 0;
            for (; i < regs.Count; i++)
            {
                Registers[i].RegisterValue = $"0x{regs[i]:X2}";
            }
            Registers[i].RegisterValue = $"0x{vm.IRegister:X4}";
            Registers[i + 1].RegisterValue = $"0x{vm.ProgramCounter:X4}";

            foreach (var instruction in Instructions)
            {
                instruction.PointsToProgramCounter = false;
                if (instruction.Address == vm.ProgramCounter)
                {
                    instruction.PointsToProgramCounter = true;
                    SelectedIndex = (instruction.Address - 512) / 2;
                }
            }
        }


        private bool StoreCanExecute()
        {
            return started;
        }

        private bool StartPauseCommandCanExecute()
        {
            return loaded;
        }

        private bool RestartCommandCanExecute()
        {
            return started;
        }

        private void StoreCommandExecute()
        {
            vm.TryStore("state.xml");
        }

        private void RestoreCommandExecute()
        {
            if(vm.TryRestore("state.xml"))
            {
                loaded = true;
                (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
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
                    vm.Pause();
                    vm.Tick -= OnTick;
                    running = false;
                    UpdateGUI();
                    (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                }
                else
                {
                    vm.Go();
                    vm.Tick += OnTick;
                    running = true;
                    (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        private void OnTick(object? sender, EventArgs arg)
        {
            Dispatcher.UIThread.Post(UpdateGUI);
        }

        private bool StepCommandCanExecute()
        {
            return !running && loaded && started;
        }

        private void StepCommandExecute()
        {
            vm.Step();
            UpdateGUI();
        }

        private void RestartCommandExecute()
        {
            Load(lastRomFile);
            Start();
            (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        }

        private void Start()
        {
            if (!vmTask?.IsCompleted ?? false)
            {
                return;
            }
            var token = cancellationTokenSource.Token;
            vmTask = vm.StartAsync(token);

            started = true;
            (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            (StoreCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            vm.Tick += OnTick;
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
                            Patterns = new List<string>() {"*.rom"}
                        },
                    new("ch8 files")
                        {
                            Patterns = new List<string>() {"*.ch8"}
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
                    this.Load(filePath);
                    loaded = true;
                    running = false;
                    started = false;
                    (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                    (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        private void OnProgramLoaded(object? sender, int bytesCount)
        {
            UpdateOpcodes(bytesCount);
            UpdateInstructions();
        }

        private void UpdateOpcodes(int bytesCount = 0)
        {
            opcodes.Clear();
            int bytesCountAdjusted = bytesCount + 512;
            for (uint i = 512; i < bytesCountAdjusted; i += 2)
            {
                opcodes.Add(vm.GetOpcode(i));
            }
        }

        private void UpdateInstructions()
        {
            Dispatcher.UIThread.Post(() =>
            {
                ushort address = 512;
                Instructions.Clear();
                foreach (var opcode in opcodes)
                {
                    Instructions.Add(new InstructionViewModel(opcode, address, vm.GetMnemonic(opcode))); //TODO: make model to provide mnemonic from VM
                    address += 2;
                }
            });
        }

        private void OnBreakpointHit(object? sender, EventArgs args)
        {
            Dispatcher.UIThread.Post(() =>
            {
                running = false;
                UpdateGUI();
                (StartPauseCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (RestartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (StepCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            });
        }
    }
}
