//based on https://jsayers.dev/category/c-sdl-tutorial-series/
using System.Runtime.InteropServices;
using System;
using SDL2;
using System.Threading.Tasks;
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

        static void Main(string[] args)
        {
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

            vm.Load(@"simple_demo.ch8");
            vm.ScreenRefresh += OnScreenRefresh;

            // Initilizes SDL.
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                Console.WriteLine($"There was an issue initilizing SDL. {SDL.SDL_GetError()}");
            }

            // Create a new window given a title, size, and passes it a flag indicating it should be shown.
            var windowPtr = SDL.SDL_CreateWindow("Yet another Chip8 Interpreter",
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
                Task.Factory.StartNew(() =>
                             {
                                 vm.Start();
                             }, TaskCreationOptions.LongRunning);

                MainLoop(pitch, windowTexturePtr, rendererPtr);
            }

            // Clean up the resources that were created.
            SDL.SDL_DestroyRenderer(rendererPtr);
            SDL.SDL_DestroyWindow(windowPtr);
            SDL.SDL_Quit();
        }
        private static void GetWindowTexturePtrAndPitch(IntPtr windowPtr, IntPtr rendererPtr, out int pitch, out IntPtr windowTexturePtr)
        {
            pitch = 0;
            windowTexturePtr = IntPtr.Zero;

            var windowSurfacePtr = SDL.SDL_GetWindowSurface(windowPtr);

            if (windowSurfacePtr != IntPtr.Zero)
            {
                var windowSurfaceStruct = (SDL2.SDL.SDL_Surface)Marshal.PtrToStructure(
                    windowSurfacePtr,
                    typeof(SDL2.SDL.SDL_Surface));

                pitch = windowSurfaceStruct.pitch;

                var format_struct = (SDL2.SDL.SDL_PixelFormat)Marshal.PtrToStructure(
                    windowSurfaceStruct.format,
                    typeof(SDL2.SDL.SDL_PixelFormat));

                windowTexturePtr = SDL.SDL_CreateTexture(rendererPtr,
                                       format_struct.format,
                                       (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                                       64 * PIXEL_SIZE, 32 * PIXEL_SIZE);
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
        private static void OnScreenRefresh(object? sender, ScreenRefreshEventArgs args)
        {
            lock (vmSurfaceLock)
            {
                switch (args.RequestType)
                {
                    case RefreshRequest.Clear:
                        vmSurface = new bool[64, 32];
                        break;
                    case RefreshRequest.Draw:
                        vmSurface = args.Surface;
                        break;
                }
            }

        }
        private static bool TryUpdatePixel(byte[] surface, int x, int y, int pitch, bool set = false)
        {
            bool result = true;
            var index = (y * PIXEL_SIZE * pitch + x * PIXEL_SIZE * 4);
            if ((index + 16) >= surface.Length)
            {
                result = false;
            }
            else
            {
                for (int a = 0; a < 16; a++)
                {
                    int currentLineIndex = index + (a * pitch);
                    for (int b = 0; b < 16; b++)
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