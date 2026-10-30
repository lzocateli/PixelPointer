import ctypes as native
import time

from PIL import Image


class XImage(native.Structure):
    _fields_ = [("width", native.c_int), ("height", native.c_int),
                ("xoffset", native.c_int), ("format", native.c_int),
                ("data", native.c_void_p), ("byte_order", native.c_int),
                ("bitmap_unit", native.c_int), ("bitmap_bit_order", native.c_int),
                ("bitmap_pad", native.c_int), ("depth", native.c_int),
                ("bytes_per_line", native.c_int), ("bits_per_pixel", native.c_int),
                ("red_mask", native.c_ulong), ("green_mask", native.c_ulong),
                ("blue_mask", native.c_ulong)]


class WindowAttributes(native.Structure):
    _fields_ = [("background_pixmap", native.c_ulong), ("background_pixel", native.c_ulong),
                ("border_pixmap", native.c_ulong), ("border_pixel", native.c_ulong),
                ("bit_gravity", native.c_int), ("win_gravity", native.c_int),
                ("backing_store", native.c_int), ("backing_planes", native.c_ulong),
                ("backing_pixel", native.c_ulong), ("save_under", native.c_int),
                ("event_mask", native.c_long), ("do_not_propagate_mask", native.c_long),
                ("override_redirect", native.c_int), ("colormap", native.c_ulong),
                ("cursor", native.c_ulong)]


class VisualInfo(native.Structure):
    _fields_ = [("visual", native.c_void_p), ("visual_id", native.c_ulong),
                ("screen", native.c_int), ("depth", native.c_int), ("visual_class", native.c_int),
                ("red_mask", native.c_ulong), ("green_mask", native.c_ulong),
                ("blue_mask", native.c_ulong), ("colormap_size", native.c_int),
                ("bits_per_rgb", native.c_int)]


