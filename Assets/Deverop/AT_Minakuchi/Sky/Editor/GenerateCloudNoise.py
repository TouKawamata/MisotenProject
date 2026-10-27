"""Rebuild CloudShape.bytes (64 cubed RGBA8, x fastest); requires numpy."""
from pathlib import Path
import numpy as np


def generate():
    size, period = 64, 8
    rng = np.random.default_rng(27102026)
    centers = rng.uniform(0.25, 0.75, (period, period, period, 3)).astype(np.float32)
    values = rng.random((period, period, period), dtype=np.float32)
    z, y, x = np.mgrid[:size, :size, :size].astype(np.float32)
    p = np.stack((x, y, z), axis=-1) * period / size
    cell = np.floor(p).astype(np.int32)
    f = p - cell
    nearest = np.full((size, size, size), 4, dtype=np.float32)
    for dz in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                offset = np.array((dx, dy, dz))
                c = (cell + offset) % period
                delta = offset + centers[c[..., 2], c[..., 1], c[..., 0]] - f
                nearest = np.minimum(nearest, np.sum(delta * delta, axis=-1))
    billows = 1 - np.clip(np.sqrt(nearest), 0, 1)
    u = f * f * (3 - 2 * f)
    noise = np.zeros((size, size, size), dtype=np.float32)
    for dz in (0, 1):
        for dy in (0, 1):
            for dx in (0, 1):
                c = (cell + np.array((dx, dy, dz))) % period
                w = (u[..., 0] if dx else 1-u[..., 0])
                w = w * (u[..., 1] if dy else 1-u[..., 1])
                w = w * (u[..., 2] if dz else 1-u[..., 2])
                noise += values[c[..., 2], c[..., 1], c[..., 0]] * w
    rgba = np.stack((billows, noise, billows, np.ones_like(noise)), axis=-1)
    data = np.rint(np.clip(rgba, 0, 1) * 255).astype(np.uint8)
    target = Path(__file__).resolve().parent.parent / 'CloudShape.bytes'
    target.write_bytes(data.tobytes())
    assert target.stat().st_size == 64**3 * 4
    print(f'{target}: {target.stat().st_size} bytes')


if __name__ == '__main__':
    generate()
