"""Builds Tekstvask.ico from the macOS app icon. Run on a Mac (uses sips): python3 make_ico.py

The macOS artwork has a 100 px transparent margin on its 1024 px canvas; Windows icons fill the canvas,
so the margin is cropped away first. Small sizes are stored as 32-bit DIBs, 256 px as PNG.
"""
import os, struct, subprocess, tempfile, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "../../../Tekstvask/Assets.xcassets/AppIcon.appiconset/icon_512x512@2x.png")
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]


def decode_rgba(path):
    data = open(path, "rb").read()
    pos, idat = 8, b""
    while pos < len(data):
        n, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        pos += 12 + n
        if kind == b"IHDR":
            w, h, depth, color = struct.unpack(">IIBB", body[:10])
            assert depth == 8 and color == 6, "expected 8-bit RGBA"
        elif kind == b"IDAT":
            idat += body
    raw, stride, rows, prev, i = zlib.decompress(idat), w * 4, [], bytearray(w * 4), 0
    for _ in range(h):
        f, line = raw[i], bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - 4] if x >= 4 else 0
            b, c = prev[x], prev[x - 4] if x >= 4 else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    return w, h, rows


def dib(path):
    w, h, rows = decode_rgba(path)
    # Bottom-up BGRA rows, then an all-zero AND mask (alpha does the masking).
    pixels = b"".join(bytes([r[x + 2], r[x + 1], r[x], r[x + 3]]) for r in reversed(rows) for x in range(0, w * 4, 4))
    mask_stride = ((w + 31) // 32) * 4
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(pixels), 0, 0, 0, 0)
    return header + pixels + bytes(mask_stride * h)


with tempfile.TemporaryDirectory() as tmp:
    cropped = os.path.join(tmp, "cropped.png")
    subprocess.run(["sips", "-c", "840", "840", SOURCE, "--out", cropped], check=True, capture_output=True)
    images = []
    for size in SIZES:
        png = os.path.join(tmp, f"{size}.png")
        subprocess.run(["sips", "-z", str(size), str(size), cropped, "--out", png], check=True, capture_output=True)
        images.append(open(png, "rb").read() if size >= 256 else dib(png))

out = struct.pack("<HHH", 0, 1, len(SIZES))
offset = 6 + 16 * len(SIZES)
for size, image in zip(SIZES, images):
    out += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(image), offset)
    offset += len(image)
open(os.path.join(HERE, "Tekstvask.ico"), "wb").write(out + b"".join(images))
print("wrote Tekstvask.ico")
