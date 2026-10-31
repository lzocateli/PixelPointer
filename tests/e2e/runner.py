import argparse
import hashlib
import io
import json
import os
import signal
import subprocess
import threading
import time
from pathlib import Path

from PIL import Image, ImageChops

from evidence import AviWriter, Timeline, read_avi, report
from x11 import X11


class Recorder:
    def __init__(self, path):
        self.path = path
        self.stop_event = threading.Event()
        self.ready = threading.Event()
        self.error = None
        self.last_capture = 0
        self.frame_condition = threading.Condition()
        self.thread = threading.Thread(target=self.record, daemon=True)

    def record(self):
        writer = None
        try:
            display = X11()
            writer = AviWriter(self.path, 800, 600, 5)
            self.started = time.monotonic()
            while not self.stop_event.is_set():
                captured_at = time.monotonic()
                stream = io.BytesIO()
                display.screenshot().save(stream, format="JPEG", quality=85)
                writer.append(stream.getvalue())
                with self.frame_condition:
                    self.last_capture = captured_at
                    self.frame_condition.notify_all()
                self.ready.set()
                self.stop_event.wait(max(0, self.started + writer.frames / 5 - time.monotonic()))
            self.ended = time.monotonic()
        except Exception as error:
            self.error = error
            self.ready.set()
        finally:
            if writer:
                writer.close()

    def start(self):
        self.thread.start()
        if not self.ready.wait(10) or self.error:
            raise RuntimeError("Gravador nao ficou pronto")

    def checkpoint(self):
        action = time.monotonic()
        with self.frame_condition:
            covered = self.frame_condition.wait_for(
                lambda: self.last_capture >= action or self.error, timeout=5)
        if not covered or self.error:
            raise RuntimeError("Frame posterior a acao ausente")
        return action

    def finish(self):
        terminal_action = self.checkpoint()
        self.stop_event.set()
        self.thread.join(10)
        if self.thread.is_alive() or self.error:
            raise RuntimeError("Gravacao interrompida")
        fps, frames = read_avi(self.path)
        for frame in frames:
            with Image.open(io.BytesIO(frame)) as image:
                image.load()
                assert image.size == (800, 600), "Dimensao do video"
        assert abs(len(frames) / fps - (self.ended - self.started)) <= 0.4, "Duracao do video"
        return {"frames": len(frames), "fps": fps, "seconds": len(frames) / fps,
            "final_action_covered": self.last_capture >= terminal_action}


