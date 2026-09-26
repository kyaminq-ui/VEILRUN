"""Procedural PLACEHOLDER movement sounds for VEILRUN (original, license-free, generated from noise/sines).

Output: Assets/Audio/SFX/Placeholder/Movement/*.wav   (44.1 kHz, 16-bit mono)
Loops carry a RIFF 'smpl' chunk so Godot's WAV importer ("Detect From WAV") loops them.
Regenerate:  python tools/generators/gen_placeholder_sfx.py
These are meant to be replaced by final assets (see docs/AUDIO_BIBLE.md and ASSET_PROVENANCE.md).
"""
import os
import struct

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Audio", "SFX", "Placeholder", "Movement")
rng = np.random.default_rng(20260926)


# ---------------------------------------------------------------- DSP helpers
def t_axis(duration):
    return np.arange(int(duration * SR)) / SR


def band_noise(n, lo, hi, tilt=0.0):
    """White noise shaped in the frequency domain (circular → seamless when looped)."""
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    gain = 1.0 / (1.0 + (lo / np.maximum(f, 1.0)) ** 4)          # 4th-order-ish highpass
    gain *= 1.0 / (1.0 + (f / hi) ** 4)                           # 4th-order-ish lowpass
    if tilt:
        gain *= (np.maximum(f, 20.0) / 1000.0) ** tilt            # spectral tilt (negative = darker)
    out = np.fft.irfft(spec * gain, n)
    return out / (np.max(np.abs(out)) + 1e-9)


def env_exp(n, attack, decay, start=0.0):
    t = np.arange(n) / SR - start
    e = np.where(t < 0, 0.0, np.where(t < attack, t / max(attack, 1e-6), np.exp(-(t - attack) / decay)))
    return e


def sweep(n, f0, f1, tau):
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / tau)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def delay(x, seconds):
    k = int(seconds * SR)
    return np.concatenate([np.zeros(k), x[: len(x) - k]]) if k > 0 else x


def normalize(x, peak_db=-3.0):
    return x / (np.max(np.abs(x)) + 1e-9) * (10 ** (peak_db / 20))


def fade(x, ms=4):
    k = int(ms / 1000 * SR)
    x = x.copy()
    x[-k:] *= np.linspace(1, 0, k)
    return x


def write_wav(name, x, loop=False):
    os.makedirs(OUT, exist_ok=True)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    frames = len(x)
    chunks = b"fmt " + struct.pack("<IHHIIHH", 16, 1, 1, SR, SR * 2, 2, 16)
    chunks += b"data" + struct.pack("<I", len(data)) + data
    if loop:
        smpl = struct.pack("<9I", 0, 0, int(1e9 / SR), 60, 0, 0, 0, 1, 0)
        smpl += struct.pack("<6I", 0, 0, 0, frames - 1, 0, 0)
        chunks += b"smpl" + struct.pack("<I", len(smpl)) + smpl
    with open(os.path.join(OUT, name), "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 4 + len(chunks)) + b"WAVE" + chunks)


# ---------------------------------------------------------------- sound designs
def footstep(surface):
    n = int(0.3 * SR)
    j = lambda a=0.1: 1 + rng.uniform(-a, a)
    heel = sweep(n, 120 * j(), 55 * j(), 0.02) * env_exp(n, 0.002, 0.018 * j()) * 0.9
    scuff = band_noise(n, 700 * j(), 5000 * j(), -0.3) * env_exp(n, 0.002, 0.022 * j()) * 0.55
    toe_t = rng.uniform(0.012, 0.03)
    toe = band_noise(n, 1800, 9000) * env_exp(n, 0.001, 0.012, toe_t) * 0.3 * j(0.3)
    x = heel + scuff + toe
    if surface == "metal":
        ring = np.zeros(n)
        for f, a, d in ((310, 0.35, 0.18), (787, 0.25, 0.12), (1432, 0.18, 0.09), (2380, 0.12, 0.06)):
            ring += np.sin(2 * np.pi * f * j(0.05) * t_axis(0.3)[:n]) * a * env_exp(n, 0.001, d * j(0.2))
        x = x * 0.8 + ring * 0.9
    return fade(normalize(x, -4.0 - rng.uniform(0, 2)))


def jump():
    n = int(0.45 * SR)
    j = lambda a=0.1: 1 + rng.uniform(-a, a)
    push = sweep(n, 140 * j(), 60, 0.03) * env_exp(n, 0.002, 0.03) * 0.8
    grit = band_noise(n, 900, 6000) * env_exp(n, 0.001, 0.02) * 0.4
    t = t_axis(0.45)[:n]
    whoosh_env = np.sin(np.clip(t / 0.3, 0, 1) * np.pi) ** 2
    whoosh = band_noise(n, 350 * j(), 2600 * j(), -0.5) * whoosh_env * 0.45
    cloth = band_noise(n, 2500, 9000) * (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(22, 35) * t)) * whoosh_env * 0.12
    return fade(normalize(push + grit + whoosh + cloth, -3.0))


