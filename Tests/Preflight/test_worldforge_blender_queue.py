"""Private file-queue and export packaging contracts. Native Blender is verified separately."""
import importlib.util
import json
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

MODULE = Path(__file__).resolve().parents[2] / "scripts/forge/worldforge_blender.py"
spec = importlib.util.spec_from_file_location("worldforge_blender", MODULE)
intake = importlib.util.module_from_spec(spec)
spec.loader.exec_module(intake)


class BlenderQueueTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.work = Path(self.temp.name)
        self.root = self.work / "private"
        intake.prepare(self.root)

    def test_concurrent_submission_publishes_one_complete_private_copy(self):
        source = self.work / "Modell mit Leerzeichen.blend"
        source.write_bytes(b"queue transport fixture" * 100)
        with ThreadPoolExecutor(max_workers=4) as pool:
            ids = list(pool.map(lambda _: intake.submit(self.root, source), range(8)))
        self.assertEqual(len(set(ids)), 1)
        job = self.root / "queue" / ids[0]
        self.assertEqual(list(job.parent.iterdir()), [job])
        self.assertEqual((job / "source.blend").read_bytes(), source.read_bytes())
        source.write_bytes(b"edited source")
        self.assertNotEqual(intake.submit(self.root, source), ids[0])

    def test_failed_job_can_be_retried_and_damaged_result_is_recovered(self):
        source = self.work / "input.glb"
        source.write_bytes(b"queue transport fixture")
        key = intake.submit(self.root, source)
        queued, failed = (self.root / d / key for d in ("queue", "failed"))
        queued.rename(failed)
        (failed / "error.txt").write_text("test failure status", encoding="utf-8")
        self.assertEqual(intake.status(self.root, key)["state"], "failed")
        intake.retry(self.root, key)
        self.assertTrue((queued / "source.glb").is_file())
        self.assertFalse((queued / "error.txt").exists())
        result = self.root / "results" / key
        result.mkdir()
        (result / "receipt.json").write_text("{}", encoding="utf-8")
        (queued / "source.glb").unlink()
        queued.rmdir()
        self.assertEqual(intake.status(self.root, key)["state"], "damaged")
        self.assertEqual(intake.submit(self.root, source), key)
        self.assertTrue((queued / "source.glb").exists())
        self.assertEqual(len(list((self.root / "damaged").glob("*/previous/receipt.json"))), 1)

    def test_separate_gltf_requires_every_buffer_and_texture_inside_the_result(self):
        gltf = self.work / "model.gltf"
        doc = {"asset": {"version": "2.0"}, "buffers": [{"uri": "model.bin", "byteLength": 14}],
               "images": [{"uri": "textures/stone%20moss.png"}]}
        gltf.write_text(json.dumps(doc))
        (self.work / "model.bin").write_bytes(b"buffer fixture")
        with self.assertRaises(ValueError):
            intake.check_gltf(self.work)
        (self.work / "textures").mkdir()
        (self.work / "textures/stone moss.png").write_bytes(b"image fixture")
        intake.check_gltf(self.work)
        (self.work / "model.bin").write_bytes(b"x")
        with self.assertRaisesRegex(ValueError, "buffer length"):
            intake.check_gltf(self.work)
        (self.work / "model.bin").write_bytes(b"buffer fixture")
        for uri in ("../outside.png", "%2e%2e/other.png", "/etc/hosts", "https://example.org/a"):
            doc["images"][0]["uri"] = uri
            gltf.write_text(json.dumps(doc))
            with self.assertRaises(ValueError):
                intake.check_gltf(self.work)

    def test_invalid_id_never_addresses_another_directory(self):
        with self.assertRaises(ValueError):
            intake.status(self.root, "../results")

    def test_queued_job_selects_its_original_trusted_exporter_after_upgrade(self):
        import hashlib
        archive = self.work / "installed-recipes"
        version = "4.5.15"
        folder = archive / version
        folder.mkdir(parents=True)
        previous = b"# previous export configuration\n"
        recipe_hash = hashlib.sha256(previous).hexdigest()
        trusted = folder / (recipe_hash + ".py")
        trusted.write_bytes(previous)
        source_hash = hashlib.sha256(b"queued source").hexdigest()
        key = hashlib.sha256(f"{intake.SCHEMA}/{version}/{recipe_hash}/.blend/{source_hash}".encode()).hexdigest()
        self.assertEqual(intake.select_recipe(".blend", source_hash, key, archive), (version, trusted))
        trusted.write_bytes(previous + b"# changed")
        with self.assertRaisesRegex(ValueError, "checksum"):
            intake.select_recipe(".blend", source_hash, key, archive)

    def test_repeated_worker_interruptions_quarantine_one_job_and_allow_explicit_retry(self):
        source = self.work / "interrupted.blend"
        source.write_bytes(b"queue transport fixture")
        key = intake.submit(self.root, source)
        queued = self.root / "queue" / key
        # These markers are what survives two worker terminations before publication.
        for marker in (".attempt-1", ".attempt-2"):
            (queued / marker).touch()
        intake.process(self.root, queued)
        self.assertIn("interrupted twice", intake.status(self.root, key)["error"])
        self.assertFalse(queued.exists())
        intake.retry(self.root, key)
        self.assertEqual(sorted(p.name for p in queued.iterdir()), ["source.blend"])
