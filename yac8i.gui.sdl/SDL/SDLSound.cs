
using System.Collections.Generic;
using System;
using SDL2;

namespace yac8i.gui.sdl
{
    public class SDLSound : IDisposable
    {
        public IReadOnlyCollection<string> SoundDevicesNames => soundDevicesNames;

        private readonly Chip8VM vm;
        private double samplingIndex = 0;
        private const double AMPLITUDE = 28000d;
        private const double SOUND_FREQUENCY = 261.63d;
        private SDL.SDL_AudioSpec have;
        private readonly uint soundDeviceId;
        private readonly List<string> soundDevicesNames = [];

        public SDLSound(Chip8VM vm)
        {
            this.vm = vm;
            SDL.SDL_AudioSpec want = new()
            {
                freq = 44100,
                format = SDL.AUDIO_S16SYS,
                channels = 1,
                samples = 512,
                callback = AudioCallback
            };

            int audioDevicesCount = SDL.SDL_GetNumAudioDevices(0);
            for (int i = 0; i < audioDevicesCount; i++)
            {
                soundDevicesNames.Add(SDL.SDL_GetAudioDeviceName(i, 0));
            }

            if (soundDevicesNames.Count != 0)
            {
                //TODO: make it possible to choose sound device
                soundDeviceId = SDL.SDL_OpenAudioDevice(soundDevicesNames[0], 0, ref want, out have, 0);

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
                    this.vm.BeepStatus += OnBeepStatusChanged;
                }
            }
            else
            {
                throw new Exception("No audio device suitable for playback.");
            }
        }

        public void Dispose()
        {
            vm.BeepStatus -= OnBeepStatusChanged;
            SDL.SDL_CloseAudioDevice(soundDeviceId);
        }

        private void OnBeepStatusChanged(object? sender, bool status)
        {
            SDL.SDL_PauseAudioDevice(soundDeviceId, status ? 0 : 1);
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
                short data = GetSoundDataForSample(sample);

                for (int channelId = 0; channelId < have.channels; channelId++)
                {
                    int offset = (sample * have.channels) + channelId;
                    streamSpan[offset] = data;
                }
            }
        }

        private short GetSoundDataForSample(int sample)
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
