import zlib
import struct
import math
import os

def write_png(filename, width, height, rgba_data):
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    
    header = b'\x89PNG\r\n\x1a\n'
    ihdr = chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
    raw_rows = []
    for y in range(height):
        raw_rows.append(b'\x00' + rgba_data[y * width * 4 : (y + 1) * width * 4])
    idat = chunk(b'IDAT', zlib.compress(b''.join(raw_rows), 9))
    iend = chunk(b'IEND', b'')
    with open(filename, 'wb') as f:
        f.write(header + ihdr + idat + iend)

def clamp01(v):
    return max(0.0, min(1.0, v))

def generate_needle_spark(filename, size=256):
    data = bytearray(size * size * 4)
    cx = size / 2.0
    cy = size / 2.0
    half = size / 2.0

    for y in range(size):
        for x in range(size):
            nx = (x - cx) / half
            ny = (y - cy) / half
            dist = math.sqrt(nx * nx + ny * ny)

            # 縦長ニードル + 横フレア + 高輝度コア
            needle_v = math.exp(-((abs(nx) * 5.0) ** 1.3 + (abs(ny) * 1.2) ** 1.3) * 2.0)
            needle_h = math.exp(-((abs(nx) * 1.5) ** 1.3 + (abs(ny) * 6.0) ** 1.3) * 3.0) * 0.4
            core = math.exp(-(dist * 5.5) ** 2) * 1.5

            intensity = (needle_v + needle_h + core) * max(0.0, 1.0 - dist)
            val = clamp01(intensity)

            # コアは超高純度ホワイト、エッジはほのかなシアン
            r = int(clamp01(val * 1.1) * 255)
            g = int(clamp01(val * 1.15) * 255)
            b = int(clamp01(val * 1.25) * 255)
            a = int(clamp01(val) * 255)

            idx = (y * size + x) * 4
            data[idx + 0] = r
            data[idx + 1] = g
            data[idx + 2] = b
            data[idx + 3] = a

    write_png(filename, size, size, bytes(data))

def generate_plasma_puff(filename, size=256):
    data = bytearray(size * size * 4)
    cx = size / 2.0
    cy = size / 2.0
    half = size / 2.0

    for y in range(size):
        for x in range(size):
            nx = (x - cx) / half
            ny = (y - cy) / half
            dist = math.sqrt(nx * nx + ny * ny)

            # 指数減衰 + スムーズステップ境界フェード
            fade = clamp01(1.0 - dist)
            fade = fade * fade * (3.0 - 2.0 * fade) # smoothstep
            glow = math.exp(-dist * 2.8) * fade
            core = math.exp(-dist * 7.0) * 0.8 * fade
            val = clamp01(glow + core)

            r = int(clamp01(val * 1.05) * 255)
            g = int(clamp01(val * 1.12) * 255)
            b = int(clamp01(val * 1.22) * 255)
            a = int(clamp01(val) * 255)

            idx = (y * size + x) * 4
            data[idx + 0] = r
            data[idx + 1] = g
            data[idx + 2] = b
            data[idx + 3] = a

    write_png(filename, size, size, bytes(data))

def generate_arc_fork(filename, size=256):
    data = bytearray(size * size * 4)
    cx = size / 2.0
    cy = size / 2.0
    half = size / 2.0

    for y in range(size):
        for x in range(size):
            nx = (x - cx) / half
            ny = (y - cy) / half
            dist = math.sqrt(nx * nx + ny * ny)
            angle = math.atan2(ny, nx)

            # 4本主光線 + 4本細副光線
            rays_main = (math.cos(angle * 4.0) * 0.5 + 0.5) ** 12.0
            rays_sub = (math.cos(angle * 4.0 + math.pi * 0.25) * 0.5 + 0.5) ** 20.0 * 0.5
            spikes = (rays_main + rays_sub) * math.exp(-dist * 2.2)

            core = math.exp(-dist * 5.0) * 1.4
            fade = clamp01(1.0 - dist)
            val = clamp01((spikes + core) * fade)

            r = int(clamp01(val * 1.1) * 255)
            g = int(clamp01(val * 1.15) * 255)
            b = int(clamp01(val * 1.3) * 255)
            a = int(clamp01(val) * 255)

            idx = (y * size + x) * 4
            data[idx + 0] = r
            data[idx + 1] = g
            data[idx + 2] = b
            data[idx + 3] = a

    write_png(filename, size, size, bytes(data))

def generate_lightning_trail(filename, width=256, height=64):
    data = bytearray(width * height * 4)
    cy = height / 2.0
    half_h = height / 2.0

    for y in range(height):
        ny = (y - cy) / half_h
        dist_y = abs(ny)
        edge_fade = clamp01(1.0 - dist_y)
        core = math.exp(-(dist_y * 4.2) ** 2) * 1.6
        glow = math.exp(-dist_y * 2.2) * 0.7 * edge_fade

        for x in range(width):
            nx = x / float(width)
            # 稲妻らしいセグメント波形モジュレーション
            wave = 0.82 + 0.18 * math.sin(nx * 16.0 * math.pi) * math.cos(nx * 8.0 * math.pi)
            val = clamp01((core + glow) * wave * edge_fade)

            r = int(clamp01(val * 1.1) * 255)
            g = int(clamp01(val * 1.15) * 255)
            b = int(clamp01(val * 1.3) * 255)
            a = int(clamp01(val) * 255)

            idx = (y * width + x) * 4
            data[idx + 0] = r
            data[idx + 1] = g
            data[idx + 2] = b
            data[idx + 3] = a

    write_png(filename, width, height, bytes(data))

def generate_micro_crackle(filename, size=128):
    data = bytearray(size * size * 4)
    cx = size / 2.0
    cy = size / 2.0
    half = size / 2.0

    for y in range(size):
        for x in range(size):
            nx = (x - cx) / half
            ny = (y - cy) / half
            dist = math.sqrt(nx * nx + ny * ny)
            angle = math.atan2(ny, nx)

            # 5方向ジグザグ微小アーク
            forks = (math.cos(angle * 5.0 + math.sin(dist * 12.0) * 1.2) * 0.5 + 0.5) ** 14.0
            sparks = forks * math.exp(-dist * 2.8)
            core = math.exp(-dist * 6.5) * 1.5
            fade = clamp01(1.0 - dist)
            val = clamp01((sparks + core) * fade)

            r = int(clamp01(val * 1.1) * 255)
            g = int(clamp01(val * 1.15) * 255)
            b = int(clamp01(val * 1.3) * 255)
            a = int(clamp01(val) * 255)

            idx = (y * size + x) * 4
            data[idx + 0] = r
            data[idx + 1] = g
            data[idx + 2] = b
            data[idx + 3] = a

    write_png(filename, size, size, bytes(data))

if __name__ == '__main__':
    out_dir = r"Assets/Deverop/AT_Murayama/CharacterElectricity/Textures"
    os.makedirs(out_dir, exist_ok=True)
    generate_needle_spark(os.path.join(out_dir, "T_Electricity_NeedleSpark.png"))
    generate_plasma_puff(os.path.join(out_dir, "T_Electricity_PlasmaPuff.png"))
    generate_arc_fork(os.path.join(out_dir, "T_Electricity_ArcFork.png"))
    generate_lightning_trail(os.path.join(out_dir, "T_Electricity_LightningTrail.png"))
    generate_micro_crackle(os.path.join(out_dir, "T_Electricity_MicroCrackle.png"))
    print("All 5 textures successfully generated!")
