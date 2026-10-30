import asyncio
import configparser
import json
import os
import shlex
import subprocess
from pathlib import Path

from dbus_fast import Message, MessageType, Variant
from dbus_fast.aio import MessageBus
from dbus_fast.constants import PropertyAccess
from dbus_fast.service import ServiceInterface, dbus_property, method, signal

from x11 import X11


class Watcher(ServiceInterface):
    def __init__(self):
        super().__init__("org.kde.StatusNotifierWatcher")
        self.items = []
        self.sender = None

    @method()
    def RegisterStatusNotifierItem(self, service: "s"):
        if service.startswith("/"):
            service = self.sender + service
        self.items.append(service)
        self.StatusNotifierItemRegistered(service)

    @method()
    def RegisterStatusNotifierHost(self, service: "s"):
        pass

    @dbus_property(access=PropertyAccess.READ)
    def RegisteredStatusNotifierItems(self) -> "as":
        return self.items

    @dbus_property(access=PropertyAccess.READ)
    def IsStatusNotifierHostRegistered(self) -> "b":
        return True

    @dbus_property(access=PropertyAccess.READ)
    def ProtocolVersion(self) -> "i":
        return 0

    @signal()
    def StatusNotifierItemRegistered(self, service: "s") -> "s":
        return service


async def main():
    desktop = X11()
    scene = desktop.window("E2E external focus", 0, 0, 800, 600)
    desktop.focus(scene)
    no_tray = os.environ.get("E2E_NO_TRAY") == "1"
    panel = None if no_tray else desktop.window("E2E StatusNotifier panel", 580, 12, 210, 170)
    bus = await MessageBus().connect()
    watcher = Watcher()

    def sender(message):
        if message.member == "RegisterStatusNotifierItem":
            watcher.sender = message.sender
        if message.interface == "org.freedesktop.DBus" and message.member == "NameOwnerChanged":
            name, _, current = message.body
            if not current:
                watcher.items = [item for item in watcher.items if item.partition("/")[0] != name]

    bus.add_message_handler(sender)
    if not no_tray:
        bus.export("/StatusNotifierWatcher", watcher)
        await bus.request_name("org.kde.StatusNotifierWatcher")
        await bus.call(Message(destination="org.freedesktop.DBus", path="/org/freedesktop/DBus",
                               interface="org.freedesktop.DBus", member="AddMatch", signature="s",
                               body=["type='signal',interface='org.freedesktop.DBus',member='NameOwnerChanged'"]))
    menu = None
    active_item = None
    entries = []
    icon = []
    opened = False
    previous_down = False
    launched = None
    desktop_file = Path(os.environ["XDG_DATA_HOME"], "applications", "io.github.lzocateli.PixelPointer.desktop")
    frame = 0
    while True:
        desktop.fill(scene, 0x182028, 0, 0, 800, 600)
        for column, color in enumerate([0x00AACC, 0xE0A020, 0x8040A0, 0xCCCCCC]):
            desktop.fill(scene, color, 40 + column * 180, 90, 140, 80)
        desktop.fill(scene, 0x20CC80 if frame % 10 < 5 else 0x4050E0, 60, 400, 180, 90)
        desktop.text(scene, "PixelPointer X11 E2E", 30, 40)
        desktop.text(scene, f"frame {frame:06d}", 60, 520)
        if desktop_file.exists():
            desktop.fill(scene, 0x303030, 20, 550, 220, 36)
            desktop.text(scene, "PixelPointer", 35, 574)
        frame += 1
        desktop.flush()
        Path("/tmp/desktop-ready").touch()
        pointer_x, pointer_y, mask = desktop.pointer()
        down = bool(mask & 256)
        if previous_down and not down and 20 <= pointer_x < 240 and 550 <= pointer_y < 586 and desktop_file.exists():
            entry = configparser.ConfigParser(interpolation=None)
            entry.read(desktop_file)
            arguments = shlex.split(entry["Desktop Entry"]["Exec"])
            if arguments != ["/package/pixelpointer", "daemon"] or entry["Desktop Entry"]["Terminal"] != "false":
                raise RuntimeError("Comando desktop fora do contrato da suite")
            if launched is None or launched.poll() is not None:
                launched = subprocess.Popen(arguments, stdin=subprocess.DEVNULL,
                                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if launched is not None:
            Path("/tmp/desktop-launch.json").write_text(json.dumps({"pid": launched.pid, "exit_code": launched.poll()}))
        if no_tray:
            previous_down = down
            await asyncio.sleep(0.06)
            continue
        if not watcher.items:
            menu, active_item, icon, entries, opened = None, None, [], [], False
        if watcher.items and watcher.items[-1] != active_item:
            item = watcher.items[-1]
            active_item = item
            destination, _, object_path = item.partition("/")
            object_path = "/" + object_path if object_path else "/StatusNotifierItem"
            reply = await bus.call(Message(destination=destination, path=object_path,
                                           interface="org.freedesktop.DBus.Properties",
                                           member="GetAll", signature="s",
                                           body=["org.kde.StatusNotifierItem"]))
            if reply.message_type == MessageType.ERROR:
                raise RuntimeError(f"Propriedades SNI: {reply.error_name}")
            values = reply.body[0]
            icon = values["IconPixmap"].value
            menu_path = values["Menu"].value
            menu_intro = await bus.introspect(destination, menu_path)
            menu = bus.get_proxy_object(destination, menu_path, menu_intro).get_interface("com.canonical.dbusmenu")
            _, layout = await menu.call_get_layout(0, -1, [])
            entries = [(child.value[0], child.value[1]["label"].value) for child in layout[2]]
            Path("/tmp/panel.json").write_text(json.dumps({"entries": entries, "icon": bool(icon)}))
        desktop.fill(panel, 0x303030, 0, 0, 210, 170)
        if icon:
            width, height, pixels = min(icon, key=lambda value: abs(value[0] - 32))
            for row in range(min(height, 40)):
                for column in range(min(width, 40)):
                    offset = (row * width + column) * 4
                    alpha, red, green, blue = pixels[offset:offset + 4]
                    if alpha:
                        desktop.fill(panel, (red << 16) | (green << 8) | blue,
                                     8 + column, 4 + row, 1, 1)
            desktop.text(panel, "PixelPointer", 55, 24)
        if opened:
            for index, (_, label) in enumerate(entries):
                desktop.text(panel, label, 12, 65 + index * 36)
        if previous_down and not down and 580 <= pointer_x < 790:
            if 12 <= pointer_y < 55 and menu:
                opened = not opened
            elif opened and 55 <= pointer_y < 170:
                index = (pointer_y - 55) // 36
                if index < len(entries):
                    await menu.call_event(entries[index][0], "clicked", Variant("i", 0), 0)
                    opened = False
        previous_down = down
        desktop.flush()
        await asyncio.sleep(0.06)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except Exception as error:
        Path(os.environ["E2E_OUTPUT"], "desktop-error.json").write_text(json.dumps({
            "type": type(error).__name__, "message": str(error)[:500],
        }))
        raise SystemExit(1)
