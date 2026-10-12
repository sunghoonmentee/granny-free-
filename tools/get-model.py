"""Fetches a model from Poly Haven into the project's model folder.

Everything on Poly Haven is CC0, so nothing here needs attribution and none is
claimed; the licence is recorded per model in docs/ASSETS.md all the same,
because "where did this come from" is a question that gets asked later.

Only the 1k jpg maps are taken. The exr and png variants of the same maps are
several times the size and nothing in a house lit this dimly can tell.

    python tools/get-model.py --list cross_pein_hammer pliers     # sizes only
    python tools/get-model.py cross_pein_hammer pliers            # fetch them
"""

import argparse
import hashlib
import json
import os
import sys
import urllib.request

API = "https://api.polyhaven.com/files/"
INFO = "https://api.polyhaven.com/info/"
DEST = os.path.join("GrannyGame", "Assets", "_Project", "Art", "Models")
RESOLUTION = "1k"


def fetch(url):
    # Poly Haven refuses a request with no user agent, which is what urllib
    # sends by default, with a 403 that looks like the asset does not exist.
    request = urllib.request.Request(url, headers={"User-Agent": "granny-free/1.0"})

    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def wanted(slug):
    """The fbx and the jpg maps that go with it, as (filename, url, size, md5)."""
    files = json.loads(fetch(API + slug))

    if "fbx" not in files or RESOLUTION not in files["fbx"]:
        raise SystemExit(f"{slug}: no {RESOLUTION} fbx on Poly Haven")

    entry = files["fbx"][RESOLUTION]["fbx"]
    out = [(slug + ".fbx", entry["url"], entry["size"], entry["md5"])]

    for path, map_entry in entry.get("include", {}).items():
        name = os.path.basename(path)

        # jpg where there is one, png where there is not. A few models ship
        # their maps only as png or exr; the exr is four times the size for
        # detail nothing in a house this dark can show, but a missing colour
        # map means an untextured grey prop, which is worse than a large file.
        if not name.lower().endswith((".jpg", ".png")):
            continue

        out.append((name, map_entry["url"], map_entry["size"], map_entry["md5"]))

    # The fbx only ever lists the colour map as a jpg; everything else it points
    # at is an exr, which Unity will import but which is four times the size for
    # a house this dark. The same maps exist as jpgs one level up, so the shape
    # and the sheen are taken from there instead of being left out.
    #
    # nor_gl, not nor_dx: Unity reads green-up normals.
    for kind in ("nor_gl", "Rough", "AO"):
        variants = files.get(kind, {}).get(RESOLUTION, {})
        if "jpg" not in variants:
            continue

        jpg = variants["jpg"]
        name = os.path.basename(jpg["url"])

        if any(existing == name for existing, _, _, _ in out):
            continue

        out.append((name, jpg["url"], jpg["size"], jpg["md5"]))

    return out


def describe(slug):
    """Real-world size and polycount, which decide whether a model is usable.

    Worth one extra request every time. A chair that turns out to be 2.3 m tall
    is a decision, not a mistake, but it has to be made before it is standing in
    a bedroom; and the only thing worse than finding that out after downloading
    is finding it out after placing thirty of them.

    Poly Haven reports in millimetres, Blender's way up: X, Y, then height.
    """
    try:
        info = json.loads(fetch(INFO + slug))
        x, y, z = (round(value / 1000, 2) for value in info["dimensions"])
        return f"{x} x {y} x {z} m tall, {info['polycount']} tris"
    except Exception:
        return ""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("slugs", nargs="+")
    parser.add_argument("--list", action="store_true", help="report sizes, download nothing")
    args = parser.parse_args()

    total = 0

    for slug in args.slugs:
        items = wanted(slug)
        size = sum(item[2] for item in items)
        total += size
        print(f"{slug:34} {size / 1_048_576:6.2f} MB  ({len(items)} files)  {describe(slug)}")

        if args.list:
            continue

        folder = os.path.join(DEST, slug)
        os.makedirs(folder, exist_ok=True)

        for name, url, expected, md5 in items:
            path = os.path.join(folder, name)
            data = fetch(url)

            got = hashlib.md5(data).hexdigest()
            if got != md5:
                raise SystemExit(f"{name}: checksum {got} != {md5}")

            with open(path, "wb") as handle:
                handle.write(data)

            print(f"    {name:48} {len(data) / 1024:8.1f} KB")

    print(f"{'':34} {total / 1_048_576:6.2f} MB total")


if __name__ == "__main__":
    main()
