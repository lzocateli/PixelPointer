import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import time


EVIDENCE = Path('/evidence')
PACKAGE = '/package/pixelpointer'
APP_ID = 'io.github.lzocateli.PixelPointer'

argparse.ArgumentParser(
    description='Test two IPC overlays on one daemon portal connection. Run only inside the isolated Wayland test container with /package and /evidence; requires sway, D-Bus, PipeWire and portals.'
).parse_args()


def command(arguments, *, check=True):
    return subprocess.run(arguments, capture_output=True, text=True, check=check, timeout=10)


def wait_for(description, predicate, timeout=15):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        result = predicate()
        if result:
            return result
        time.sleep(0.05)
    raise RuntimeError(f'Timeout: {description}')


def windows():
    tree = json.loads(command(['swaymsg', '-t', 'get_tree']).stdout)
    found = []
    pending = [tree]
    while pending:
        node = pending.pop()
        if node.get('app_id') == APP_ID:
            found.append(node)
        pending.extend(node.get('nodes', []))
        pending.extend(node.get('floating_nodes', []))
    return found


def inspect_overlay(name, expected):
    screenshot = EVIDENCE / f'{name}.png'
    capture = command(['grim', '-o', 'HEADLESS-1', str(screenshot)])
    (EVIDENCE / f'{name}-capture.out').write_text(capture.stdout)
    (EVIDENCE / f'{name}-capture.err').write_text(capture.stderr)
    dimensions = command(['magick', str(screenshot), '-format', '%wx%h', 'info:']).stdout
    if dimensions != '800x600':
        raise RuntimeError('Overlay inspection returned unexpected physical dimensions')
    pixel = command(['magick', str(screenshot), '-format', '%[pixel:p{400,300}]', 'info:']).stdout
    if pixel not in (f'srgb({expected})', f'srgba({expected},1)'):
        raise RuntimeError(f'{name}: unexpected frame color')


def unique_name(process_id):
    names = command(['dbus-send', '--session', '--print-reply', '--dest=org.freedesktop.DBus',
                     '/org/freedesktop/DBus', 'org.freedesktop.DBus.ListNames']).stdout
    for name in re.findall(r'string "(:[^"]+)"', names):
        reply = command(['dbus-send', '--session', '--print-reply', '--dest=org.freedesktop.DBus',
                         '/org/freedesktop/DBus', 'org.freedesktop.DBus.GetConnectionUnixProcessID',
                         f'string:{name}'], check=False)
        match = re.search(r'uint32 ([0-9]+)', reply.stdout)
        if match and int(match.group(1)) == process_id:
            return name
    return None


def capability_calls(text, sender):
    found = []
    for block in re.split(r'(?=^method call )', text, flags=re.MULTILINE):
        if not re.match(rf'method call .*sender={re.escape(sender)}\s', block):
            continue
        strings = [json.loads(value) for value in re.findall(r'^\s+string (".*")$', block, re.MULTILINE)]
        if not strings or strings[0] != 'org.freedesktop.portal.ScreenCast':
            continue
        method = re.search(r'member=(GetAll|Get)\b', block).group(1)
        found.append(method if method == 'GetAll' else f'Get:{strings[1]}')
    return found


