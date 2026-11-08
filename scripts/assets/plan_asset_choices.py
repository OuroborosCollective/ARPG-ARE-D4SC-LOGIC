"""Deterministic, offline candidates from an owner-supplied partial Unity asset list.

No Unity account calls, no purchase/download, and no entitlement inference.
"""

import argparse
import json
import os
from pathlib import Path

CATALOG = Path(os.environ.get(
    'ARPG_PRIVATE_ASSET_CATALOG',
    '/var/lib/unityarpg/.local/share/are-arpg/private-asset-catalog.json'))
PROFILES = {
    'ashen-crypt': [
        ('Lite Dungeon Pack - Low Poly 3D Art by Gridness', 'dungeon geometry'),
        ('Skeleton Ghost (FREE)', 'undead enemy'),
        ('Low Poly Fantasy Sword Pack', 'loot weapon'),
        ('Stylized Player Character (Free)', 'hero model'),
    ],
    'ember-rift': [
        ('Stylized Rocks with Magic Rune', 'rune scenery'),
        ('Combat Fantasy Free Pack', 'combat visual candidate'),
        ('Fantasy Monster 3D Model 04 - Game Ready - PixeliusVita', 'boss model'),
    ],
    'mobile-performance': [
        ('Runtime Performance Monitor Lite', 'FPS/frame-time monitoring'),
        ('Lite Dungeon Pack - Low Poly 3D Art by Gridness', 'low-poly environment'),
    ],
}


def plan(profile):
    """Select candidates, never assert ownership, download, or engine compatibility."""
    if profile not in PROFILES:
        raise ValueError('unsupported selection profile')
    if CATALOG.is_symlink() or not CATALOG.is_file():
        raise ValueError('private owner-supplied catalog missing')
    data = json.loads(CATALOG.read_text(encoding='utf-8'))
    if data.get('scope') != 'partial' or not isinstance(data.get('items'), list):
        raise ValueError('expected explicitly partial owner-supplied catalog')
    names = {item['name']: item for item in data['items'] if isinstance(item, dict) and isinstance(item.get('name'), str)}
    candidates = []
    for title, role in PROFILES[profile]:
        item = names.get(title)
        candidates.append({
            'title': title,
            'role': role,
            'publisher_from_excerpt': item.get('publisher') if item else None,
            'version_from_excerpt': item.get('version') if item else None,
            'catalogued_in_owner_excerpt': bool(item),
            'entitlement_verified': False,
            'download_verified': False,
            'unity_2022_compatibility': 'untested',
        })
    return {'schema': 'arpg.asset-choice.v1', 'profile': profile,
            'source': 'owner-provided partial excerpt; not a live Unity entitlement query',
            'downloads_checked': False, 'candidates': candidates}


def main():
    p = argparse.ArgumentParser(description='Offline ARPG Unity asset candidate planner')
    p.add_argument('--catalog', type=Path, default=None)
    p.add_argument('--profile', required=True, choices=sorted(PROFILES))
    args = p.parse_args()
    global CATALOG
    if args.catalog is not None:
        CATALOG = args.catalog
    print(json.dumps(plan(args.profile), sort_keys=True, indent=2, ensure_ascii=False))


if __name__ == '__main__':
    main()
