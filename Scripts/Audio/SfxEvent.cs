using Godot;
using Godot.Collections;

namespace Veilrun.Audio;

/// <summary>
/// One sound "event" (e.g. a concrete footstep): a set of variants plus randomization.
/// Built once into an AudioStreamRandomizer, which avoids immediate repeats.
/// </summary>
[GlobalClass]
public partial class SfxEvent : Resource
{
    private AudioStreamRandomizer? _stream;

    [Export] public Array<AudioStream> Variants { get; set; } = new();
    [Export(PropertyHint.Range, "-40,12,0.5,suffix:dB")] public float VolumeDb { get; set; }
    [Export(PropertyHint.Range, "0,6,0.05,suffix:st")] public float RandomPitchSemitones { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,12,0.5,suffix:dB")] public float RandomVolumeDb { get; set; } = 1.5f;

    public bool IsEmpty => Variants.Count == 0;

    public AudioStream? Stream
    {
        get
        {
            if (_stream is null && !IsEmpty)
            {
                _stream = new AudioStreamRandomizer
                {
                    PlaybackMode = AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats,
                    RandomPitchSemitones = RandomPitchSemitones,
                    RandomVolumeOffsetDb = RandomVolumeDb,
                };
                for (int i = 0; i < Variants.Count; i++)
                {
                    _stream.AddStream(i, Variants[i]);
                }
            }

            return _stream;
        }
    }
}
