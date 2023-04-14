using System;
using SDL2;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace yac8i.gui.sdl
{
    public class SDLVideo : IDisposable
    {
        public const int PIXEL_SIZE = 16;
        private bool[,] vmSurface = new bool[64, 32];
        bool running = true;
        byte[] surface = new byte[512 * 4096];
        private IntPtr windowPtr;
        private IntPtr windowTexturePtr;
        private IntPtr rendererPtr;
        private int pitch;
        private Chip8VM vm;
        private static Dictionary<SDL.SDL_Keycode, ushort> keysMapping = new Dictionary<SDL.SDL_Keycode, ushort>()
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

        public SDLVideo(Chip8VM vm, IntPtr windowPtr, IntPtr rendererPtr)
        {
            this.windowPtr = windowPtr;
            this.rendererPtr = rendererPtr;
            this.vm = vm;
        }

        public bool Initialize()
        {
            bool result = true;
            GetWindowTexturePtrAndPitch(windowPtr, rendererPtr, out pitch, out windowTexturePtr);
            if (windowTexturePtr == IntPtr.Zero)
            {
                Console.WriteLine($"There was an issue reading window texture.  {SDL.SDL_GetError()}");
                result = false;
            }
            return result;
        }

        public void Dispose()
        {
            running = false;

            SDL.SDL_DestroyRenderer(rendererPtr);
            SDL.SDL_DestroyWindow(windowPtr);
        }

        public void MainLoop()
        {
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

                vm.TickAutoResetEvent.WaitOne(200);
            }
        }

        private void GetWindowTexturePtrAndPitch(IntPtr windowPtr, IntPtr rendererPtr, out int pitch, out IntPtr windowTexturePtr)
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
                                                                 64 * SDLVideo.PIXEL_SIZE, 32 * SDLVideo.PIXEL_SIZE);
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

        private bool TryUpdatePixel(byte[] surface, int x, int y, int pitch, bool set = false)
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

    }
}