#!/usr/bin/env python3
"""ALPHA345 2/4/6 premium concept geometry (read-only, no paid tools).

Do not substitute these DRAFT_CONCEPT coordinates for owner-authorized
inventory, signed creator rights, an Economics quote or a campaign receipt.
"""
import argparse
import copy
import json
from pathlib import Path
import unittest

FRAME=(1920,1080)
EXPECTED_COUNTS=(2,4,6)
EXPECTED_AREAS=(518400,691200,1080000)


def rect(v):
    values=tuple(v.get(s) for s in ("x","y","width","height"))
    if len(values)!=4 or any(type(x) is not int for x in values):
        raise ValueError("All four placement coordinates must be integers")
    x,y,w,h=values
    if x<0 or y<0 or w<=0 or h<=0 or x+w>FRAME[0] or y+h>FRAME[1]:
        raise ValueError("Placement outside the 1920x1080 display frame")
    return values


def intersects(a,b):
    x,y,w,h=a
    u,v,m,n=b
    return x<u+m and u<x+w and y<v+n and v<y+h


def verify(doc):
    if doc.get("schema")!="ALPHA-345-CONCEPT-LAYOUTS/V1" or doc.get("mode")!="OFFLINE_SYNTHETIC_ONLY":
        raise ValueError("Only versioned fictional 16:9 layouts are supported")
    if doc.get("canvas")!={"width":1920,"height":1080,"aspect_ratio":"16:9"}:
        raise ValueError("Unknown display frame")
    auth=doc.get("authority")
    if not isinstance(auth,dict):
        raise ValueError("Commercial source authority missing")
    forbidden=("owner_signed","creator_license_verified","advertiser_assets_verified",
        "inventory_available","reservations_authorized","external_use_authorized")
    if any(auth.get(k) is not False for k in forbidden):
        raise ValueError("This proposal may not claim real creator or commercial authorization")
    if auth.get("lifecycle")!="DRAFT_CONCEPT_NOT_OWNER_APPROVED" or auth.get("quote_status")!="PRICING_UNAVAILABLE":
        raise ValueError("A concept must not become a quoted or approved inventory product")
    if auth.get("real_impressions")!=0 or auth.get("delivery_status")!="NOT_SENT":
        raise ValueError("No impression or delivery is verified")
    variants=doc.get("variants")
    if not isinstance(variants,list) or len(variants)!=3:
        raise ValueError("Exactly three 2/4/6 proposals must be supplied")
    receipt=[]
    for expected_count,expected_area,variant in zip(EXPECTED_COUNTS,EXPECTED_AREAS,variants):
        ads=variant.get("ads")
        if not isinstance(ads,list) or len(ads)!=expected_count:
            raise ValueError("Advertisement count does not match configured variant")
        protected=rect(variant["creator_region"])
        placed=[]
        slotnames=set()
        for ad in ads:
            item=rect(ad)
            name=ad.get("slot_id")
            if not isinstance(name,str) or not name.endswith("_CONCEPT") or name in slotnames:
                raise ValueError("Unapproved or duplicate inventory slot")
            if not isinstance(ad.get("brand"),str) or not 1<=len(ad["brand"])<=45:
                raise ValueError("Missing fictional brand label")
            slotnames.add(name)
            if intersects(item,protected) or any(intersects(item,other) for other in placed):
                raise ValueError("Advertiser rectangles overlap creator or another ad")
            placed.append(item)
        area=sum(w*h for x,y,w,h in placed)
        if area!=expected_area or area+protected[2]*protected[3]!=FRAME[0]*FRAME[1]:
            raise ValueError("Incorrect coverage or unaccounted creator area")
        receipt.append({
            "variant":variant.get("id"),
            "ads":len(placed),
            "advertiser_area_px2":area,
            "ad_coverage_percent":round(area*100/(FRAME[0]*FRAME[1]),3),
            "creator_content_percent":round(100-area*100/(FRAME[0]*FRAME[1]),3),
            "no_overlap":True,
            "creator_protected":True,
            "commercially_approved":False,
            "delivery_status":"NOT_SENT"
        })
    return {
        "schema":"AGM345-OFFLINE-PREMIUM-LAYOUT-VERIFICATION/V1",
        "mode":"OFFLINE_SYNTHETIC_ONLY",
        "source":"DRAFT_CONCEPT_NOT_OWNER_APPROVED",
        "geometry_validation":"PASS",
        "real_impressions":0,
        "delivery_status":"NOT_SENT",
        "variants":receipt
    }


class NegativeGeometryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base=json.loads(Path("assets/alpha-prototypes/advertising-real-estate/"
            "AGM345-PREMIUM-2-4-6-CONCEPT-LAYOUTS.json").read_text())

    def test_valid_three_variants_have_exact_area(self):
        self.assertEqual([25.0,33.333,52.083],
            [v["ad_coverage_percent"] for v in verify(self.base)["variants"]])

    def test_ad_overlaps_creator(self):
        d=copy.deepcopy(self.base)
        d["variants"][0]["ads"][0]["y"]=760
        with self.assertRaises(ValueError):verify(d)

    def test_double_sold_rectangle(self):
        d=copy.deepcopy(self.base)
        d["variants"][2]["ads"][1]["x"]=800
        with self.assertRaises(ValueError):verify(d)

    def test_unsupported_commercial_authorization(self):
        d=copy.deepcopy(self.base)
        d["authority"]["owner_signed"]=True
        with self.assertRaises(ValueError):verify(d)

    def test_wrong_coverage_dimensions(self):
        d=copy.deepcopy(self.base)
        d["variants"][1]["ads"][0]["height"]=200
        with self.assertRaises(ValueError):verify(d)

    def test_no_fake_delivery_claim(self):
        d=copy.deepcopy(self.base)
        d["authority"]["real_impressions"]=1
        with self.assertRaises(ValueError):verify(d)

    def test_wrong_version_or_device(self):
        d=copy.deepcopy(self.base)
        d["canvas"]["width"]=1080
        with self.assertRaises(ValueError):verify(d)


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--manifest",default="assets/alpha-prototypes/advertising-real-estate/"
        "AGM345-PREMIUM-2-4-6-CONCEPT-LAYOUTS.json")
    p.add_argument("--receipt")
    p.add_argument("--selftest",action="store_true")
    a=p.parse_args()
    if a.selftest:
        result=unittest.main(argv=["geometry contract"],exit=False)
        if not result.result.wasSuccessful():
            raise SystemExit(1)
    data=verify(json.loads(Path(a.manifest).read_text()))
    if a.receipt:
        out=Path(a.receipt)
        out.parent.mkdir(parents=True,exist_ok=True)
        out.write_text(json.dumps(data,indent=2)+"\n")
    print("AGM345_PREMIUM_CONCEPT_LAYOUTS_PASS 2=25.000% 4=33.333% 6=52.083%"
          " owner_signed=false delivery=NOT_SENT")


if __name__=="__main__":
    main()
