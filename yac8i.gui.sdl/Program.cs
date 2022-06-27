//based on https://jsayers.dev/category/c-sdl-tutorial-series/
using System.Runtime.InteropServices;
using System;
using SDL2;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using yac8i;

namespace yac8i.gui.sdl
{
    public class Program
    {
        private const int PIXEL_SIZE = 16;
        private static bool[,] vmSurface = new bool[64, 32];
        private static object vmSurfaceLock = new object();
        private static Chip8VM vm = new Chip8VM();
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static Task? vmTask = null;
        private static IntPtr windowPtr;
        private static double samplingIndex = 0;
        private const double AMPLITUDE = 28000d;
        private const double SOUND_FREQUENCY = 261.63d;
        private static SDL.SDL_AudioSpec have;
        private static uint soundDeviceId;

        static void AudioCallback(IntPtr userdata, IntPtr stream, int lenInBytes)
        {
            Span<short> streamSpan;
            unsafe
            {
                short* buffer = (short*)stream.ToPointer();
                streamSpan = new Span<short>(buffer, lenInBytes / sizeof(short));
            }
            for (int sample = 0; sample < have.samples; sample++)
            {
                short data = GetData(sample);

                for (int channelId = 0; channelId < have.channels; channelId++)
                {
                    int offset = (sample * have.channels) + channelId;
                    streamSpan[offset] = data;
                }
            }


            short GetData(int sample)
            {
                short result = (short)(AMPLITUDE * Math.Sin(samplingIndex));
                samplingIndex += (2.0d * Math.PI * SOUND_FREQUENCY) / have.freq;
                //we want to wrap around after we make full circle
                if (samplingIndex >= (2.0d * Math.PI))
                {
                    samplingIndex -= 2.0d * Math.PI;
                }
                return result;

            }
        }

        static void Main(string[] args)
        {
            vm.ScreenRefresh += OnScreenRefresh;
            // Initilizes SDL.
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) < 0)
            {
                Console.WriteLine($"There was an issue initializing SDL. {SDL.SDL_GetError()}");
            }
            SDL.SDL_AudioSpec want = new SDL.SDL_AudioSpec();
            want.freq = 44100;
            want.format = SDL.AUDIO_S16SYS;
            want.channels = 1;
            want.samples = 512;
            want.callback = AudioCallback;

