import struct
import tempfile
import unittest
from pathlib import Path
from xml.etree import ElementTree

from evidence import AviWriter, read_avi, report


class EvidenceTests(unittest.TestCase):
    def test_roundtrip_and_frame_counts(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "video.avi"
            writer = AviWriter(path, 800, 600, 5)
            writer.append(b"first")
            writer.append(b"last")
            writer.close()
            writer.close()
            fps, frames = read_avi(path)
            self.assertEqual((fps, frames), (5, [b"first", b"last"]))
            self.assertEqual(struct.unpack_from("<I", path.read_bytes(), 48)[0], 2)
            self.assertEqual(struct.unpack_from("<I", path.read_bytes(), 140)[0], 2)
            with self.assertRaises(FileExistsError):
                AviWriter(path, 800, 600, 5)

    def test_empty_and_truncated_video_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "video.avi"
            writer = AviWriter(path, 800, 600, 5)
            writer.close()
            with self.assertRaisesRegex(ValueError, "vazio"):
                read_avi(path)
            path.write_bytes(path.read_bytes()[:-1])
            with self.assertRaisesRegex(ValueError, "truncado"):
                read_avi(path)

    def test_failure_is_preserved_in_both_reports(self):
        with tempfile.TemporaryDirectory() as directory:
            report(directory, "controlled-failure", 1, "daemon interrompido")
            suite = ElementTree.parse(Path(directory) / "junit.xml").getroot()
            self.assertEqual(suite.get("failures"), "1")
            self.assertEqual(suite.find("testcase/failure").get("message"), "daemon interrompido")


if __name__ == "__main__":
    unittest.main()