def check_multi_monitor():
    command(['swaymsg', 'create_output'])
    command(['swaymsg', 'output', 'HEADLESS-2', 'resolution', '800x600'])
    command(['swaymsg', 'output', 'HEADLESS-2', 'position', '800', '0'])
    outputs = wait_for('two active outputs', lambda: [output for output in
                       json.loads(command(['swaymsg', '-t', 'get_outputs']).stdout) if output.get('active')])
    if len(outputs) != 2:
        raise RuntimeError('Expected exactly two active Wayland outputs')
    probe = command([PACKAGE, 'probe'])
    (EVIDENCE / 'multi-monitor-probe.txt').write_text(probe.stdout + probe.stderr)
    screenshot_version = command(['dbus-send', '--session', '--print-reply',
                                 '--dest=org.freedesktop.portal.Desktop', '/org/freedesktop/portal/desktop',
                                 'org.freedesktop.DBus.Properties.Get',
                                 'string:org.freedesktop.portal.Screenshot', 'string:version'], check=False)
    if screenshot_version.returncode == 0 and 'uint32 0' not in screenshot_version.stdout:
        raise RuntimeError('Fixture unexpectedly provides Screenshot')
    target = EVIDENCE / 'multi-monitor.png'
    result = command([PACKAGE, 'capture', '--out', str(target)], check=False)
    (EVIDENCE / 'multi-monitor.out').write_text(result.stdout)
    (EVIDENCE / 'multi-monitor.err').write_text(result.stderr)
    if result.returncode != 1 or target.exists():
        raise RuntimeError('Multi-monitor capture did not fail safely without Screenshot')
    if 'Unhandled exception' in result.stderr or 'ScreenCast:' in result.stderr:
        raise RuntimeError('Multi-monitor capture crashed or used single-monitor ScreenCast')
    if 'Nenhum portal de captura disponível' not in result.stderr:
        raise RuntimeError('Missing controlled Screenshot error')
    (EVIDENCE / 'multi-monitor-result.json').write_text(json.dumps({
        'passed': True, 'active_outputs': len(outputs), 'capture_exit': result.returncode,
        'no_file': not target.exists(), 'controlled_error': True,
    }))


