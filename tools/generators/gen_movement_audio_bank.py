"""Generates Resources/Audio/DefaultMovementAudio.tres from the placeholder WAVs.

Run after gen_placeholder_sfx.py (and after Godot has imported the WAVs).
When final assets arrive, either edit the .tres in the Godot inspector or point this script at them.
"""
import glob
import os

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SFX_DIR = "Assets/Audio/SFX/Placeholder/Movement"
OUT = os.path.join(ROOT, "Resources", "Audio", "DefaultMovementAudio.tres")

# event property -> (file glob, volume dB, random pitch semitones, random volume dB)
EVENTS = {
    "FootstepConcrete": ("footstep_concrete_*.wav", -6.0, 1.2, 2.0),
    "FootstepMetal": ("footstep_metal_*.wav", -7.0, 0.8, 2.0),
    "Jump": ("jump_*.wav", -8.0, 1.0, 1.5),
    "LandSoft": ("land_soft_*.wav", -6.0, 1.0, 1.5),
    "LandMedium": ("land_medium_*.wav", -3.0, 0.8, 1.0),
    "LandHeavy": ("land_heavy_*.wav", -1.0, 0.6, 1.0),
    "LandDeadly": ("land_deadly_*.wav", 0.0, 0.3, 0.5),
}
LOOPS = {"BreathCalmLoop": "breath_calm_loop.wav", "BreathHeavyLoop": "breath_heavy_loop.wav", "WindLoop": "wind_loop.wav"}

ext, ids = [], {}


def ext_id(path, kind):
    if path not in ids:
        ids[path] = f"{len(ids) + 1}_{kind}"
        ext.append(f'[ext_resource type="{kind}" path="{path}" id="{ids[path]}"]')
    return ids[path]


bank_script = ext_id("res://Scripts/Audio/MovementAudioBank.cs", "Script")
event_script = ext_id("res://Scripts/Audio/SfxEvent.cs", "Script")

subs, props = [], []
for prop, (pattern, vol, pitch, rvol) in EVENTS.items():
    files = sorted(glob.glob(os.path.join(ROOT, SFX_DIR, pattern)))
    if not files:
        raise SystemExit(f"no files for {pattern}")
    refs = ", ".join(f'ExtResource("{ext_id("res://" + SFX_DIR + "/" + os.path.basename(f), "AudioStream")}")' for f in files)
    sid = f"SfxEvent_{prop}"
    subs.append(f'[sub_resource type="Resource" id="{sid}"]\nscript = ExtResource("{event_script}")\n'
                f'Variants = Array[AudioStream]([{refs}])\nVolumeDb = {vol}\nRandomPitchSemitones = {pitch}\nRandomVolumeDb = {rvol}\n')
    props.append(f'{prop} = SubResource("{sid}")')
for prop, name in LOOPS.items():
    props.append(f'{prop} = ExtResource("{ext_id("res://" + SFX_DIR + "/" + name, "AudioStream")}")')

text = f'[gd_resource type="Resource" script_class="MovementAudioBank" load_steps={len(ext) + len(subs) + 1} format=3]\n\n'
text += "\n".join(ext) + "\n\n" + "\n".join(subs) + "\n[resource]\n" + f'script = ExtResource("{bank_script}")\n' + "\n".join(props) + "\n"
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
print("wrote", OUT, len(ext), "ext resources")
