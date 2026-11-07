#!/usr/bin/env python3
"""Reproducible ALPHA visual-ad layouts from DRAFT conceptual 2/4/6 manifest.

Outputs styled SVG frames for optional FFmpeg rasterization; no generative
credits, third-party images, commercial authorization or real creator feed.
The vector host is a fictional placeholder, not a live person's likeness.
"""
import argparse
import json
from pathlib import Path
from xml.sax.saxutils import escape
from verify_premium_layouts import verify

BRANDS={
    "BLUEWAVE":("#087ad0","#072e72","PURE WATER"),
    "ORCHID ROAST":("#e69a23","#8c3210","SPECIALTY COFFEE"),
    "CITRA POP":("#067bd3","#103d96","FRESH SPARKLE"),
    "GROVE JUICE":("#f99b23","#af4b0a","FRUIT MOMENTS"),
    "FRUITBURST":("#189c54","#05523c","REAL FRUIT"),
    "FROST POP":("#ac37db","#62179f","ICE CREAM"),
    "SKY TECH":("#167be2","#0c3699","EVERYDAY TECH"),
    "CRAFT LAB":("#f5a11c","#a1530c","SMARTER TOOLS"),
    "AURA BEAUTY":("#bb3ad9","#6d129b","STUDIO"),
    "NOVA AUDIO":("#0ab97c","#086f6e","AUDIO"),
    "LUMI CARE":("#11a7dd","#08659b","CARE"),
    "CRUNCHBOX":("#c632e3","#8a10ab","SNACKS")
}


def txt(x,y,value,size=46,color="#fff",weight=800,anchor=None,letter_spacing=0):
    anchor= ' text-anchor="'+anchor+'"' if anchor else ''
    return ('<text x="'+str(x)+'" y="'+str(y)+'" font-family="DejaVu Sans, sans-serif"'
            ' font-size="'+str(size)+'" font-weight="'+str(weight)+'"'
            ' letter-spacing="'+str(letter_spacing)+'" fill="'+color+'"'+anchor+'>'
            +escape(str(value))+'</text>')


def sponsor(ad,idx):
    x,y,w,h=(ad[k] for k in ("x","y","width","height"))
    name=ad["brand"]
    if name not in BRANDS:
        raise ValueError("Unapproved fictional example brand label in layout")
    col1,col2,tagline=BRANDS[name]
    key="b"+str(idx)
    out=[
        '<defs><linearGradient id="'+key+'" x1="0" y1="0" x2="1" y2="1">',
        '<stop offset="0" stop-color="'+col1+'"/>',
        '<stop offset="1" stop-color="'+col2+'"/></linearGradient></defs>',
        f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="url(#{key})"/>',
        f'<rect x="{x+5}" y="{y+5}" width="{w-10}" height="{h-10}"'
        ' rx="14" fill="none" stroke="#f1d18a" stroke-width="5"/>',
        f'<path d="M{x+14} {y+13}H{x+w-14}" stroke="#ffffff" opacity=".35" stroke-width="5"/>'
    ]
    if w>=800:
        fontsize=52 if h>=240 else 45
        out.append(txt(x+43,y+int(h*.49),name,fontsize))
        out.append(txt(x+45,y+int(h*.73),tagline,23,"#edf6fc",600,letter_spacing=1))
        cx=x+w-105
        cy=y+h//2
        out.append(f'<circle cx="{cx}" cy="{cy}" r="64" fill="none" stroke="#ffffff" stroke-width="9" opacity=".85"/>')
        out.append(f'<circle cx="{cx}" cy="{cy}" r="31" fill="none" stroke="#fff1b3" stroke-width="8"/>')
    else:
        words=name.split(" ")
        for n,word in enumerate(words):
            out.append(txt(x+23,y+85+57*n,word,42))
        out.append(txt(x+25,y+220,tagline,23,"#f2ecde",600))
        cx=x+w//2
        cy=y+h-115
        out.append(f'<circle cx="{cx}" cy="{cy}" r="73" stroke="#fff" stroke-width="9" opacity=".7" fill="none"/>')
        out.append(f'<circle cx="{cx}" cy="{cy}" r="32" stroke="#fff1b3" stroke-width="9" fill="none"/>')
    return '\n'.join(out)


