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
    capture = command([PACKAGE, 'capture', '--out', str(screenshot)])
    (EVIDENCE / f'{name}-capture.out').write_text(capture.stdout)
    (EVIDENCE / f'{name}-capture.err').write_text(capture.stderr)
    if 'ScreenCast + PipeWire: 800x600' not in capture.stdout:
        raise RuntimeError('Overlay inspection did not use ScreenCast')
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


def main():
    baseline = subprocess.run(['sh', '/opt/wayland/run.sh'], check=False, timeout=90)
    if baseline.returncode != 0:
        raise RuntimeError('Baseline ScreenCast failed')

    runtime = Path(os.environ['XDG_RUNTIME_DIR'])
    os.environ['DBUS_SESSION_BUS_ADDRESS'] = f'unix:path={runtime / "bus"}'
    os.environ['WAYLAND_DISPLAY'] = next(runtime.glob('wayland-[0-9]')).name
    os.environ['SWAYSOCK'] = str(next(runtime.glob('sway-ipc.*.sock')))
    os.environ['SDL_VIDEODRIVER'] = 'wayland'
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
                window = wait_for(f'{mode} window', lambda: windows())
                if len(window) != 1:
                    raise RuntimeError('Expected one PixelPointer window')
                wait_for(f'{mode} frame', lambda: (EVIDENCE / 'daemon.err').read_text().count('ScreenCast capacidades:') == initial_count + index)
                inspect_overlay(f'daemon-{mode}', expected)
                command(['swaymsg', f'[con_id={window[0]["id"]}]', 'kill'])
                wait_for(f'{mode} window closed', lambda: not windows())
                wait_for(f'{mode} session ended', lambda: f'--- sessão {mode} ---' in (EVIDENCE / 'daemon.out').read_text())
                if daemon.poll() is not None or command([PACKAGE, 'status']).returncode != 0:
                    raise RuntimeError('Daemon stopped after overlay closure')
                results.append({'mode': mode, 'color': expected, 'daemon_running': True})

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
                'quit': True, 'capability_calls': calls, 'lifecycle': lifecycle,
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
    (EVIDENCE / 'daemon-result.json').write_text(json.dumps({'passed': False, 'error': str(error)}))
    raise