def main():
    baseline = subprocess.run(['sh', '/opt/wayland/run.sh'], check=False, timeout=90)
    if baseline.returncode != 0:
        raise RuntimeError('Baseline ScreenCast failed')

    runtime = Path(os.environ['XDG_RUNTIME_DIR'])
    os.environ['DBUS_SESSION_BUS_ADDRESS'] = f'unix:path={runtime / "bus"}'
    os.environ['WAYLAND_DISPLAY'] = next(runtime.glob('wayland-[0-9]')).name
    os.environ['SWAYSOCK'] = str(next(runtime.glob('sway-ipc.*.sock')))
    os.environ['SDL_VIDEODRIVER'] = 'wayland'
    if os.environ.get('CHECK_MULTI_MONITOR') == '1':
        check_multi_monitor()
        return
    output_scale = float(os.environ.get('OUTPUT_SCALE', '1'))
    if output_scale not in (1, 1.5, 2):
        raise RuntimeError('Unsupported output scale')
    command(['swaymsg', 'output', 'HEADLESS-1', 'scale', str(output_scale)])
    def scaled_output():
        outputs = json.loads(command(['swaymsg', '-t', 'get_outputs']).stdout)
        return next((output for output in outputs if output.get('name') == 'HEADLESS-1'
                     and output.get('active') and abs(output.get('scale', 0) - output_scale) < 0.01), None)
    display = wait_for('requested output scale', scaled_output)
    daemon = None
    monitor = None
    try:
        with (EVIDENCE / 'daemon.out').open('w') as output, (EVIDENCE / 'daemon.err').open('w') as errors, \
                (EVIDENCE / 'capabilities-monitor.log').open('w') as monitored, \
                (EVIDENCE / 'capabilities-monitor.err').open('w') as monitor_errors:
            monitor = subprocess.Popen(['dbus-monitor', '--session',
                                        "type='method_call',interface='org.freedesktop.DBus.Properties',destination='org.freedesktop.portal.Desktop'"],
                                       stdout=monitored, stderr=monitor_errors)
            wait_for('property monitor', lambda: any('became a monitor' in line and f'pid={monitor.pid} ' in line
                     for line in (EVIDENCE / 'dbus.log').read_text().splitlines()))
            daemon = subprocess.Popen([PACKAGE, 'daemon'], stdout=output, stderr=errors)
            original_pid = daemon.pid
            wait_for('daemon IPC', lambda: command([PACKAGE, 'status'], check=False).returncode == 0)
            sender = wait_for('daemon D-Bus identity', lambda: unique_name(daemon.pid))
            initial_count = (EVIDENCE / 'daemon.err').read_text().count('ScreenCast capacidades:')
            samples = [('draw', '#00aacc', '0,170,204'), ('zoom', '#cc5500', '204,85,0')]
            results = []
            for index, (mode, background, expected) in enumerate(samples, start=1):
                command(['swaymsg', 'output', 'HEADLESS-1', 'bg', background, 'solid_color'])
                command([PACKAGE, mode])
                def configured_window():
                    candidates = windows()
                    (EVIDENCE / 'window-geometry.json').write_text(json.dumps({
                        'output': {'scale': display['scale'], 'rect': display['rect']},
                        'windows': [{'rect': candidate.get('rect'), 'fullscreen_mode': candidate.get('fullscreen_mode')}
                                    for candidate in candidates],
                    }))
                    if len(candidates) != 1:
                        return None
                    bounds = candidates[0]['rect']
                    return candidates if candidates[0].get('fullscreen_mode', 0) > 0 and bounds['width'] == display['rect']['width'] and bounds['height'] == display['rect']['height'] else None
                window = wait_for(f'{mode} configured window', configured_window)
                if len(window) != 1:
                    raise RuntimeError('Expected one PixelPointer window')
                rect = window[0]['rect']
                if rect['width'] != display['rect']['width'] or rect['height'] != display['rect']['height']:
                    raise RuntimeError('Overlay does not cover the logical output')
                wait_for(f'{mode} frame', lambda: (EVIDENCE / 'daemon.err').read_text().count('ScreenCast capacidades:') == initial_count + index)
                inspect_overlay(f'daemon-{mode}', expected)
                command(['swaymsg', f'[con_id={window[0]["id"]}]', 'kill'])
                wait_for(f'{mode} window closed', lambda: not windows())
                wait_for(f'{mode} session ended', lambda: f'--- sessão {mode} ---' in (EVIDENCE / 'daemon.out').read_text())
                if daemon.poll() is not None or command([PACKAGE, 'status']).returncode != 0:
                    raise RuntimeError('Daemon stopped after overlay closure')
                session = (EVIDENCE / 'daemon.out').read_text().split(f'--- sessão {mode} ---', 1)[1]
                dimensions = re.search(r'janela\s+\.+.*?(\d+)x(\d+) px, densidade ([0-9.]+)', session)
                if dimensions is None:
                    raise RuntimeError('Missing overlay pixel metrics')
                pixel_width, pixel_height = int(dimensions.group(1)), int(dimensions.group(2))
                density = float(dimensions.group(3))
                if output_scale > 1 and density <= 1:
                    raise RuntimeError('Requested HiDPI scale was not applied to the overlay')
                if abs(pixel_width - rect['width'] * density) > 2 or abs(pixel_height - rect['height'] * density) > 2:
                    raise RuntimeError('Logical and pixel dimensions disagree with density')
                results.append({'mode': mode, 'color': expected, 'daemon_running': True,
                                'logical_width': rect['width'], 'logical_height': rect['height'],
                                'pixel_width': pixel_width, 'pixel_height': pixel_height, 'density': density})

            diagnostic = (EVIDENCE / 'daemon.err').read_text()
            reads = re.findall(r'^ScreenCast capacidades: [0-9]+ ms; consulta (GetAll|Get)$', diagnostic, re.MULTILINE)
            expected_method = 'Get' if os.environ.get('DENY_GETALL') == '1' else 'GetAll'
            if reads != [expected_method, expected_method]:
                raise RuntimeError('Unexpected daemon capability reads')
            if 'Screenshot' in diagnostic or 'Unhandled exception' in diagnostic:
                raise RuntimeError('Daemon capture fallback or unhandled error')
            bus_log = (EVIDENCE / 'dbus.log').read_text()
            daemon_rejections = [line for line in bus_log.splitlines()
                                 if 'Rejected send message' in line
                                 and 'member="GetAll"' in line
                                 and f'pid={daemon.pid} ' in line
                                 and 'comm="/package/pixelpointer daemon' in line]
            expected_rejections = 1 if expected_method == 'Get' else 0
            if len(daemon_rejections) != expected_rejections:
                raise RuntimeError('Unexpected GetAll retry count on daemon connection')
            monitor.terminate()
            monitor.wait(timeout=5)
            calls = capability_calls((EVIDENCE / 'capabilities-monitor.log').read_text(), sender)
            expected_calls = ['GetAll'] + ['Get:version', 'Get:AvailableSourceTypes', 'Get:AvailableCursorModes'] * 2
            if expected_method == 'GetAll':
                expected_calls = ['GetAll', 'GetAll']
            if calls != expected_calls:
                raise RuntimeError('Unexpected capability query sequence on the daemon connection')

            lifecycle = None
            if os.environ.get('CHECK_LIFECYCLE') == '1':
                command(['swaymsg', 'output', 'HEADLESS-1', 'bg', '#00aacc', 'solid_color'])
                command([PACKAGE, 'draw'])
                wait_for('quit with open overlay', lambda: windows())
                inspect_overlay('quit-open-overlay', '0,170,204')
                command([PACKAGE, 'quit'])
                if daemon.wait(timeout=10) != 0:
                    raise RuntimeError('Quit with open overlay failed')
                wait_for('overlay removed by quit', lambda: not windows())
                if command([PACKAGE, 'status'], check=False).returncode != 1:
                    raise RuntimeError('Old daemon still reported as running')
                daemon = subprocess.Popen([PACKAGE, 'daemon'], stdout=output, stderr=errors)
                wait_for('restarted daemon IPC', lambda: command([PACKAGE, 'status'], check=False).returncode == 0)
                restarted_sender = wait_for('restarted D-Bus identity', lambda: unique_name(daemon.pid))
                if restarted_sender == sender:
                    raise RuntimeError('Restart did not create a fresh portal connection')
                command(['swaymsg', 'output', 'HEADLESS-1', 'bg', '#cc5500', 'solid_color'])
                command([PACKAGE, 'zoom'])
                wait_for('restarted overlay', lambda: windows())
                inspect_overlay('restart-overlay', '204,85,0')
                lifecycle = {'quit_open_overlay': True, 'stopped_status': True,
                             'restart_pid': daemon.pid, 'fresh_portal_connection': True,
                             'restart_capture': True}
            command([PACKAGE, 'quit'])
            if daemon.wait(timeout=10) != 0:
                raise RuntimeError('Daemon did not exit cleanly')
            wait_for('final overlay removed', lambda: not windows())
            (EVIDENCE / 'daemon-result.json').write_text(json.dumps({
                'passed': True, 'pid': original_pid, 'queries': reads,
                'rejections': len(daemon_rejections), 'sessions': results,
                'quit': True, 'capability_calls': calls, 'lifecycle': lifecycle, 'output_scale': output_scale,
            }))
    finally:
        if monitor is not None and monitor.poll() is None:
            monitor.terminate()
            monitor.wait(timeout=5)
        if daemon is not None and daemon.poll() is None:
            command([PACKAGE, 'quit'], check=False)
            try:
                daemon.wait(timeout=5)
            except subprocess.TimeoutExpired:
                daemon.kill()
                daemon.wait(timeout=5)


try:
    main()
except Exception as error:
    result_name = 'multi-monitor-result.json' if os.environ.get('CHECK_MULTI_MONITOR') == '1' else 'daemon-result.json'
    (EVIDENCE / result_name).write_text(json.dumps({'passed': False, 'error': str(error)}))
    raise