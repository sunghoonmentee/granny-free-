"""Turn a recorded scenario into things a person can look at.

The scenario test writes one JPEG per camera per captured moment, plus a CSV of
where she was. This makes an animated GIF per camera, a contact sheet of the
chase view with the floor and state burned into each tile, and an MP4 when
ffmpeg is on PATH.

    python tools/make_scenario_media.py unity-logs/scenario/20261001-013000
"""

from __future__ import annotations

import csv
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw

TILE_COLUMNS = 4
GIF_MS_PER_FRAME = 400


def load_path(folder: Path) -> dict[str, dict[str, str]]:
    """frame id -> row of the path log, so tiles can be labelled."""
    csv_path = folder / "path.csv"
    if not csv_path.exists():
        return {}

    # utf-8-sig: Unity writes the CSV with a byte order mark, which would
    # otherwise end up glued to the first column name.
    with csv_path.open(encoding="utf-8-sig") as handle:
        return {row["frame"]: row for row in csv.DictReader(handle)}


def frames_for(folder: Path, camera: str) -> list[Path]:
    return sorted(folder.glob(f"*_{camera}.jpg"))


def make_gif(folder: Path, camera: str, width: int) -> Path | None:
    shots = frames_for(folder, camera)
    if not shots:
        return None

    images = []
    for shot in shots:
        image = Image.open(shot).convert("RGB")
        height = round(image.height * width / image.width)
        images.append(image.resize((width, height), Image.LANCZOS))

    out = folder / f"{camera}.gif"
    images[0].save(
        out,
        save_all=True,
        append_images=images[1:],
        duration=GIF_MS_PER_FRAME,
        loop=0,
        optimize=True,
    )
    return out


def make_contact_sheet(folder: Path, camera: str, rows_of: dict, every: int = 2) -> Path | None:
    shots = frames_for(folder, camera)[::every]
    if not shots:
        return None

    tile_width = 420
    sample = Image.open(shots[0])
    tile_height = round(sample.height * tile_width / sample.width)

    columns = min(TILE_COLUMNS, len(shots))
    rows = (len(shots) + columns - 1) // columns

    sheet = Image.new("RGB", (columns * tile_width, rows * tile_height), (12, 14, 15))
    draw = ImageDraw.Draw(sheet)

    for index, shot in enumerate(shots):
        image = Image.open(shot).convert("RGB").resize((tile_width, tile_height), Image.LANCZOS)
        x = (index % columns) * tile_width
        y = (index // columns) * tile_height
        sheet.paste(image, (x, y))

        frame_id = shot.name.split("_")[0]
        row = rows_of.get(frame_id)
        if row:
            # Latin floor names, because PIL's default font has no Korean glyphs
            # and would draw the ones from the CSV as empty boxes.
            floor = {"-1": "B1", "0": "1F", "1": "2F", "2": "Attic"}.get(
                row["floor"], row["floor"])
            label = f"{float(row['t']):4.1f}s  {floor}  {row['state']}"
            draw.rectangle([x, y, x + tile_width, y + 22], fill=(0, 0, 0))
            draw.text((x + 8, y + 6), label, fill=(240, 235, 225))

    out = folder / f"{camera}-contact.png"
    sheet.save(out)
    return out


def make_mp4(folder: Path, camera: str) -> Path | None:
    if not shutil.which("ffmpeg"):
        return None

    out = folder / f"{camera}.mp4"
    result = subprocess.run(
        [
            "ffmpeg", "-y", "-framerate", "5",
            "-pattern_type", "glob", "-i", str(folder / f"*_{camera}.jpg"),
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-vf", "scale=960:-2",
            str(out),
        ],
        capture_output=True,
    )
    return out if result.returncode == 0 else None


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    folder = Path(sys.argv[1])
    if not folder.is_dir():
        print(f"No such folder: {folder}")
        return 1

    rows_of = load_path(folder)
    made: list[Path] = []

    for camera, width in (("chase", 640), ("plan", 560), ("eye", 640)):
        for product in (make_gif(folder, camera, width), make_mp4(folder, camera)):
            if product:
                made.append(product)

    for camera in ("chase", "plan"):
        sheet = make_contact_sheet(folder, camera, rows_of)
        if sheet:
            made.append(sheet)

    for product in made:
        print(f"{product}  ({product.stat().st_size // 1024} KB)")

    return 0 if made else 1


if __name__ == "__main__":
    raise SystemExit(main())