def creator_scene(protected):
    x,y,w,h=(protected[k] for k in ("x","y","width","height"))
    cx=x+w//2
    cy=y+h//2
    a=[
        f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="url(#studio)"/>',
        f'<rect x="{x+24}" y="{y+22}" width="{w-48}" height="{h-44}"'
        ' rx="16" fill="none" stroke="#78aebf" stroke-opacity=".65" stroke-width="5"/>',
        # warm studio lights and decorative bokeh
        f'<circle cx="{x+int(w*.2)}" cy="{y+int(h*.20)}" r="70" fill="#ffc56f" opacity=".11"/>',
        f'<circle cx="{x+int(w*.78)}" cy="{y+int(h*.26)}" r="90" fill="#d3e9ff" opacity=".08"/>',
        f'<rect x="{x+int(w*.10)}" y="{y+int(h*.76)}" width="{int(w*.80)}" height="{int(h*.08)}" rx="18" fill="#173b47"/>',
        # simple original vector podcast host—intentionally NOT a real person
        f'<ellipse cx="{cx}" cy="{cy+int(h*.33)}" rx="190" ry="190" fill="#1f444c"/>',
        f'<path d="M{cx-168} {cy+int(h*.28)}Q{cx} {cy-25} {cx+168} {cy+int(h*.28)}'
        f' L{cx+168} {cy+int(h*.43)}H{cx-168}Z" fill="#2d5059"/>',
        f'<ellipse cx="{cx}" cy="{cy-55}" rx="109" ry="139" fill="#201d23"/>',
        f'<ellipse cx="{cx}" cy="{cy-40}" rx="90" ry="115" fill="#d5a788"/>',
        f'<path d="M{cx-90} {cy-85}Q{cx-30} {cy-183} {cx+85} {cy-90}"'
        ' stroke="#1d212b" stroke-width="40" fill="none"/>',
        f'<path d="M{cx-112} {cy-108}Q{cx} {cy-260} {cx+112} {cy-108}"'
        ' fill="none" stroke="#111a27" stroke-width="23"/>',
        f'<rect x="{cx-132}" y="{cy-76}" width="32" height="95" rx="15" fill="#081723"/>',
        f'<rect x="{cx+100}" y="{cy-76}" width="32" height="95" rx="15" fill="#081723"/>',
        f'<path d="M{cx-35} {cy+25}Q{cx} {cy+50} {cx+35} {cy+25}"'
        ' fill="none" stroke="#784640" stroke-width="6"/>',
        # mic with subtle shine
        f'<rect x="{cx+76}" y="{cy+40}" width="63" height="157" rx="31" fill="#0b1929" stroke="#829aa5" stroke-width="7"/>',
        f'<rect x="{cx+97}" y="{cy+100}" width="12" height="79" fill="#456b7c"/>',
        f'<path d="M{cx+106} {cy+197}V{cy+280}M{cx+32} {cy+281}H{cx+180}"'
        ' stroke="#273b4d" stroke-width="17"/>',
        f'<rect x="{x+40}" y="{y+40}" width="{min(w-80,715)}" height="88" rx="16"'
        ' fill="#082037" stroke="#f4be55" stroke-width="3"/>',
        txt(x+70,y+95,"ALPHA PODCAST  |  FICTIONAL CREATOR",31,"#fff2da"),
        f'<rect x="{x+40}" y="{y+h-103}" width="{min(w-80,670)}" height="64" rx="13" fill="#051c35"/>',
        txt(x+59,y+h-62,"CREATOR CONTENT • DEMO ONLY",29,"#e1f0f7",700)
    ]
    return '\n'.join(a)


def render(variant):
    title=variant["title"]
    frame=['<svg xmlns="http://www.w3.org/2000/svg" width="1920" height="1080" viewBox="0 0 1920 1080">',
      '<defs><linearGradient id="studio" x1="0" y1="0" x2="1" y2="1">'
      '<stop stop-color="#182638" offset="0"/><stop stop-color="#103b40" offset=".5"/>'
      '<stop stop-color="#060d20" offset="1"/></linearGradient></defs>',
      '<rect width="1920" height="1080" fill="#071c31"/>',
      creator_scene(variant["creator_region"])
    ]
    for i,ad in enumerate(variant["ads"]):
        frame.append(sponsor(ad,i))
    frame.append('<rect x="0" y="0" width="1920" height="1080" fill="none" stroke="#e3bc72" stroke-width="10"/>')
    # A visible status label outside false commercial and performance claims.
    frame.append('<rect x="530" y="1000" width="840" height="56" rx="14" fill="#041e39" stroke="#edb94d" stroke-width="3"/>')
    frame.append(txt(557,1038,"OFFLINE CONCEPT — NO REAL AD DELIVERY",30,"#f3d18e",700))
    frame.append('</svg>')
    return '\n'.join(frame)


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--manifest",default="assets/alpha-prototypes/advertising-real-estate/"
        "AGM345-PREMIUM-2-4-6-CONCEPT-LAYOUTS.json")
    p.add_argument("--out-dir",required=True)
    args=p.parse_args()
    data=json.loads(Path(args.manifest).read_text())
    verify(data)
    directory=Path(args.out_dir)
    directory.mkdir(parents=True,exist_ok=True)
    paths=[]
    for variant in data["variants"]:
        label=f'agm345-{len(variant["ads"])}-sponsor-concept.svg'
        path=directory/label
        if path.exists():
            raise FileExistsError("Refusing to overwrite existing concept artwork")
        path.write_text(render(variant),encoding="utf-8")
        paths.append(str(path))
    print("AGM345_PREMIUM_VECTOR_CREATIVE_RENDERED files="+str(len(paths))
          +" authorizations=false delivery=NOT_SENT")


if __name__=="__main__":
    main()
