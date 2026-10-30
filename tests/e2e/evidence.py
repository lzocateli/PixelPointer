import json
import struct
import time
from pathlib import Path
from xml.etree import ElementTree


class Timeline:
    def __init__(self, path):
        self.path = Path(path)
        self.started = time.monotonic()

    def emit(self, action, **details):
        with self.path.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps({"seconds": time.monotonic() - self.started,
                                     "action": action, **details}, ensure_ascii=True) + "\n")


class AviWriter:
    def __init__(self, path, width, height, fps):
        self.stream = Path(path).open("xb+")
        self.width, self.height, self.fps = width, height, fps
        self.frames = 0
        self.index = []
        self.stream.write(b"RIFF\0\0\0\0AVI ")
        header = struct.pack("<14I", 1000000 // fps, 0, 0, 0x10, 0, 0, 1,
                             width * height * 3, width, height, 0, 0, 0, 0)
        stream_header = struct.pack("<4s4sIHH8I4h", b"vids", b"MJPG", 0, 0, 0,
                                    0, 1, fps, 0, 0, width * height * 3,
                                    0xFFFFFFFF, 0, 0, 0, width, height)
        bitmap = struct.pack("<IiiHH4sIiiII", 40, width, height, 1, 24, b"MJPG",
                             width * height * 3, 0, 0, 0, 0)
        streams = self.chunk(b"strh", stream_header) + self.chunk(b"strf", bitmap)
        self.stream.write(self.chunk(b"LIST", b"hdrl" + self.chunk(b"avih", header)
                                     + self.chunk(b"LIST", b"strl" + streams)))
        self.movi_offset = self.stream.tell()
        self.stream.write(b"LIST\0\0\0\0movi")

    @staticmethod
    def chunk(kind, payload):
        return kind + struct.pack("<I", len(payload)) + payload + b"\0" * (len(payload) % 2)

    def append(self, jpeg):
        offset = self.stream.tell() - self.movi_offset - 8
        self.index.append(struct.pack("<4sIII", b"00dc", 0x10, offset, len(jpeg)))
        self.stream.write(self.chunk(b"00dc", jpeg))
        self.frames += 1

    def close(self):
        if self.stream.closed:
            return
        end = self.stream.tell()
        self.stream.seek(self.movi_offset + 4)
        self.stream.write(struct.pack("<I", end - self.movi_offset - 8))
        self.stream.seek(end)
        self.stream.write(self.chunk(b"idx1", b"".join(self.index)))
        size = self.stream.tell()
        self.stream.seek(4)
        self.stream.write(struct.pack("<I", size - 8))
        self.stream.seek(48)
        self.stream.write(struct.pack("<I", self.frames))
        self.stream.seek(140)
        self.stream.write(struct.pack("<I", self.frames))
        self.stream.close()


def read_avi(path):
    data = Path(path).read_bytes()
    if data[:4] != b"RIFF" or data[8:12] != b"AVI ":
        raise ValueError("Video nao e AVI")
    if struct.unpack_from("<I", data, 4)[0] + 8 != len(data):
        raise ValueError("Video truncado")
    frames = []
    fps = None

    def walk(start, end):
        nonlocal fps
        while start < end:
            kind, length = struct.unpack_from("<4sI", data, start)
            payload = start + 8
            if payload + length > end:
                raise ValueError("Chunk truncado")
            if kind == b"LIST":
                walk(payload + 4, payload + length)
            elif kind == b"avih":
                fps = 1000000 / struct.unpack_from("<I", data, payload)[0]
            elif kind == b"00dc":
                frames.append(data[payload:payload + length])
            start = payload + length + length % 2

    walk(12, len(data))
    if not frames or not fps:
        raise ValueError("Video vazio")
    return fps, frames


def report(directory, name, elapsed, error=None):
    suite = ElementTree.Element("testsuite", name="pixelpointer-x11", tests="1",
                                failures=str(int(error is not None)), time=f"{elapsed:.3f}")
    case = ElementTree.SubElement(suite, "testcase", name=name, time=f"{elapsed:.3f}")
    if error is not None:
        ElementTree.SubElement(case, "failure", message=error).text = error
    ElementTree.ElementTree(suite).write(Path(directory) / "junit.xml", encoding="utf-8",
                                       xml_declaration=True)
    (Path(directory) / "result.json").write_text(json.dumps({
        "scenario": name, "passed": error is None, "seconds": elapsed, "error": error,
    }, indent=2) + "\n", encoding="utf-8")
