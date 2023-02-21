using System;

namespace yac8i.gui.sdl
{
    public class Program
    {
        private static Chip8VM vm = new Chip8VM();

        static void Main(string[] args)
        {
            vm.NewMessage += (s, a) =>
            {
                Console.WriteLine(a);
            };

            using (SDLFront sdlFront = new SDLFront(vm))
            {
                if (!sdlFront.Initialize())
                {
                    Console.WriteLine("Error while initializing SDL front.");
                }
                else
                {
                    var audioDevices = sdlFront.GetAudioDevices();
                    sdlFront.ChooseAudioDevice(audioDevices[0]);
                    sdlFront.Start();
                }
            }
        }
    }
}