class X11:
    def __init__(self):
        self.lib = native.CDLL("libX11.so.6")
        self.xtest = native.CDLL("libXtst.so.6")
        self.lib.XInitThreads()
        pointer, integer, window = native.c_void_p, native.c_int, native.c_ulong
        signatures = {
            "XOpenDisplay": (pointer, [native.c_char_p]),
            "XDefaultRootWindow": (window, [pointer]),
            "XCreateSimpleWindow": (window, [pointer, window, integer, integer,
                                            native.c_uint, native.c_uint, native.c_uint,
                                            window, window]),
            "XCreateGC": (pointer, [pointer, window, window, pointer]),
            "XSetForeground": (integer, [pointer, pointer, window]),
            "XFillRectangle": (integer, [pointer, window, pointer, integer, integer,
                                         native.c_uint, native.c_uint]),
            "XDrawString": (integer, [pointer, window, pointer, integer, integer,
                                     native.c_char_p, integer]),
            "XStoreName": (integer, [pointer, window, native.c_char_p]),
            "XMapRaised": (integer, [pointer, window]),
            "XChangeWindowAttributes": (integer, [pointer, window, window, native.POINTER(WindowAttributes)]),
            "XRaiseWindow": (integer, [pointer, window]),
            "XSetInputFocus": (integer, [pointer, window, integer, window]),
            "XFlush": (integer, [pointer]),
            "XSync": (integer, [pointer, integer]),
            "XKeysymToKeycode": (native.c_ubyte, [pointer, window]),
            "XStringToKeysym": (window, [native.c_char_p]),
            "XQueryTree": (integer, [pointer, window, native.POINTER(window),
                                     native.POINTER(window), native.POINTER(native.POINTER(window)),
                                     native.POINTER(native.c_uint)]),
            "XFetchName": (integer, [pointer, window, native.POINTER(pointer)]),
            "XFree": (integer, [pointer]),
            "XGetImage": (native.POINTER(XImage), [pointer, window, integer, integer,
                                                  native.c_uint, native.c_uint, window, integer]),
            "XDestroyImage": (integer, [native.POINTER(XImage)]),
            "XQueryPointer": (integer, [pointer, window, native.POINTER(window),
                                       native.POINTER(window), native.POINTER(integer),
                                       native.POINTER(integer), native.POINTER(integer),
                                       native.POINTER(integer), native.POINTER(native.c_uint)]),
            "XInternAtom": (window, [pointer, native.c_char_p, integer]),
            "XGetSelectionOwner": (window, [pointer, window]),
            "XGrabKey": (integer, [pointer, integer, native.c_uint, window, integer, integer, integer]),
            "XCloseDisplay": (integer, [pointer]),
            "XMatchVisualInfo": (integer, [pointer, integer, integer, integer, native.POINTER(VisualInfo)]),
        }
        for name, (result, arguments) in signatures.items():
            function = getattr(self.lib, name)
            function.restype, function.argtypes = result, arguments
        for name, arguments in {
            "XTestFakeKeyEvent": [pointer, native.c_uint, integer, window],
            "XTestFakeButtonEvent": [pointer, native.c_uint, integer, window],
            "XTestFakeMotionEvent": [pointer, integer, integer, integer, window],
        }.items():
            getattr(self.xtest, name).argtypes = arguments
        self.display = self.lib.XOpenDisplay(None)
        if not self.display:
            raise RuntimeError("Servidor X11 indisponivel")
        self.root = self.lib.XDefaultRootWindow(self.display)
        self.gc = self.lib.XCreateGC(self.display, self.root, 0, None)

    def window(self, name, x, y, width, height):
        result = self.lib.XCreateSimpleWindow(self.display, self.root, x, y, width, height,
                                              0, 0, 0x182028)
        self.lib.XStoreName(self.display, result, name.encode())
        attributes = WindowAttributes()
        attributes.override_redirect = 1
        self.lib.XChangeWindowAttributes(self.display, result, 1 << 9, native.byref(attributes))
        self.lib.XMapRaised(self.display, result)
        self.flush()
        return result

    def fill(self, window, color, x, y, width, height):
        self.lib.XSetForeground(self.display, self.gc, color)
        self.lib.XFillRectangle(self.display, window, self.gc, x, y, width, height)

    def text(self, window, text, x, y):
        self.lib.XSetForeground(self.display, self.gc, 0xFFFFFF)
        self.lib.XDrawString(self.display, window, self.gc, x, y, text.encode(), len(text))

    def flush(self):
        self.lib.XSync(self.display, 0)

    def focus(self, window):
        self.lib.XSetInputFocus(self.display, window, 1, 0)
        self.flush()

    def find(self, title, parent=None):
        parent = self.root if parent is None else parent
        name = native.c_void_p()
        if self.lib.XFetchName(self.display, parent, native.byref(name)) and name:
            actual = native.string_at(name).decode(errors="replace")
            self.lib.XFree(name)
            if actual == title:
                return parent
        root, owner = native.c_ulong(), native.c_ulong()
        children, count = native.POINTER(native.c_ulong)(), native.c_uint()
        if self.lib.XQueryTree(self.display, parent, native.byref(root), native.byref(owner),
                              native.byref(children), native.byref(count)):
            descendants = list(children[:count.value])
            if children:
                self.lib.XFree(children)
            for child in descendants:
                found = self.find(title, child)
                if found:
                    return found
        return None

    def key(self, key, pressed):
        code = self.lib.XKeysymToKeycode(self.display, self.lib.XStringToKeysym(key.encode()))
        if not code:
            raise ValueError(f"Tecla desconhecida: {key}")
        self.xtest.XTestFakeKeyEvent(self.display, code, int(pressed), 0)
        self.flush()

    def chord(self, *keys):
        for key in keys:
            self.key(key, True)
        for key in reversed(keys):
            self.key(key, False)

    def move(self, x, y):
        self.xtest.XTestFakeMotionEvent(self.display, 0, x, y, 0)
        self.flush()

    def button(self, button, pressed):
        self.xtest.XTestFakeButtonEvent(self.display, button, int(pressed), 0)
        self.flush()

    def click(self, x, y, button=1):
        self.move(x, y)
        self.button(button, True)
        self.button(button, False)

    def drag(self, x, y, width=160):
        self.move(x, y)
        self.button(1, True)
        for offset in range(0, width + 1, 8):
            self.move(x + offset, y)
            time.sleep(0.012)
        self.button(1, False)

    def pointer(self):
        root, child = native.c_ulong(), native.c_ulong()
        root_x, root_y, local_x, local_y = [native.c_int() for _ in range(4)]
        mask = native.c_uint()
        self.lib.XQueryPointer(self.display, self.root, native.byref(root), native.byref(child),
                               native.byref(root_x), native.byref(root_y), native.byref(local_x),
                               native.byref(local_y), native.byref(mask))
        return root_x.value, root_y.value, mask.value

    def screenshot(self):
        pointer = self.lib.XGetImage(self.display, self.root, 0, 0, 800, 600, 0xFFFFFFFF, 2)
        if not pointer:
            raise RuntimeError("Captura X11 falhou")
        try:
            image = pointer.contents
            if image.bits_per_pixel != 32 or image.byte_order != 0:
                raise RuntimeError("Formato X11 nao suportado")
            data = native.string_at(image.data, image.bytes_per_line * image.height)
            return Image.frombytes("RGB", (800, 600), data, "raw", "BGRX", image.bytes_per_line)
        finally:
            self.lib.XDestroyImage(pointer)

    def compositor(self):
        atom = self.lib.XInternAtom(self.display, b"_NET_WM_CM_S0", 0)
        return bool(self.lib.XGetSelectionOwner(self.display, atom))

    def grab_control(self, key):
        code = self.lib.XKeysymToKeycode(self.display, self.lib.XStringToKeysym(key.encode()))
        for extra in [0, 2, 16, 18]:
            self.lib.XGrabKey(self.display, code, 4 | extra, self.root, 0, 1, 1)
        self.flush()

    def close(self):
        self.lib.XCloseDisplay(self.display)

    def alpha_visual(self):
        visual = VisualInfo()
        if not self.lib.XMatchVisualInfo(self.display, 0, 32, 4, native.byref(visual)):
            raise RuntimeError("Xvfb nao oferece visual TrueColor de 32 bits")
        return str(visual.visual_id)
