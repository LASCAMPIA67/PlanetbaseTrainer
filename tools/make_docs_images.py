"""Builds the README screenshots and the social preview from the in-game self-test captures.

Run the self-test at 1920x1080 with PBTRAINER_SELFTEST_LANGS=en,fr (see CONTRIBUTING.md), then:

    python tools/make_docs_images.py <self-test output folder> docs/images

Requires Python 3 and Pillow. Window crops are saved as 256-color PNG files, game views as JPEG files.
"""
import pathlib
import sys

from PIL import Image, ImageDraw, ImageFont

SRC = pathlib.Path(sys.argv[1])
OUT = pathlib.Path(sys.argv[2])

WINDOW = (469, 211, 1451, 871)   # trainer window + a small margin
HUD = (488, 0, 1432, 316)        # active cheats HUD + notifications (menu hidden)
HERO = (0, 0, 1920, 900)         # full width, without the game's bottom bar and its (French) tip
TABS = ["Colonists", "Construction", "Resources", "Base", "Trade", "Threats", "Time", "Progression", "Settings", "search"]


def load(name):
    return Image.open(SRC / f"{name}.png").convert("RGB")


def save_png(img, path):
    img.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(path, optimize=True)


def save_jpg(img, path, quality=86):
    img.save(path, quality=quality, subsampling=0, optimize=True, progressive=True)


def build_language(lang):
    out = OUT / lang
    out.mkdir(parents=True, exist_ok=True)
    save_jpg(load(f"4_game_Favorites_{lang}").crop(HERO).resize((1600, 750), Image.Resampling.LANCZOS), out / "hero.jpg")
    save_jpg(load(f"3_game_hud_{lang}").crop(HUD), out / "hud.jpg", quality=88)
    for tab in TABS:
        save_png(load(f"4_game_{tab}_{lang}").crop(WINDOW), out / f"{tab.lower()}.png")


def font(names, size):
    for name in names:
        path = pathlib.Path("C:/Windows/Fonts") / name
        if path.exists():
            return ImageFont.truetype(str(path), size)
    raise SystemExit(f"none of {names} found")


def build_social_preview():
    """1280x640 card for the repository's social preview (Settings > General > Social preview)."""
    frame = load("4_game_Favorites_en")
    accent = frame.getpixel((1372, 348)) + (255,)  # the "God mode" switch track
    background = (10, 15, 22, 255)
    card = Image.new("RGBA", (1280, 640), background)

    # Right: the trainer window in game, faded into the dark panel on its left edge.
    shot = frame.crop((440, 56, 1480, 896)).resize((792, 640), Image.Resampling.LANCZOS).convert("RGBA")
    fade = Image.new("L", shot.size, 255)
    fade_draw = ImageDraw.Draw(fade)
    for x in range(140):
        fade_draw.line([(x, 0), (x, shot.size[1])], fill=int(255 * (x / 140) ** 1.5))
    card.paste(shot, (1280 - shot.size[0], 0), fade)

    # Left: title and tagline.
    draw = ImageDraw.Draw(card)
    title_font = font(["segoeuib.ttf", "arialbd.ttf"], 72)
    text_font = font(["segoeui.ttf", "arial.ttf"], 27)
    x = 64
    draw.text((x, 250), "Planetbase", font=title_font, fill=accent, anchor="ls")
    draw.text((x, 332), "Trainer", font=title_font, fill=(255, 255, 255, 255), anchor="ls")
    draw.rounded_rectangle((x, 362, x + 64, 368), radius=3, fill=accent)
    lines = ["48 cheats, toggled one by one", "Rebindable hotkeys", "English & French menu", "Free & open source"]
    for i, line in enumerate(lines):
        draw.text((x, 418 + i * 42), line, font=text_font, fill=(214, 222, 232, 255), anchor="ls")
    save_jpg(card.convert("RGB"), OUT / "social-preview.jpg", quality=88)


for language in ("en", "fr"):
    build_language(language)
build_social_preview()

total = 0
for path in sorted(OUT.rglob("*.*")):
    size = path.stat().st_size
    total += size
    with Image.open(path) as img:
        print(f"{path.relative_to(OUT)!s:28} {img.size[0]:5}x{img.size[1]:<5} {size // 1024:5} KB")
print(f"total {total // 1024} KB")
