//based on https://jsayers.dev/category/c-sdl-tutorial-series/
using System.Collections.Generic;
using System;
using SDL2;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;

namespace yac8i.gui.sdl
{
    public class SDLFront
    {
        public AutoResetEvent DoFrameAutoResetEvent = new AutoResetEvent(false);
        private const int PIXEL_SIZE = 16;
        private bool[,] vmSurface = new bool[64, 32];
        private object vmSurfaceLock = new object();
        private Chip8VM vm;

        private IntPtr windowPtr;
        private IntPtr windowTexturePtr;
        private IntPtr rendererPtr;
        private int pitch;
        private double samplingIndex = 0;
        private const double AMPLITUDE = 28000d;
        private const double SOUND_FREQUENCY = 261.63d;
        private SDL.SDL_AudioSpec have;
        private uint soundDeviceId;

        public SDLFront(Chip8VM vm)
        {
            this.vm = vm;
        }

        public void InitializeAndStart()
        {
            // Initilizes SDL.
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO) < 0)
            {
                throw new Exception($"There was an issue initializing SDL. {SDL.SDL_GetError()}");
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
                    throw new Exception($"Failed to open audio: {SDL.SDL_GetError()}");
                }
                else if (want.format != have.format)
                {
                    throw new Exception($"Failed to get the desired AudioSpec. Instead got: frequency: {have.freq}, format: {have.format}, channels: {have.channels}, samples: {have.samples}, sizes: {have.size}");
                }
                else
                {
                    vm.BeepStatus += OnBeepStatusChanged;
                }
            }
            else
            {
                throw new Exception("No audio device suitable for playback.");
            }

            // Create a new window given a title, size, and passes it a flag indicating it should be shown.
            windowPtr = SDL.SDL_CreateWindow("Yet another Chip8 Interpreter",
                                              SDL.SDL_WINDOWPOS_UNDEFINED,
                                              SDL.SDL_WINDOWPOS_UNDEFINED,
                                              64 * PIXEL_SIZE, 32 * PIXEL_SIZE,
                                              SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

            if (windowPtr == IntPtr.Zero)
            {
                throw new Exception($"There was an issue creating the window. {SDL.SDL_GetError()}");
            }
            SetWindowTexturePtrAndPitch(windowPtr);


            if (windowTexturePtr == IntPtr.Zero)
            {
                throw new Exception($"There was an issue reading window texture.  {SDL.SDL_GetError()}");
            }
            else
            {
                MainLoop();
            }

            // Clean up the resources that were created.

            SDL.SDL_CloseAudioDevice(soundDeviceId);
            SDL.SDL_DestroyRenderer(rendererPtr);
            SDL.SDL_DestroyWindow(windowPtr);
            SDL.SDL_Quit();
        }

        private void SetWindowTexturePtrAndPitch(IntPtr windowPtr)
        {
            pitch = 0;
            windowTexturePtr = IntPtr.Zero;

            // Creates a new SDL hardware renderer. 
            rendererPtr = SDL.SDL_CreateRenderer(windowPtr,
                                                  -1,
                                                  SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            if (rendererPtr == IntPtr.Zero)
            {
                throw new Exception($"There was an issue creating the renderer. {SDL.SDL_GetError()}");
            }
            uint pixelFormat = SDL.SDL_GetWindowPixelFormat(windowPtr);
            windowTexturePtr = SDL.SDL_CreateTexture(rendererPtr,
                                                    pixelFormat,
                                                     (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                                                     64 * PIXEL_SIZE, 32 * PIXEL_SIZE);
            
            
            pitch = SDL.SDL_BYTESPERPIXEL(pixelFormat) * 64 * PIXEL_SIZE;

        }

        private void MainLoop()
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
                while (SDL.SDL_PollEvent(out SDL.SDL_Event e) > 0)
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
                    }
                }

                vmSurface = vm.Surface;
                lock (vmSurfaceLock)
                {
                    for (int i = 0; i < vmSurface.GetLength(0); i++)
                    {
                        for (int j = 0; j < vmSurface.GetLength(1); j++)
                        {
                            if (!TryUpdatePixel(surface, i, j, vmSurface[i, j]))
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

                DoFrameAutoResetEvent.WaitOne(200);
            }
        }

        private void OnBeepStatusChanged(object? sender, bool status)
        {
            SDL.SDL_PauseAudioDevice(soundDeviceId, status ? 0 : 1);
        }

        private bool TryUpdatePixel(byte[] surface, int x, int y, bool set = false)
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
                        if (set)
                        {
                            surface[currentIndex + 2] = (byte)0xff;//red
                            surface[currentIndex + 3] = 0; //alpha
                        }
                        else
                        {
                            if (surface[currentIndex + 3] > 0)
                            {
                                surface[currentIndex + 3] = 0;
                                surface[currentIndex + 2] = 0;
                            }
                            else
                            {
                                surface[currentIndex + 3] = (byte)0x7f;
                            }
                        }

                    }
                }
            }
            return result;
        }

        private void AudioCallback(IntPtr userdata, IntPtr stream, int lenInBytes)
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
    }
}
