from PIL import Image, ImageFilter
import numpy as np
from collections import deque
import os

SRC = [
    ("0_main.png", "main"),
    ("2_sleep.png", "sleep"),
    ("3_play1.png", "play1"),
    ("3_play2.png", "play2"),
    ("3_play3.png", "play3"),
    ("3_play4.png", "play4"),
]
OUT = "assets"
os.makedirs(OUT, exist_ok=True)
os.makedirs("verification/assets", exist_ok=True)

def remove_checkerboard(path):
    im = Image.open(path).convert("RGBA")
    rgb = np.array(im.convert("RGB"), dtype=np.uint8)
    h, w, _ = rgb.shape
    mx = rgb.max(axis=2).astype(np.int16)
    mn = rgb.min(axis=2).astype(np.int16)
    # Checkerboard + near-white neutral pixels. Touching the border decides
    # what is background, so white clothing inside the figure survives.
    candidate = (mn >= 198) & ((mx - mn) <= 18)
    visited = np.zeros((h, w), dtype=bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if candidate[y, x] and not visited[y, x]:
                visited[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if candidate[y, x] and not visited[y, x]:
                visited[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        if y > 0 and candidate[y - 1, x] and not visited[y - 1, x]:
            visited[y - 1, x] = True
            q.append((y - 1, x))
        if y + 1 < h and candidate[y + 1, x] and not visited[y + 1, x]:
            visited[y + 1, x] = True
            q.append((y + 1, x))
        if x > 0 and candidate[y, x - 1] and not visited[y, x - 1]:
            visited[y, x - 1] = True
            q.append((y, x - 1))
        if x + 1 < w and candidate[y, x + 1] and not visited[y, x + 1]:
            visited[y, x + 1] = True
            q.append((y, x + 1))
    alpha = np.where(visited, 0, 255).astype(np.uint8)
    alpha_im = Image.fromarray(alpha, "L")
    # Trim one pixel of the white fringe, then soften the cut.
    alpha_im = alpha_im.filter(ImageFilter.MinFilter(3))
    alpha_im = alpha_im.filter(ImageFilter.GaussianBlur(0.55))
    alpha = np.array(alpha_im)
    rgba = np.dstack([rgb, alpha])
    out = Image.fromarray(rgba, "RGBA")
    bbox = out.getbbox()
    if bbox:
        out = out.crop(bbox)
    return out

for src, key in SRC:
    im = remove_checkerboard(os.path.join("img", src))
    # The main art has one enclosed checkerboard pocket inside the ahoge loop
    # that is not connected to the border. Clear that pocket explicitly.
    if key == "main":
        data = np.array(im)
        region = np.zeros(data.shape[:2], bool)
        region[19:49, 184:254] = True
        rgb = data[:, :, :3].astype(np.int16)
        pocket = region & (data[:, :, 3] > 0) & (rgb.min(axis=2) >= 195) & ((rgb.max(axis=2) - rgb.min(axis=2)) <= 30)
        data[:, :, 3][pocket] = 0
        im = Image.fromarray(data, "RGBA")
    # Keep a common canvas ratio for stable crossfades while staying lightweight.
    im.thumbnail((560, 560), Image.LANCZOS)
    out_path = os.path.join(OUT, f"{key}.webp")
    im.save(out_path, "WEBP", quality=91, method=6, alpha_quality=100)
    im.save(os.path.join(OUT, f"{key}.png"), "PNG", optimize=True)
    # Dark-background proof sheet for visual inspection.
    proof = Image.new("RGBA", im.size, (8, 20, 38, 255))
    proof.alpha_composite(im)
    proof.save(os.path.join("verification/assets", f"{key}.png"))
    print(key, im.size, "webp", os.path.getsize(out_path))