            int audioDevicesCount = SDL.SDL_GetNumAudioDevices(0);
            List<string> soundDevicesNames = new List<string>();
            for (int i = 0; i < audioDevicesCount; i++)
            {
                soundDevicesNames.Add(SDL.SDL_GetAudioDeviceName(i, 0));

            }
            if (soundDevicesNames.Any())
            {
                //TODO: make it possible to choose sound device
                soundDeviceId = SDL.SDL_OpenAudioDevice(soundDevicesNames[1], 0, ref want, out have, 0);

                if (soundDeviceId == 0)
                {
                    Console.WriteLine($"Failed to open audio: {SDL.SDL_GetError()}");
                }
                else if (want.format != have.format)
                {
                    Console.WriteLine($"Failed to get the desired AudioSpec. Instead got:");
                    Console.WriteLine($"frequency: {have.freq}");
                    Console.WriteLine($"format: {have.format}");
                    Console.WriteLine($"channels: {have.channels}");
                    Console.WriteLine($"samples: {have.samples}");
                    Console.WriteLine($"sizes: {have.size}");
                }
                else
                {
                    vm.BeepStatus += OnBeepStatusChanged;
                }
            }
            else
            {
                Console.WriteLine("No audio device suitable for playback.");
            }
            // Enable drop of file            
            SDL.SDL_EventState(SDL.SDL_EventType.SDL_DROPFILE, SDL.SDL_ENABLE);
            // Create a new window given a title, size, and passes it a flag indicating it should be shown.
            windowPtr = SDL.SDL_CreateWindow("Yet another Chip8 Interpreter",
                                              SDL.SDL_WINDOWPOS_UNDEFINED,
                                              SDL.SDL_WINDOWPOS_UNDEFINED,
                                              64 * PIXEL_SIZE, 32 * PIXEL_SIZE,
                                              SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

            if (windowPtr == IntPtr.Zero)
            {
                Console.WriteLine($"There was an issue creating the window. {SDL.SDL_GetError()}");
            }

            // Creates a new SDL hardware renderer.
            var rendererPtr = SDL.SDL_CreateRenderer(windowPtr,
                                                  -1,
                                                  SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

            if (rendererPtr == IntPtr.Zero)
            {
                Console.WriteLine($"There was an issue creating the renderer. {SDL.SDL_GetError()}");
            }

            int pitch = 0;
            IntPtr windowTexturePtr;
            GetWindowTexturePtrAndPitch(windowPtr, rendererPtr, out pitch, out windowTexturePtr);


            if (windowTexturePtr == IntPtr.Zero)
            {
                Console.WriteLine($"There was an issue reading window texture.  {SDL.SDL_GetError()}");
            }
            else
            {
                MainLoop(pitch, windowTexturePtr, rendererPtr);
            }
            // Shutdown vm
            cancellationTokenSource.Cancel();
            vmTask?.Wait();
            // Clean up the resources that were created.

            SDL.SDL_CloseAudioDevice(soundDeviceId);
            SDL.SDL_DestroyRenderer(rendererPtr);
            SDL.SDL_DestroyWindow(windowPtr);
            SDL.SDL_Quit();
        }

        private static void StartVm(string file)
        {
            if (!vmTask?.IsCompleted ?? false)
            {
                cancellationTokenSource.Cancel();
                vmTask.Wait();
            }
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var cancelToken = cancellationTokenSource.Token;
            vm.Reset();
            vm.Load(file);
            vmTask = Task.Run(() => vm.Start(cancelToken), cancelToken);

        }

        private static void GetWindowTexturePtrAndPitch(IntPtr windowPtr, IntPtr rendererPtr, out int pitch, out IntPtr windowTexturePtr)
        {
            pitch = 0;
            windowTexturePtr = IntPtr.Zero;

            var windowSurfacePtr = SDL.SDL_GetWindowSurface(windowPtr);

            if (windowSurfacePtr != IntPtr.Zero)
            {
                object? tmpMarshaledObject = Marshal.PtrToStructure(windowSurfacePtr, typeof(SDL2.SDL.SDL_Surface));

                if (tmpMarshaledObject != null)
                {
                    var windowSurfaceStruct = (SDL2.SDL.SDL_Surface)tmpMarshaledObject;
                    pitch = windowSurfaceStruct.pitch;

                    tmpMarshaledObject = Marshal.PtrToStructure(windowSurfaceStruct.format, typeof(SDL2.SDL.SDL_PixelFormat));
                    if (tmpMarshaledObject != null)
                    {
                        var format_struct = (SDL2.SDL.SDL_PixelFormat)tmpMarshaledObject;

                        windowTexturePtr = SDL.SDL_CreateTexture(rendererPtr,
                                                                 format_struct.format,
                                                                 (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                                                                 64 * PIXEL_SIZE, 32 * PIXEL_SIZE);
                    }
                    else
                    {
                        Console.WriteLine($"There was an issue reading window pixel format.  {SDL.SDL_GetError()}");
                    }
                }
                else
                {
                    Console.WriteLine($"There was an issue reading window texture.  {SDL.SDL_GetError()}");
                }
            }
            else
            {
                Console.WriteLine($"There was an issue reading window surface.  {SDL.SDL_GetError()}");
            }
        }
        private static void MainLoop(int pitch, IntPtr windowTexturePtr, IntPtr rendererPtr)
        {
            var running = true;
            var surface = new byte[512 * 4096];
            var keysMapping = new Dictionary<SDL.SDL_Keycode, ushort>()
            {
                {SDL.SDL_Keycode.SDLK_1,0x1},
                {SDL.SDL_Keycode.SDLK_2,0x2},
                {SDL.SDL_Keycode.SDLK_3,0x3},
                {SDL.SDL_Keycode.SDLK_4,0xC},
                {SDL.SDL_Keycode.SDLK_q,0x4},
                {SDL.SDL_Keycode.SDLK_w,0x5},
                {SDL.SDL_Keycode.SDLK_e,0x6},
                {SDL.SDL_Keycode.SDLK_r,0xD},
                {SDL.SDL_Keycode.SDLK_a,0x7},
                {SDL.SDL_Keycode.SDLK_s,0x8},
                {SDL.SDL_Keycode.SDLK_d,0x9},
                {SDL.SDL_Keycode.SDLK_f,0xE},
                {SDL.SDL_Keycode.SDLK_z,0xA},
                {SDL.SDL_Keycode.SDLK_x,0x0},
                {SDL.SDL_Keycode.SDLK_c,0xB},
                {SDL.SDL_Keycode.SDLK_v,0xF},
            };

            // Main loop for the program
            while (running)
            {
                // Check to see if there are any events and continue to do so until the queue is empty.
                while (SDL.SDL_PollEvent(out SDL.SDL_Event e) == 1)
                {
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_QUIT:
                            running = false;
                            break;
                        case SDL.SDL_EventType.SDL_KEYDOWN:
                            {
                                if (keysMapping.TryGetValue(e.key.keysym.sym, out ushort keyValue))
                                {
                                    vm.UpdateKeyState(keyValue, true);
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_KEYUP:
                            {
                                if (keysMapping.TryGetValue(e.key.keysym.sym, out ushort keyValue))
                                {
                                    vm.UpdateKeyState(keyValue, false);
                                }
                                break;
                            }
                        case SDL.SDL_EventType.SDL_DROPFILE:
                            {
                                //TODO: on drop, try to read file and see if we can run it
                                //      in the emulator
                                string s = SDL.UTF8_ToManaged(e.drop.file, true);


                                SDL.SDL_ShowSimpleMessageBox(
                                    SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_INFORMATION,
                                    "File dropped on window",
                                    $"Starting {s}",
                                    windowPtr);
                                StartVm(s);

                                break;
                            }
                    }
                }

                lock (vmSurfaceLock)
                {
                    for (int i = 0; i < vmSurface.GetLength(0); i++)
                    {
                        for (int j = 0; j < vmSurface.GetLength(1); j++)
                        {
                            if (!TryUpdatePixel(surface, i, j, pitch, vmSurface[i, j]))
                            {
                                running = false;
                                Console.WriteLine($"Failed to update pixel {i} {j}");
                            }
                        }
                    }
                }

                //Update window.
                unsafe
                {
                    fixed (byte* surfacePtr = surface)
                    {
                        SDL.SDL_UpdateTexture(windowTexturePtr, IntPtr.Zero, (IntPtr)surfacePtr, pitch);
                        SDL.SDL_RenderCopy(rendererPtr, windowTexturePtr, IntPtr.Zero, IntPtr.Zero);
                        SDL.SDL_RenderPresent(rendererPtr);
                    }
                }
                //to not kill CPU
                System.Threading.Thread.Sleep(10);
            }
        }


        private static void OnBeepStatusChanged(object? sender, bool status)
        {
            SDL.SDL_PauseAudioDevice(soundDeviceId, status ? 0 : 1);
        }

        private static void OnScreenRefresh(object? sender, ScreenRefreshEventArgs args)
        {
            lock (vmSurfaceLock)
            {
                switch (args.RequestType)
                {
                    case RefreshRequest.Clear:
                        ClearSurface();
                        break;
                    case RefreshRequest.Draw:
                        vmSurface = args.Surface;
                        break;
                }
            }

        }
        private static void ClearSurface()
        {
            unsafe
            {
                fixed (bool* surfacePointer = &vmSurface[0, 0])
                {
                    var surfaceSpan = new Span<bool>(surfacePointer, 64 * 32);
                    surfaceSpan.Fill(false);
                }
            }
        }
        private static bool TryUpdatePixel(byte[] surface, int x, int y, int pitch, bool set = false)
        {
            bool result = true;
            var index = (y * PIXEL_SIZE * pitch + x * PIXEL_SIZE * 4);
            if ((index + PIXEL_SIZE) >= surface.Length)
            {
                result = false;
            }
            else
            {
                for (int a = 0; a < PIXEL_SIZE; a++)
                {
                    int currentLineIndex = index + (a * pitch);
                    for (int b = 0; b < PIXEL_SIZE; b++)
                    {
                        int currentIndex = currentLineIndex + (b * 4);
                        surface[currentIndex] = 0; //blue
                        surface[currentIndex + 1] = 0;  //green
                        surface[currentIndex + 2] = (byte)(set ? 0xff : 0);//red
                        surface[currentIndex + 3] = 0; //alpha
                    }
                }
            }
            return result;
        }
    }
}