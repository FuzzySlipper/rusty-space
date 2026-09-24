#!/usr/bin/env python3
"""Generate the bridge theater's audio content.

Two small loop-safe clips, authored as product content for task #8313:

- drive-hum.wav: a 2-second seamless drive loop. Integer-cycle harmonics of
  55 Hz only, so the loop boundary is click-free by construction.
- impact-thud.wav: a 0.5-second hull knock: decaying low sine plus a decaying
  noise burst, both reaching digital silence before the end.

44.1 kHz mono 16-bit PCM. Regenerate with: python3 scripts/generate-bridge-audio.py
"""
import math
import struct
import wave
from pathlib import Path

RATE = 44100


def write_mono(path: Path, samples: list[float]) -> None:
    peak = max(1e-9, max(abs(s) for s in samples))
    scale = 0.89 / peak
    frames = b"".join(
        struct.pack("<h", int(max(-1.0, min(1.0, s * scale)) * 32767))
        for s in samples
    )
    with wave.open(str(path), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(frames)
    print(f"wrote {path} ({len(samples) / RATE:.2f}s)")


def drive_hum() -> list[float]:
    seconds = 2.0
    count = int(RATE * seconds)
    partials = [(55.0, 1.0), (110.0, 0.45), (165.0, 0.22), (220.0, 0.10)]
    out = []
    for i in range(count):
        t = i / RATE
        s = sum(a * math.sin(2.0 * math.pi * f * t) for f, a in partials)
        out.append(s / len(partials))
    return out


def impact_thud() -> list[float]:
    seconds = 0.5
    count = int(RATE * seconds)
    # Deterministic pseudo-noise: a hash of the sample index, so regeneration
    # is bit-stable without shipping a random seed.
    out = []
    for i in range(count):
        t = i / RATE
        env = math.exp(-t * 14.0)
        knock = math.sin(2.0 * math.pi * 60.0 * t) * env
        hiss = (((i * 1103515245 + 12345) >> 16) / 32768.0 - 1.0) * math.exp(-t * 30.0)
        out.append(0.7 * knock + 0.3 * hiss)
    return out


def main() -> None:
    root = Path(__file__).resolve().parent.parent / "content" / "audio"
    root.mkdir(parents=True, exist_ok=True)
    write_mono(root / "drive-hum.wav", drive_hum())
    write_mono(root / "impact-thud.wav", impact_thud())


if __name__ == "__main__":
    main()
