"""Focused, no-network unit tests for the ALPHA-357 decoded-video QA gate."""
import importlib.util
from pathlib import Path
import unittest

source = Path(__file__).with_name("alpha357_verify_rendered_video.py")
spec = importlib.util.spec_from_file_location("alpha357_verifier", source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class QualityGateTests(unittest.TestCase):
    def test_exact_full_frame_partition_for_all_supported_layouts(self):
        for layout in (2, 4, 6):
            with self.subTest(layout=layout):
                outcome = module.geometry_partition(layout)
                self.assertEqual(outcome["gap_px"], 0)
                self.assertEqual(outcome["overlap_px"], 0)
                self.assertEqual(outcome["creator_intrusion_px"], 0)
                self.assertEqual(len(outcome["slots"]), layout)

    def test_old_six_ad_side_gaps_are_rejected(self):
        original = module.GEOMETRY[6]
        try:
            replacement = [slot if slot[0] != "LEFT_PURPLE" else
                           (slot[0], 0, 270, 270, 540, "purple")
                           for slot in original["slots"]]
            module.GEOMETRY[6] = {"slots": replacement, "creator": original["creator"]}
            with self.assertRaisesRegex(ValueError, "gap|overlap"):
                module.geometry_partition(6)
        finally:
            module.GEOMETRY[6] = original

    def test_overlaps_and_out_of_bounds_fail_closed(self):
        original = module.GEOMETRY[2]
        try:
            replacement = list(original["slots"])
            name, x, y, width, height, hue = replacement[1]
            replacement[1] = (name, x - 10, y, width, height, hue)
            module.GEOMETRY[2] = {"slots": replacement, "creator": original["creator"]}
            with self.assertRaisesRegex(ValueError, "overlap"):
                module.geometry_partition(2)
        finally:
            module.GEOMETRY[2] = original

        original = module.GEOMETRY[4]
        try:
            replacement = list(original["slots"])
            name, x, y, width, height, hue = replacement[0]
            replacement[0] = (name, -1, y, width, height, hue)
            module.GEOMETRY[4] = {"slots": replacement, "creator": original["creator"]}
            with self.assertRaisesRegex(ValueError, "Out-of-canvas"):
                module.geometry_partition(4)
        finally:
            module.GEOMETRY[4] = original

    def test_unsupported_layout_rejected(self):
        with self.assertRaises(ValueError):
            module.geometry_partition(3)

    def test_fixture_panel_hues_reject_background(self):
        self.assertTrue(module.matches_hue((10, 66, 138), "blue"))
        self.assertTrue(module.matches_hue((162, 86, 28), "orange"))
        self.assertTrue(module.matches_hue((12, 92, 46), "green"))
        self.assertTrue(module.matches_hue((88, 27, 124), "purple"))
        self.assertFalse(module.matches_hue((13, 28, 42), "purple"))
        self.assertFalse(module.matches_hue((13, 28, 42), "green"))

    def test_all_decoded_sampling_points_are_within_their_slot(self):
        for layout in (2, 4, 6):
            for name, x, y, _ in module.panel_samples(layout):
                slot = next(s for s in module.GEOMETRY[layout]["slots"] if s[0] == name)
                _, sx, sy, width, height, _ = slot
                self.assertTrue(sx <= x < sx + width)
                self.assertTrue(sy <= y < sy + height)


if __name__ == "__main__":
    unittest.main()
