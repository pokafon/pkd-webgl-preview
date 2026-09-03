#!/usr/bin/env python3
"""Docs/art_manifest.yaml の status: missing 項目に対して仮画像PNGを生成する。

使い方:
    python Tools/generate_placeholder_image.py [--manifest Docs/art_manifest.yaml] [--id ID ...]

画像生成APIが使える場合はそちらを試み、失敗/未設定なら必ずPillowによる
識別可能なプレースホルダー画像（単色背景+ラベル文字+種別+ID+サイズ）を出力する。
どちらの場合も処理を止めず、必ず target_path にPNGを書き出す。

生成した仮画像は Assets/Art/AI_Placeholder/ 以下に置かれる前提。
このスクリプトは Unity への割り当ては行わない（別途シーン/メタファイル編集が必要）。
"""
from __future__ import annotations

import argparse
import hashlib
import os
import sys
import textwrap

try:
    import yaml
except ImportError:
    print("PyYAML が必要です: pip install pyyaml", file=sys.stderr)
    raise

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    print("Pillow が必要です: pip install pillow", file=sys.stderr)
    raise

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

TYPE_COLORS = {
    "background": (46, 58, 89),
    "character": (90, 46, 58),
    "minigame_sprite": (46, 89, 63),
    "event_cg": (89, 70, 46),
    "ui": (60, 60, 70),
    "other": (70, 70, 70),
}


def _color_for(item: dict) -> tuple[int, int, int]:
    base = TYPE_COLORS.get(item.get("type", "other"), TYPE_COLORS["other"])
    # id ごとに少しだけ色味をずらして見分けやすくする
    h = int(hashlib.md5(item["id"].encode("utf-8")).hexdigest(), 16)
    jitter = [(h >> (i * 8)) % 20 - 10 for i in range(3)]
    return tuple(max(0, min(255, c + j)) for c, j in zip(base, jitter))


def _load_font(size: int):
    candidates = [
        "C:/Windows/Fonts/YuGothB.ttc",
        "C:/Windows/Fonts/msgothic.ttc",
        "C:/Windows/Fonts/meiryo.ttc",
        "C:/Windows/Fonts/arial.ttf",
    ]
    for path in candidates:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except Exception:
                continue
    return ImageFont.load_default()


def generate_fallback_image(item: dict) -> Image.Image:
    """Pillowで単色背景+ラベル文字+種別+ID+サイズを描いた識別可能な仮画像を作る。"""
    width = int(item.get("width", 512))
    height = int(item.get("height", 512))
    bg = _color_for(item)
    img = Image.new("RGBA", (width, height), bg + (255,))
    draw = ImageDraw.Draw(img)

    border = max(2, min(width, height) // 100)
    draw.rectangle(
        [border, border, width - border - 1, height - border - 1],
        outline=(255, 255, 255, 180),
        width=border,
    )

    title_font = _load_font(max(14, min(width, height) // 14))
    body_font = _load_font(max(10, min(width, height) // 24))

    lines = [
        "AI_PLACEHOLDER",
        item["id"],
        f"type: {item.get('type', '?')}",
        f"{width} x {height}",
        f"scene: {item.get('scene', '?')}",
    ]
    obj = item.get("object")
    if obj:
        lines.extend(textwrap.wrap(f"object: {obj}", width=max(10, width // 12)))

    y = height * 0.08
    for i, line in enumerate(lines):
        font = title_font if i == 0 else body_font
        try:
            bbox = draw.textbbox((0, 0), line, font=font)
            tw = bbox[2] - bbox[0]
            th = bbox[3] - bbox[1]
        except Exception:
            tw, th = draw.textsize(line, font=font)
        x = max(border + 4, (width - tw) / 2)
        draw.text((x, y), line, font=font, fill=(255, 255, 255, 255))
        y += th * 1.5
        if y > height - border - 20:
            break

    # 斜めの警告ストライプで「仮素材」であることを視覚的に強調
    stripe = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(stripe)
    stripe_h = max(20, height // 18)
    sdraw.rectangle([0, height - stripe_h, width, height], fill=(0, 0, 0, 140))
    warn_font = _load_font(max(10, stripe_h // 2))
    warn_text = "TEMP ART - DO NOT SHIP"
    try:
        bbox = sdraw.textbbox((0, 0), warn_text, font=warn_font)
        tw = bbox[2] - bbox[0]
    except Exception:
        tw, _ = sdraw.textsize(warn_text, font=warn_font)
    sdraw.text(
        (max(4, (width - tw) / 2), height - stripe_h + stripe_h * 0.15),
        warn_text,
        font=warn_font,
        fill=(255, 210, 90, 255),
    )
    img = Image.alpha_composite(img, stripe)
    return img


def generate_via_api(item: dict) -> "Image.Image | None":
    """画像生成APIが使えるなら使う。使えなければ None を返して呼び出し側にフォールバックさせる。

    現状このプロジェクトには画像生成APIキーが設定されていないため、
    環境変数 PKD_IMAGE_GEN_API_KEY が無ければ即座に None を返す。
    将来 API を導入する場合はここに実装を追加する。
    """
    api_key = os.environ.get("PKD_IMAGE_GEN_API_KEY")
    if not api_key:
        return None
    try:
        # NOTE: 実プロジェクトの画像生成API仕様に合わせて実装すること。
        # ここではキーが設定されていても未実装のため None を返す（処理は止めない）。
        print(f"  [info] PKD_IMAGE_GEN_API_KEY が設定されていますが、API連携は未実装のためフォールバックします: {item['id']}")
        return None
    except Exception as exc:  # 何が起きても処理は止めない
        print(f"  [warn] 画像生成API呼び出しに失敗、フォールバックします ({item['id']}): {exc}")
        return None


def process_item(item: dict, repo_root: str) -> str:
    target_rel = item["target_path"]
    target_abs = os.path.join(repo_root, target_rel)
    os.makedirs(os.path.dirname(target_abs), exist_ok=True)

    img = generate_via_api(item)
    source = "api"
    if img is None:
        img = generate_fallback_image(item)
        source = "fallback"

    img.save(target_abs)
    return source


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--manifest",
        default=os.path.join("Docs", "art_manifest.yaml"),
        help="manifest YAMLのパス（リポジトリルートからの相対 or 絶対パス）",
    )
    parser.add_argument(
        "--id",
        action="append",
        dest="ids",
        default=None,
        help="このIDのみ処理する（複数指定可）。省略時は status:missing 全件。",
    )
    args = parser.parse_args()

    manifest_path = args.manifest
    if not os.path.isabs(manifest_path):
        manifest_path = os.path.join(REPO_ROOT, manifest_path)

    with open(manifest_path, encoding="utf-8") as f:
        items = yaml.safe_load(f) or []

    targets = [i for i in items if i.get("status") == "missing"]
    if args.ids:
        wanted = set(args.ids)
        targets = [i for i in targets if i["id"] in wanted]

    if not targets:
        print("status: missing の項目はありません。何もしません。")
        return 0

    print(f"{len(targets)} 件の仮画像を生成します。")
    for item in targets:
        try:
            source = process_item(item, REPO_ROOT)
            print(f"  [ok] {item['id']} -> {item['target_path']} ({source})")
        except Exception as exc:
            # 1件失敗しても他の処理は止めない
            print(f"  [error] {item['id']} の生成に失敗しました: {exc}", file=sys.stderr)

    print("完了。manifest の status 更新は手動、または別ツールで行ってください。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