def landing(tier):
    # tier: 0 soft, 1 medium, 2 heavy, 3 deadly
    dur = (0.35, 0.5, 0.7, 1.4)[tier]
    n = int(dur * SR)
    j = lambda a=0.1: 1 + rng.uniform(-a, a)
    thump = sweep(n, (110, 100, 90, 80)[tier] * j(), (60, 50, 42, 35)[tier], 0.03) * env_exp(n, 0.002, (0.03, 0.05, 0.08, 0.12)[tier]) * 1.0
    body = band_noise(n, 40, (500, 450, 400, 350)[tier], -0.8) * env_exp(n, 0.002, (0.03, 0.06, 0.1, 0.18)[tier]) * (0.4 + 0.15 * tier)
    scuff = band_noise(n, 800, 7000, -0.3) * env_exp(n, 0.002, (0.03, 0.04, 0.05, 0.06)[tier]) * 0.5
    x = thump + body + scuff
    if tier >= 1:  # second contact (other foot / knee)
        step = np.pad(footstep("concrete"), (0, max(0, n - int(0.3 * SR))))[:n]
        x += delay(step, rng.uniform(0.05, 0.09)) * (0.35 + 0.1 * tier)
    if tier >= 2:  # hands slapping the ground
        x += delay(band_noise(n, 1200, 8000) * env_exp(n, 0.001, 0.015), rng.uniform(0.11, 0.16)) * 0.5
    if tier == 3:  # crack + long rumble
        crack = np.zeros(n)
        for k in range(5):
            crack += band_noise(n, 2500, 12000) * env_exp(n, 0.0005, 0.004, 0.02 + k * rng.uniform(0.006, 0.012))
        x += crack * 0.6 + band_noise(n, 25, 180, -1.0) * env_exp(n, 0.02, 0.5) * 0.7
    return fade(normalize(x, -1.5 - (3 - tier)), 20)


def breath_loop(heavy):
    period = 1.1 if heavy else 3.2
    cycles = 8 if heavy else 4
    n_cycle = int(period * SR)
    out = []
    for _ in range(cycles):
        t = np.arange(n_cycle) / SR
        in_len, out_len = (0.38, 0.52) if heavy else (1.1, 1.5)
        in_len *= rng.uniform(0.92, 1.08)
        out_len *= rng.uniform(0.92, 1.08)
        inhale_env = np.where(t < in_len, np.sin(np.pi * t / in_len) ** 2, 0.0)
        t2 = t - in_len - (0.02 if heavy else 0.15)
        exhale_env = np.where((t2 > 0) & (t2 < out_len), np.clip(np.sin(np.pi * np.clip(t2, 0, None) / out_len), 0, None) ** 1.5, 0.0)
        inhale = band_noise(n_cycle, 700, 3800, -0.4) * inhale_env * (0.55 if heavy else 0.35)
        exhale = band_noise(n_cycle, 300, 2400, -0.6) * exhale_env * (0.8 if heavy else 0.45)
        if heavy:  # a little voiced "huh" in the exhale
            voiced = np.sin(2 * np.pi * rng.uniform(150, 190) * t) * band_noise(n_cycle, 200, 900) * exhale_env * 0.25
            exhale += voiced
        out.append(inhale + exhale)
    return normalize(np.concatenate(out), -8.0 if heavy else -12.0)