class Scenario:
    def __init__(self, args):
        self.args = args
        self.output = Path(args.output)
        self.output.mkdir(parents=True, exist_ok=False)
        self.timeline = Timeline(self.output / "timeline.jsonl")
        self.processes = []
        self.daemon = None
        self.display = None
        self.deadline = time.monotonic() + 120
        self.recorder = None
        self.signals = []

    def check_deadline(self):
        if time.monotonic() > self.deadline:
            raise TimeoutError("Prazo global de 120 segundos excedido")

    def wait(self, name, condition, timeout=10):
        deadline = min(self.deadline, time.monotonic() + timeout)
        while time.monotonic() < deadline:
            diagnostic = self.output / "desktop-error.json"
            if diagnostic.exists():
                raise RuntimeError(f"Desktop: {diagnostic.read_text()}")
            if self.daemon is not None and self.daemon.poll() is not None:
                raise RuntimeError(f"Daemon interrompido: {self.daemon.returncode}")
            result = condition()
            if result:
                self.timeline.emit("verified", check=name)
                return result
            threading.Event().wait(0.05)
        raise TimeoutError(f"Condicao nao satisfeita: {name}")

    def spawn(self, arguments):
        process = subprocess.Popen(arguments, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.processes.append(process)
        return process

    def cli(self, *arguments, expected=0):
        self.check_deadline()
        result = subprocess.run(["/package/pixelpointer", *arguments], capture_output=True,
                                timeout=10, text=True)
        assert result.returncode == expected, f"CLI {arguments[0]}: codigo {result.returncode}"
        return result.stdout + result.stderr

    def screenshot(self, name):
        image = self.display.screenshot()
        image.save(self.output / f"{name}.png")
        if self.recorder:
            self.recorder.checkpoint()
        self.timeline.emit("screenshot", name=name)
        return image

    def chord(self, *keys):
        self.check_deadline()
        self.timeline.emit("keys", keys=keys)
        self.display.chord(*keys)

    def setup(self):
        os.environ["E2E_OUTPUT"] = str(self.output)
        for name in ["HOME", "XDG_RUNTIME_DIR", "XDG_CONFIG_HOME", "XDG_DATA_HOME"]:
            Path(os.environ[name]).mkdir(parents=True, exist_ok=True)
        Path(os.environ["XDG_RUNTIME_DIR"]).chmod(0o700)
        self.pictures = Path(os.environ["HOME"]) / "Imagens com espa\u00e7os"
        self.config = Path(os.environ["XDG_CONFIG_HOME"]) / "user-dirs.dirs"
        self.config.write_text(f'XDG_PICTURES_DIR="{self.pictures}"\n')
        Path("/tmp/.X11-unix").mkdir(mode=0o1777, exist_ok=True)
        self.spawn(["Xvfb", ":99", "-screen", "0", "800x600x24", "-nolisten", "tcp", "-ac"])
        self.wait("socket X11", lambda: Path("/tmp/.X11-unix/X99").exists())
        self.display = X11()
        if not self.args.no_compositor:
            os.environ["SDL_VIDEO_X11_WINDOW_VISUALID"] = self.display.alpha_visual()
        bus = self.spawn(["dbus-daemon", "--session", "--nofork", "--address=unix:path=/tmp/runtime/bus"])
        os.environ["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=/tmp/runtime/bus"
        self.wait("D-Bus privado", lambda: Path("/tmp/runtime/bus").exists() and bus.poll() is None)
        Path("/tmp/twmrc").write_text('NoTitle\nRandomPlacement\nNoGrabServer\nBorderWidth 0\n')
        self.spawn(["twm", "-f", "/tmp/twmrc"])
        if not self.args.no_compositor:
            self.spawn(["xcompmgr", "-n"])
            self.wait("compositor", self.display.compositor)
        os.environ["E2E_NO_TRAY"] = "1" if self.args.no_tray else "0"
        self.spawn(["python", "desktop.py"])
        self.wait("desktop e painel", lambda: Path("/tmp/desktop-ready").exists())
        self.recorder = Recorder(self.output / "desktop.avi")
        self.recorder.start()
        self.timeline.emit("recorder-ready")
        self.screenshot("00-before")
        for command in ["--help", "--version", "probe"]:
            assert self.cli(command).strip(), f"CLI sem saida: {command}"
        self.cli("capture", "--out", str(self.output / "capture.png"))
        with Image.open(self.output / "capture.png") as image:
            image.load()
            assert image.size == (800, 600)
        self.start_daemon()
        if not self.args.no_tray:
            self.wait("icone SNI", lambda: Path("/tmp/panel.json").exists())
            panel = json.loads(Path("/tmp/panel.json").read_text())
            assert panel["icon"] and [item[1] for item in panel["entries"]] == ["Zoom", "Desenhar", "Sair"]
            # O painel publica panel.json antes de repintar o icone.
            self.wait("icone renderizado", lambda: len(
                self.display.screenshot().crop((588, 16, 628, 56)).getcolors(1601) or []) > 4)
            self.screenshot("01-tray")
        self.cli("daemon", expected=1)
        self.cli("status")

    def start_daemon(self):
        initialized = threading.Event()
        self.hotkeys = {}
        self.daemon = subprocess.Popen(["/package/pixelpointer", "daemon"], stdout=subprocess.PIPE,
                                       stderr=subprocess.STDOUT, text=True)
        self.processes.append(self.daemon)
        daemon = self.daemon

        def diagnostics():
            for line in daemon.stdout:
                for token in ["congelada", "indispon", "registrada", "salvar", "salva", "Atalhos", "ignoradas"]:
                    if token in line:
                        self.signals.append(token)
                if line.startswith("Atalhos "):
                    available, _, conflicts = line.partition("; conflitos:")
                    self.hotkeys = {"zoom": "zoom=" in available, "draw": "draw=" in available,
                                    "conflict_zoom": "zoom" in conflicts}
                if line.startswith("Tray "):
                    self.tray_unavailable = "indispon" in line
                    initialized.set()

        threading.Thread(target=diagnostics, daemon=True).start()
        self.wait("daemon status", lambda: subprocess.run(
            ["/package/pixelpointer", "status"], stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL, timeout=3).returncode == 0)
        self.wait("SDL iniciado", initialized.is_set)

    def stop_daemon(self):
        self.cli("quit")
        self.daemon.wait(timeout=10)
        assert self.daemon.returncode == 0, "Saida do daemon"
        self.daemon = None
        self.wait("janela liberada ao encerrar", lambda: not self.display.find("PixelPointer"))
        self.cli("status", expected=1)

    def overlay(self, mode):
        external = self.display.find("E2E external focus")
        assert external, "Janela de foco externo ausente"
        self.display.focus(external)
        self.wait("cena repintada antes da captura", lambda:
              self.display.screenshot().getpixel((60, 120)) == (0, 170, 204))
        self.chord("Control_L", "1" if mode == "zoom" else "2")
        self.wait(f"atalho global {mode}", lambda: self.display.find("PixelPointer"))
        self.screenshot(f"opened-{mode}-{time.monotonic_ns()}")

    def close_overlay(self, right=False):
        if right:
            self.display.click(400, 300, button=3)
        else:
            self.chord("Escape")
        self.wait("sobreposicao fechada", lambda: not self.display.find("PixelPointer"))
        self.cli("status")

    def save(self):
        folder = self.pictures / "Screenshots"
        previous = set(folder.glob("*.png")) if folder.exists() else set()
        self.chord("Control_L", "s")
        created = self.wait("PNG exportado", lambda: set(folder.glob("*.png")) - previous)
        assert len(created) == 1, "Mais de um PNG por Ctrl+S"
        source = created.pop()
        image = Image.open(source)
        image.load()
        assert image.size == (800, 600), "Dimensao do PNG"
        target = self.output / f"export-{len(list(self.output.glob('export-*.png'))):02d}.png"
        target.write_bytes(source.read_bytes())
        return source, image.convert("RGBA")

    @staticmethod
    def red_count(image, region=(190, 290, 380, 310)):
        return sum(red > 180 and green < 100 and blue < 100 and alpha > 180
                   for red, green, blue, alpha in image.convert("RGBA").crop(region).getdata())

    def tray(self, index):
        self.timeline.emit("tray-click", entry=index)
        self.display.move(600, 30)
        self.display.button(1, True)
        threading.Event().wait(0.15)
        self.display.button(1, False)
        self.wait("menu visivel", lambda: all(
            sum(pixel == (255, 255, 255) for pixel in self.display.screenshot().crop(
                (590, 65 + row * 36, 700, 80 + row * 36)).getdata()) > 20
            for row in range(3)))
        self.screenshot(f"menu-{index}")
        self.display.move(630, 76 + 36 * index)
        self.display.button(1, True)
        threading.Event().wait(0.15)
        self.display.button(1, False)

    def annotations(self):
        self.overlay("draw")
        self.chord("w")
        self.wait("quadro branco", lambda: self.display.screenshot().getpixel((100, 200)) == (255, 255, 255))
        colors = [("r", (255, 0, 0)), ("g", (50, 205, 50)), ("b", (30, 144, 255)),
                  ("y", (255, 255, 0)), ("o", (255, 165, 0)), ("p", (255, 105, 180))]

        def count(image, color, row):
            return sum(all(abs(actual - expected) <= 8 for actual, expected in zip(pixel[:3], color))
                       and pixel[3] == 255 for pixel in image.convert("RGBA").crop(
                           (190, row - 10, 380, row + 10)).getdata())

        for index, (key, color) in enumerate(colors):
            row = 220 + index * 40
            self.chord(key)
            self.display.drag(200, row)
            self.wait(f"cor {key}", lambda: count(self.display.screenshot(), color, row) > 300)
        _, exported = self.save()
        for index, (key, color) in enumerate(colors):
            assert count(exported, color, 220 + index * 40) > 300, f"Cor {key} ausente no PNG"
        self.screenshot("06-colors")
        self.chord("e")
        self.chord("r")
        self.display.drag(200, 250)
        self.wait("traco fino", lambda: self.red_count(self.display.screenshot(), (190, 240, 380, 260)) > 300)
        self.display.key("Control_L", True)
        try:
            for _ in range(5):
                self.display.click(400, 300, button=4)
        finally:
            self.display.key("Control_L", False)
        self.display.drag(200, 350)
        self.wait("traco espesso", lambda: self.red_count(self.display.screenshot(), (190, 330, 380, 370)) > 1500)
        _, exported = self.save()
        thin = self.red_count(exported, (275, 230, 285, 270))
        thick = self.red_count(exported, (275, 330, 285, 370))
        assert thick > thin + 60, "Ctrl+roda nao aumentou espessura em pelo menos 6 pixels"
        self.screenshot("07-widths")
        self.close_overlay()

    def zoom_controls(self):
        self.cli("capture", "--out", str(self.output / "zoom-source.png"))
        self.overlay("zoom")
        self.display.move(0, 0)

        def cyan_width():
            return sum(abs(red) <= 8 and abs(green - 170) <= 8 and abs(blue - 204) <= 8
                       for red, green, blue in self.display.screenshot().crop((0, 300, 800, 301)).getdata())

        try:
            self.wait("zoom inicial 2x", lambda: abs(cyan_width() - 280) <= 4)
        except TimeoutError:
            self.save()
            raise
        self.display.click(0, 0, button=4)
        self.wait("roda amplia para 2.5x", lambda: abs(cyan_width() - 350) <= 4)
        self.chord("Up")
        self.wait("seta amplia para 3.125x", lambda: abs(cyan_width() - 438) <= 4)
        self.chord("Down")
        self.wait("seta reduz para 2.5x", lambda: abs(cyan_width() - 350) <= 4)
        self.display.click(0, 0, button=5)
        self.wait("roda reduz para 2x", lambda: abs(cyan_width() - 280) <= 4)
        self.screenshot("08-zoom-top-left")
        self.display.move(799, 599)
        self.wait("zoom acompanha mouse", lambda: cyan_width() == 0)
        self.screenshot("09-zoom-bottom-right")
        self.close_overlay()

    def settings(self):
        path = Path(os.environ["XDG_CONFIG_HOME"]) / "pixelpointer" / "settings.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        blue = (30, 144, 255)

        def colored(color, region):
            return sum(all(abs(actual - expected) <= 8 for actual, expected in zip(pixel, color))
                       for pixel in self.display.screenshot().crop(region).getdata())

        try:
            path.write_text('{"version": 1, "pen": {"color": "blue", "width": 20}, "zoom": {"initial": 4}, "rendering": {"gpu": true}}\n')
            assert "Configurações .. carregadas" in self.cli("probe"), "probe nao carregou settings.json"
            self.overlay("zoom")
            self.display.move(0, 0)
            # A 4x, a linha 480 da tela corresponde a y=120 da cena, dentro do retangulo ciano.
            self.wait("zoom inicial configurado 4x", lambda: abs(colored((0, 170, 204), (0, 480, 800, 481)) - 560) <= 4)
            self.close_overlay()
            self.overlay("draw")
            self.chord("w")
            self.display.drag(200, 300)
            self.wait("cor e espessura configuradas", lambda: colored(blue, (190, 285, 380, 315)) > 2500)
            self.screenshot("12-settings")
            self.close_overlay()
            invalid = '{"pen": {"width": 999}}\n'
            path.write_text(invalid)
            self.overlay("draw")
            self.chord("w")
            self.display.drag(200, 300)
            self.wait("padroes com arquivo invalido", lambda: "ignoradas" in self.signals
                      and self.red_count(self.display.screenshot()) > 300)
            assert path.read_text() == invalid, "settings.json foi modificado"
            self.close_overlay()
        finally:
            path.unlink(missing_ok=True)

    def save_failure(self):
        self.overlay("draw")
        invalid = Path(os.environ["HOME"]) / "destino-bloqueado"
        invalid.write_text("arquivo, nao diretorio\n")
        original = self.config.read_text()
        previous = set(Path(os.environ["HOME"]).rglob("*.png"))
        try:
            self.config.write_text(f'XDG_PICTURES_DIR="{invalid}"\n')
            self.chord("Control_L", "s")
            self.wait("erro de salvamento visivel", lambda: "salvar" in self.signals and
                      self.display.screenshot().getpixel((20, 20)) == (156, 32, 32))
            assert set(Path(os.environ["HOME"]).rglob("*.png")) == previous, "Destino alternativo inesperado"
            self.screenshot("10-save-error")
        finally:
            self.config.write_text(original)
        self.save()
        self.cli("status")
        self.close_overlay()

    def lifecycle(self):
        blocker = X11()
        try:
            blocker.grab_control("1")
            self.start_daemon()
            assert self.hotkeys == {"zoom": False, "draw": True, "conflict_zoom": True}, "Conflito anunciado como sucesso"
            self.timeline.emit("verified", check="conflito real Ctrl+1", hotkeys=self.hotkeys)
            self.overlay("draw")
            self.stop_daemon()
        finally:
            blocker.close()
        self.start_daemon()
        assert self.hotkeys == {"zoom": True, "draw": True, "conflict_zoom": False}, "Grab nao liberado no reinicio"
        self.overlay("zoom")
        self.close_overlay()
        self.overlay("draw")
        self.stop_daemon()
        self.timeline.emit("verified", check="reinicio e quit com sobreposicao aberta")
        self.cli("install-desktop-file")
        desktop_file = Path(os.environ["XDG_DATA_HOME"], "applications", "io.github.lzocateli.PixelPointer.desktop")
        icon = Path(os.environ["XDG_DATA_HOME"], "icons/hicolor/scalable/apps/io.github.lzocateli.PixelPointer.svg")
        original = desktop_file.read_bytes(), icon.read_bytes()
        self.cli("install-desktop-file")
        assert original == (desktop_file.read_bytes(), icon.read_bytes()), "Instalacao nao idempotente"
        self.wait("lancador desktop visivel", lambda: self.display.screenshot().getpixel((25, 555)) == (48, 48, 48))
        self.screenshot("11-desktop-launcher")
        self.display.move(100, 565)
        self.display.button(1, True)
        threading.Event().wait(0.15)
        self.display.button(1, False)
        self.wait("daemon iniciado sem terminal pelo menu", lambda: Path("/tmp/desktop-launch.json").exists() and
              subprocess.run(["/package/pixelpointer", "status"], stdout=subprocess.DEVNULL,
                     stderr=subprocess.DEVNULL, timeout=3).returncode == 0)
        self.overlay("zoom")
        self.cli("quit")
        self.wait("daemon do menu encerrou", lambda: json.loads(Path("/tmp/desktop-launch.json").read_text())["exit_code"] == 0)
        self.wait("janela do menu liberada", lambda: not self.display.find("PixelPointer"))
        self.cli("status", expected=1)
        self.cli("uninstall-desktop-file")
        assert not desktop_file.exists() and not icon.exists(), "Integracao desktop nao removida"

    def exercise(self):
        self.overlay("zoom")
        if self.args.inject_failure:
            self.timeline.emit("controlled-failure", action_name="kill-daemon")
            self.daemon.kill()
            self.daemon.wait(timeout=5)
            self.wait("daemon deve permanecer vivo", lambda: False)
        self.chord("w")
        self.display.drag(200, 300)
        self.wait("traco vermelho visivel", lambda: self.red_count(self.display.screenshot()) > 300)
        self.screenshot("02-stroke")
        first_path, image = self.save()
        assert self.red_count(image) > 300, "PNG sem traco"
        first_hash = hashlib.sha256(first_path.read_bytes()).hexdigest()
        second_path, _ = self.save()
        assert second_path != first_path and hashlib.sha256(first_path.read_bytes()).hexdigest() == first_hash
        self.chord("Control_L", "z")
        self.wait("desfazer", lambda: self.red_count(self.display.screenshot()) < 50)
        self.display.drag(200, 300)
        self.wait("novo traco", lambda: self.red_count(self.display.screenshot()) > 300)
        self.chord("e")
        self.wait("apagar", lambda: self.red_count(self.display.screenshot()) < 50)
        self.chord("k")
        self.wait("quadro preto", lambda: self.display.screenshot().getpixel((100, 200)) == (0, 0, 0))
        self.screenshot("03-blackboard")
        self.close_overlay()
        self.annotations()
        self.zoom_controls()
        self.settings()
        self.save_failure()
        for caps, num in [(False, False), (True, False), (True, True), (False, True)]:
            if caps:
                self.chord("Caps_Lock")
            if num:
                self.chord("Num_Lock")
            for mode in ["zoom", "draw"]:
                self.overlay(mode)
                self.close_overlay(right=True)
            if num:
                self.chord("Num_Lock")
            if caps:
                self.chord("Caps_Lock")
        self.overlay("draw")
        self.display.drag(200, 300)
        self.wait("traco em desenho", lambda: self.red_count(self.display.screenshot()) > 300)
        if not self.args.no_compositor:
            before = self.screenshot("04-live-before")
            self.wait("fundo animado sob desenho", lambda: ImageChops.difference(
                before.crop((60, 400, 240, 490)), self.display.screenshot().crop((60, 400, 240, 490))).getbbox())
            assert self.red_count(self.screenshot("05-live-after")) > 300
            _, image = self.save()
            assert image.getpixel((100, 200))[3] == 0 and self.red_count(image) > 300, "PNG sem alfa/traco"
        else:
            assert "congelada" in self.signals, "Aviso de captura congelada ausente"
            _, image = self.save()
            assert image.getpixel((100, 200))[3] == 255, "Fallback nao e captura opaca"
        self.close_overlay()
        if not self.args.no_tray:
            for index in [0, 1]:
                self.tray(index)
                self.wait("sobreposicao via menu", lambda: self.display.find("PixelPointer"))
                self.close_overlay()
            self.tray(2)
        else:
            assert self.tray_unavailable, "Limitacao da tray nao foi informada"
            self.cli("quit")
        self.daemon.wait(timeout=10)
        assert self.daemon.returncode == 0, "Saida do daemon"
        self.daemon = None
        self.cli("status", expected=1)
        self.lifecycle()
        self.screenshot("99-after-quit")
        self.timeline.emit("scenario-complete")

    def run(self):
        started = time.monotonic()
        error = None
        try:
            self.setup()
            self.exercise()
        except Exception as exception:
            error = f"{type(exception).__name__}: {exception}"
            self.timeline.emit("failure", message=error)
            if self.display:
                try:
                    self.screenshot("failure")
                except Exception:
                    pass
        finally:
            video = None
            if self.recorder:
                try:
                    video = self.recorder.finish()
                except Exception as exception:
                    error = f"{error or ''} Video: {exception}".strip()
            else:
                error = error or "Video obrigatorio ausente"
            for process in reversed(self.processes):
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=3)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait(timeout=3)
            manifest = {
                "package_sha256": hashlib.sha256(Path("/package/pixelpointer").read_bytes()).hexdigest(),
                "package_files": {
                    str(path.relative_to("/package")): hashlib.sha256(path.read_bytes()).hexdigest()
                    for path in sorted(Path("/package").rglob("*")) if path.is_file()
                },
                "image_id": os.environ.get("E2E_IMAGE_ID", "unknown"),
                "resolution": [800, 600], "renderer": "software", "fps": 5,
                "sdl_visual_id": os.environ.get("SDL_VIDEO_X11_WINDOW_VISUALID"),
                "session": "X11", "desktop": "twm + painel sintetico SNI",
                "compositor": not self.args.no_compositor, "tray": not self.args.no_tray,
                "video": video, "diagnostic_signals": sorted(set(self.signals)),
            }
            (self.output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
            for source, target in [("/opt/native-packages.tsv", "native-packages.tsv"),
                                   ("/opt/python-packages.json", "python-packages.json"),
                                   ("/opt/native-links.txt", "native-links.txt"),
                                   ("/opt/native-notices/components.tsv", "native-components.tsv")]:
                (self.output / target).write_bytes(Path(source).read_bytes())
            report(self.output, "x11-vertical", time.monotonic() - started, error)
        print(json.dumps({"passed": error is None, "error": error}), flush=True)
        return int(error is not None)


def main():
    parser = argparse.ArgumentParser(description="E2E X11 isolado do PixelPointer")
    parser.add_argument("--output", required=True)
    parser.add_argument("--inject-failure", action="store_true")
    parser.add_argument("--no-compositor", action="store_true")
    parser.add_argument("--no-tray", action="store_true")
    args = parser.parse_args()
    def timeout_handler(signum, frame):
        raise TimeoutError("Prazo de seguranca de 150 segundos excedido")

    signal.signal(signal.SIGALRM, timeout_handler)
    signal.alarm(150)
    return Scenario(args).run()


if __name__ == "__main__":
    raise SystemExit(main())
