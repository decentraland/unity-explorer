"""Pixel measurements for the fixed 1920x1080 Backpack layout used by the 28116 audit."""

from PIL import Image, ImageChops, ImageFilter


def catalog_cells(image):
    assert image.size == (1920, 1080), image.size
    icons, labels = [], []
    for index in range(16):
        x = int(1920 * (.50 + .079 * (index % 4)))
        y = int(1080 * (.335 + .144 * (index // 4)))
        pixels = list(image.crop((x-52, y-52, x+52, y+52)).convert("RGB").getdata())
        light = sum(min(p) > 140 and max(p)-min(p) < 45 for p in pixels)
        silhouette = [i // 104 for i,p in enumerate(pixels) if max(p) < 18]
        if light > 100 and len(silhouette) > 25 and max(silhouette)-min(silhouette) > 43:
            icons.append(index + 1)
            continue
        # In the patched player the centered TMP name is dark neutral ink over
        # the rarity background until the real silhouette arrives. Use its
        # center band and keep the raw image to validate capture classifications.
        ink = [i//104 for i,p in enumerate(pixels) if 20 <= i//104 < 84 and
               10 < max(p) < 110 and max(p)-min(p) < 16]
        if len(ink) > 25 and max(ink)-min(ink) >= 4:
            labels.append(index + 1)
    return icons, labels


def emote_icons(image):
    return catalog_cells(image)[0]


def wearable_cards(image):
    assert image.size == (1920, 1080)
    visible = []
    for index in range(16):
        x = int(1920 * (.50 + .079 * (index % 4)))
        y = int(1080 * (.375 + .144 * (index // 4)))
        pixels = list(image.crop((x-48, y-44, x+48, y+44)).convert("RGB").getdata())
        if sum(min(p) > 130 and max(p)-min(p) < 50 for p in pixels) > 900:
            visible.append(index + 1)
    return visible


def glyph_crop(image):
    crop = image.crop((1572, 482, 1636, 518)).convert("L")
    return crop.point(lambda value: 255 if value > 185 else 0)


def clap_detail(image, reference):
    # Compare the actual title glyphs with the original player, allowing one
    # antialiasing pixel around each stroke. Both precision and recall must pass.
    candidate = glyph_crop(image)
    if not candidate.getbbox():
        return False
    near = reference.filter(ImageFilter.MaxFilter(3))
    close = candidate.filter(ImageFilter.MaxFilter(3))
    false_pixels = ImageChops.subtract(candidate, near).histogram()[255]
    missed = ImageChops.subtract(reference, close).histogram()[255]
    return (false_pixels < candidate.histogram()[255] * .12
            and missed < reference.histogram()[255] * .12)
