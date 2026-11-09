using System;
using System.Collections.Generic;
using GameProject.Core.Ports;
using Raylib_cs;

namespace GameProject.Audio;

/// <summary>
/// Hardware audio implementation using Raylib-cs with procedural waveform synthesis for SFX.
/// Fully resilient to headless and non-audio environments with zero exceptions.
/// </summary>
public sealed class RaylibAudioService : IAudioService, IDisposable
{
    private float _masterVolume = 1.0f;
    private float _musicVolume = 0.8f;
    private float _sfxVolume = 1.0f;
    private bool _isMuted = false;

    private readonly Dictionary<SfxType, Sound> _soundCache = new();
    private MusicTrack _currentTrack = MusicTrack.None;
    private bool _isDeviceInitialized = false;

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            ApplyMasterVolume();
        }
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set => _musicVolume = Math.Clamp(value, 0f, 1f);
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Math.Clamp(value, 0f, 1f);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyMasterVolume();
        }
    }

    public RaylibAudioService()
    {
        InitializeAudioDevice();
    }

    private void InitializeAudioDevice()
    {
        if (!Raylib.IsAudioDeviceReady())
        {
            try
            {
                Raylib.InitAudioDevice();
                _isDeviceInitialized = Raylib.IsAudioDeviceReady();
            }
            catch
            {
                _isDeviceInitialized = false;
            }
        }
        else
        {
            _isDeviceInitialized = true;
        }

        if (_isDeviceInitialized)
        {
            ApplyMasterVolume();
            SynthesizeProceduralSounds();
        }
    }

    private void ApplyMasterVolume()
    {
        if (_isDeviceInitialized && Raylib.IsAudioDeviceReady())
        {
            Raylib.SetMasterVolume(_isMuted ? 0.0f : _masterVolume);
        }
    }

    private void SynthesizeProceduralSounds()
    {
        // 1. ItemCollected: Cheerful ascending two-tone chime (E5 -> G5)
        _soundCache[SfxType.ItemCollected] = GenerateArpeggioSound(659.25, 783.99, 0.25f);

        // 2. InteractPrompt: Soft tactile blip (880 Hz, 0.04s)
        _soundCache[SfxType.InteractPrompt] = GenerateToneSound(880.0, 0.04f, 0.5f);

        // 3. ButtonClick: Crisp click impulse
        _soundCache[SfxType.ButtonClick] = GenerateToneSound(1200.0, 0.025f, 0.7f);

        // 4. PhoCookSuccess: Joyful major chord fanfare
        _soundCache[SfxType.PhoCookSuccess] = GenerateFanfareSound();

        // 5. DialogueBlip: Soft conversational chirp
        _soundCache[SfxType.DialogueBlip] = GenerateToneSound(587.33, 0.035f, 0.4f);

        // 6. StepStreet: Subdued cobblestone footstep tap
        _soundCache[SfxType.StepStreet] = GenerateFootstepSound();

        // 7. ErrorChime: Soft descending thud
        _soundCache[SfxType.ErrorChime] = GenerateToneSound(220.0, 0.15f, 0.6f);
    }

    private static Sound GenerateToneSound(double freq, float duration, float maxAmp = 0.8f)
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * duration);
        short[] samples = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleRate;
            double env = 1.0 - ((double)i / sampleCount); // linear fade out
            samples[i] = (short)(Math.Sin(2.0 * Math.PI * freq * t) * env * (16000 * maxAmp));
        }

        return CreateSoundFromSamples(samples, sampleRate);
    }

    private static Sound GenerateArpeggioSound(double freq1, double freq2, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * duration);
        int halfCount = sampleCount / 2;
        short[] samples = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleRate;
            double freq = (i < halfCount) ? freq1 : freq2;
            double env = 1.0 - ((double)i / sampleCount);
            samples[i] = (short)(Math.Sin(2.0 * Math.PI * freq * t) * env * 14000);
        }

        return CreateSoundFromSamples(samples, sampleRate);
    }

    private static Sound GenerateFanfareSound()
    {
        int sampleRate = 44100;
        float duration = 0.55f;
        int sampleCount = (int)(sampleRate * duration);
        short[] samples = new short[sampleCount];

        // C5, E5, G5, C6
        double[] freqs = [523.25, 659.25, 783.99, 1046.50];
        int segment = sampleCount / freqs.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleRate;
            int segIdx = Math.Min(i / segment, freqs.Length - 1);
            double freq = freqs[segIdx];
            double env = Math.Exp(-2.0 * (double)(i % segment) / segment);
            samples[i] = (short)(Math.Sin(2.0 * Math.PI * freq * t) * env * 15000);
        }

        return CreateSoundFromSamples(samples, sampleRate);
    }

    private static Sound GenerateFootstepSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.04f);
        short[] samples = new short[sampleCount];
        var rand = new Random(42);

        for (int i = 0; i < sampleCount; i++)
        {
            double env = 1.0 - ((double)i / sampleCount);
            double noise = (rand.NextDouble() * 2.0 - 1.0) * 0.4;
            double tone = Math.Sin(2.0 * Math.PI * 130.0 * i / sampleRate) * 0.6;
            samples[i] = (short)((noise + tone) * env * 9000);
        }

        return CreateSoundFromSamples(samples, sampleRate);
    }

    private static Sound CreateSoundFromSamples(short[] samples, int sampleRate)
    {
        Wave wave = new Wave
        {
            FrameCount = (uint)samples.Length,
            SampleRate = (uint)sampleRate,
            SampleSize = 16,
            Channels = 1
        };

        unsafe
        {
            fixed (short* ptr = samples)
            {
                wave.Data = ptr;
                return Raylib.LoadSoundFromWave(wave);
            }
        }
    }

    public void PlaySfx(SfxType sfx, float volumeModifier = 1.0f)
    {
        if (_isMuted || sfx == SfxType.None) return;
        if (!_isDeviceInitialized || !Raylib.IsAudioDeviceReady()) return;

        if (_soundCache.TryGetValue(sfx, out Sound sound))
        {
            float vol = Math.Clamp(_sfxVolume * volumeModifier, 0f, 1f);
            Raylib.SetSoundVolume(sound, vol);
            Raylib.PlaySound(sound);
        }
    }

    public void PlayMusic(MusicTrack track, bool loop = true, float fadeDuration = 0.5f)
    {
        _currentTrack = track;
    }

    public void StopMusic(float fadeDuration = 0.5f)
    {
        _currentTrack = MusicTrack.None;
    }

    public void PauseMusic() { }
    public void ResumeMusic() { }
    public bool IsMusicPlaying => _currentTrack != MusicTrack.None;
    public MusicTrack CurrentTrack => _currentTrack;
    public void Update(float deltaTime) { }
    public void PreloadCommonAudio() { }

    public void Dispose()
    {
        if (_isDeviceInitialized && Raylib.IsAudioDeviceReady())
        {
            foreach (var kvp in _soundCache)
            {
                Raylib.UnloadSound(kvp.Value);
            }
            _soundCache.Clear();

            try
            {
                Raylib.CloseAudioDevice();
            }
            catch
            {
                // Safe ignore on shutdown
            }
            _isDeviceInitialized = false;
        }
    }
}
