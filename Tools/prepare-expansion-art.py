"""Bake approved generated sheets into game assets. Pillow is required.

The user approved splitting, resizing and palette cleanup to the existing 24x32 format.
Source sheets and cell-to-ID mappings remain in Docs/ElementExpansionSources.json.
Never replace the pre-existing 13 species' field frames.
"""
import json
import hashlib
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]

def largest_component(image):
    pixels = list(image.getdata())
    w, h = image.size
    occupied = {i for i, c in enumerate(pixels) if c[3]}
    largest = set()
    while occupied:
        start = occupied.pop()
        group = {start}
        pending = [start]
        while pending:
            i = pending.pop()
            x, y = i % w, i // w
            for n, valid in ((i-1,x>0),(i+1,x<w-1),(i-w,y>0),(i+w,y<h-1)):
                if valid and n in occupied:
                    occupied.remove(n);group.add(n);pending.append(n)
        if len(group) > len(largest): largest = group
    image.putdata([c if i in largest else (0,0,0,0) for i,c in enumerate(pixels)])
    return image

def pixel_frame(source):
    source = source.convert('RGBA')
    source.putalpha(source.getchannel('A').point(lambda a: 255 if a >= 200 else 0))
    bbox = source.getbbox()
    if bbox is None: raise ValueError('Empty source cell')
    source = source.crop(bbox)
    for width, height in ((14,20),(13,19),(12,18),(16,22),(18,24)):
        scale = min(width/source.width,height/source.height)
        small = source.resize((max(1,round(source.width*scale)),max(1,round(source.height*scale))),Image.Resampling.NEAREST)
        alpha = small.getchannel('A')
        small = small.convert('RGB').quantize(colors=12,dither=Image.Dither.NONE).convert('RGBA')
        small.putalpha(alpha)
        small = largest_component(small)
        area = sum(a > 0 for a in small.getchannel('A').getdata())
        if 48 <= area <= 240:
            output=Image.new('RGBA',(24,32))
            output.paste(small,((24-small.width)//2,29-small.height))
            return output
    raise ValueError(f'No readable pixel silhouette ({area} pixels)')

def meta(path, ppu):
    m = path.with_suffix(path.suffix+'.meta')
    if m.exists(): return
    guid=hashlib.md5(('element-expansion:'+str(path.relative_to(ROOT))).encode()).hexdigest()
    m.write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureType: 8
  spriteMode: 1
  spritePixelsToUnits: {ppu}
  alignment: 7
  spritePivot: {{x: 0.5, y: 0}}
  alphaIsTransparency: 1
  nPOTScale: 0
  maxTextureSize: {32 if ppu == 32 else 1024}
  textureCompression: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''')

def main():
    manifest=json.loads((ROOT/'Docs/ElementExpansionSources.json').read_text())
    outputs=[]
    for sheet in manifest['sheets']:
        image=Image.open(ROOT/sheet['path']).convert('RGBA')
        columns,rows=sheet['columns'],sheet['rows']
        for i,id in enumerate(sheet['ids']):
            cell=image.crop((i%columns*image.width//columns,i//columns*image.height//rows,
                             (i%columns+1)*image.width//columns,(i//columns+1)*image.height//rows))
            if 'boxes' in sheet: cell=image.crop(tuple(sheet['boxes'][i]))
            if sheet['kind']=='field':
                result=pixel_frame(cell)
                path=ROOT/'Assets/Art/Final/Field'/f'{id}.png'
                if id.rsplit('_',1)[0] in manifest['preservedFieldIds']:
                    raise ValueError('Refusing to overwrite original field art: '+id)
                ppu=32
            else:
                # Keep proportions; each portrait gets a square transparent presentation canvas.
                bbox=cell.getbbox()
                if bbox is None: raise ValueError('Empty portrait: '+id)
                cell=cell.crop(bbox);cell.thumbnail((480,480),Image.Resampling.LANCZOS)
                # Remove detached neighboring-cell fragments after the reviewed custom crops.
                cell.putalpha(cell.getchannel('A').point(lambda a: a if a >= 20 else 0))
                cell=largest_component(cell)
                cell=cell.crop(cell.getbbox())
                result=Image.new('RGBA',(512,512));result.paste(cell,((512-cell.width)//2,512-cell.height))
                path=ROOT/'Assets/Art/Final/Portraits'/f'{id}.png';ppu=256
            path.parent.mkdir(parents=True,exist_ok=True)
            result.save(path);meta(path,ppu);outputs.append(str(path.relative_to(ROOT)))
    print(f'Prepared {len(outputs)} assets; original field species preserved: {len(manifest["preservedFieldIds"])}')

if __name__=='__main__': main()
