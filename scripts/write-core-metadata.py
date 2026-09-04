"""Write deterministic metadata for original shared scripts; preserve any existing Unity GUIDs."""
import hashlib
from pathlib import Path

root=Path(__file__).resolve().parents[1]
for group in ('Core','Solver','Application'):
    for asset in (root/'Assets'/'Scripts'/group).glob('*.cs'):
        meta=Path(str(asset)+'.meta')
        if meta.exists():
            continue
        relative=asset.relative_to(root).as_posix()
        guid=hashlib.sha256(('RubikSim:'+relative).encode()).hexdigest()[:32]
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{fileID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
for folder in sorted((root/'Assets').rglob('*')):
    if not folder.is_dir():
        continue
    meta=Path(str(folder)+'.meta')
    if meta.exists():
        continue
    guid=hashlib.sha256(('RubikSim:'+folder.relative_to(root).as_posix()).encode()).hexdigest()[:32]
    meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
