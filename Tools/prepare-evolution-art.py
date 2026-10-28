"""Bake the approved portrait/24x32 companion split from generated stage sheets.

Pillow is required. Source sheets + reviewed split positions remain in Docs/ArtSources/Evolution.
The original base art and the existing 11 restored portraits are preserved.
"""
import importlib.util
import json
import warnings
from pathlib import Path
from PIL import Image

warnings.filterwarnings('ignore',category=DeprecationWarning)
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('pixel_baker',ROOT/'Tools/prepare-expansion-art.py')
pixel=importlib.util.module_from_spec(spec);spec.loader.exec_module(pixel)

def split_row(image):
    # Generated sheets use eight columns and two occupied rows, with transparent space below.
    # Find the actual quiet seam rather than assuming the model hit the requested row height.
    alpha=image.getchannel('A')
    counts=[sum(alpha.crop((0,y,image.width,y+1)).histogram()[200:]) for y in range(image.height)]
    lo,hi=round(image.height*.27),round(image.height*.50)
    seam=min(range(lo,hi),key=lambda y:sum(counts[max(lo,y-8):min(hi,y+9)]))
    return seam

def portrait(cell):
    cell=cell.convert('RGBA')
    cell.putalpha(cell.getchannel('A').point(lambda a:a if a>=30 else 0))
    cell=pixel.largest_component(cell)
    bbox=cell.getbbox()
    if bbox is None:raise ValueError('Empty stage portrait')
    cell=cell.crop(bbox);cell.thumbnail((480,480),Image.Resampling.NEAREST)
    scale=min(480/cell.width,480/cell.height)
    if scale>1:cell=cell.resize((round(cell.width*scale),round(cell.height*scale)),Image.Resampling.NEAREST)
    result=Image.new('RGBA',(512,512))
    result.paste(cell,((512-cell.width)//2,512-cell.height))
    return result

def main():
    plan=json.loads((ROOT/'Docs/EvolutionArtSources.json').read_text())
    entries=[];out=ROOT/'Assets/Resources/Art/Evolution'
    for sheet in plan['sheets']:
        path=ROOT/sheet['path'];image=Image.open(path).convert('RGBA')
        seam=sheet.get('rowSplit') or split_row(image)
        sheet['rowSplit']=seam
        bounds=[0,seam,image.height]
        for j,form in enumerate(sheet['forms']):
            key=form['shadowId']+'_'+form['stage']
            cells=[]
            for n in (0,1):
                index=j*2+n;col=index%8;row=index//8
                cells.append(image.crop((col*image.width//8,bounds[row],(col+1)*image.width//8,bounds[row+1])))
            original=ROOT/'Assets/Art/Final/Portraits'/f'{key}.png'
            result=Image.open(original).convert('RGBA') if original.exists() else portrait(cells[0])
            p=out/'Portraits'/f'{key}.png';p.parent.mkdir(parents=True,exist_ok=True)
            result.save(p);pixel.meta(p,256)
            resources=[]
            for n,cell in enumerate(cells,1):
                f=out/'Field'/f'{key}_{n:02}.png';f.parent.mkdir(parents=True,exist_ok=True)
                pixel.pixel_frame(cell).save(f);pixel.meta(f,32)
                resources.append(str(f.relative_to(ROOT/'Assets/Resources').with_suffix('')))
            entries.append(dict(shadowId=form['shadowId'],stage=form['stage'],formName=form['formName'],
                portrait=str(p.relative_to(ROOT/'Assets/Resources').with_suffix('')),fieldFrames=resources))
    (ROOT/'Docs/EvolutionArtSources.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2)+'\n')
    (ROOT/'Assets/Resources/Data/EvolutionArtCatalog.json').write_text(json.dumps(dict(entries=entries),ensure_ascii=False,indent=2)+'\n')
    print(f'Prepared {len(entries)} evolved portraits and {len(entries)*2} walking frames')

if __name__=='__main__':main()