def wind_loop():
    dur = 8.0
    n = int(dur * SR)
    base = band_noise(n, 60, 1400, -0.9)
    howl = band_noise(n, 300, 700) * 0.35
    t = np.arange(n) / SR
    gust = 0.65 + 0.2 * np.sin(2 * np.pi * t / dur * 2 + 0.7) + 0.15 * np.sin(2 * np.pi * t / dur * 5 + 2.1)
    return normalize((base + howl) * gust, -6.0)


def hand_plant():
    n = int(0.3 * SR)
    j = lambda a=0.1: 1 + rng.uniform(-a, a)
    slap = band_noise(n, 1500 * j(), 9000) * env_exp(n, 0.0008, 0.012 * j()) * 0.8
    palm = sweep(n, 220 * j(), 110, 0.01) * env_exp(n, 0.001, 0.02) * 0.5
    second = delay(band_noise(n, 1200, 8000) * env_exp(n, 0.0008, 0.01), rng.uniform(0.025, 0.05)) * 0.5
    cloth = band_noise(n, 2500, 9000) * env_exp(n, 0.01, 0.08, 0.02) * 0.15
    return fade(normalize(slap + palm + second + cloth, -4.0))


def slide_loop():
    dur = 3.0
    n = int(dur * SR)
    t = np.arange(n) / SR
    grit = band_noise(n, 600, 6000, -0.4)
    rumble = band_noise(n, 40, 400, -0.8) * 0.6
    rough = 0.75 + 0.25 * np.sin(2 * np.pi * t / dur * 37) * np.sin(2 * np.pi * t / dur * 11 + 1.3)
    return normalize((grit + rumble) * rough, -7.0)


def roll():
    n = int(0.6 * SR)
    x = np.zeros(n)
    for k, (tt, a) in enumerate(((0.0, 1.0), (0.13, 0.7), (0.27, 0.55))):
        step = np.pad(landing(0), (0, max(0, n - int(0.35 * SR))))[:n]
        x += delay(step, tt + rng.uniform(-0.01, 0.01)) * a
    t = np.arange(n) / SR
    x += band_noise(n, 400, 3000, -0.5) * np.sin(np.clip(t / 0.5, 0, 1) * np.pi) ** 2 * 0.35
    return fade(normalize(x, -2.5), 20)


# ---------------------------------------------------------------- emit
if __name__ == "__main__":
    for i in range(1, 9):
        write_wav(f"footstep_concrete_{i:02d}.wav", footstep("concrete"))
    for i in range(1, 7):
        write_wav(f"footstep_metal_{i:02d}.wav", footstep("metal"))
    for i in range(1, 5):
        write_wav(f"jump_{i:02d}.wav", jump())
    for tier, name in enumerate(("soft", "medium", "heavy", "deadly")):
        for i in range(1, (4 if tier < 3 else 2)):
            write_wav(f"land_{name}_{i:02d}.wav", landing(tier))
    for i in range(1, 5):
        write_wav(f"hand_plant_{i:02d}.wav", hand_plant())
    for i in range(1, 3):
        write_wav(f"roll_{i:02d}.wav", roll())
    write_wav("slide_loop.wav", slide_loop(), loop=True)
    write_wav("breath_calm_loop.wav", breath_loop(False), loop=True)
    write_wav("breath_heavy_loop.wav", breath_loop(True), loop=True)
    write_wav("wind_loop.wav", wind_loop(), loop=True)
    print("wrote", len(os.listdir(OUT)), "files to", os.path.normpath(OUT))
