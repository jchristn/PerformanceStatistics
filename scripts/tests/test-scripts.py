"""Exercise the agent install/uninstall/reinstall scripts in scripts/agent/{linux,macos,wsl,wsl2,windows}
and the root run-tui.sh / run-docker.sh.

Run with: python3 scripts/tests/test-scripts.py

The Unix scripts run in a temporary copy of the repository against fake tools (dotnet, uname, sudo,
systemctl, launchctl, pkill, ps, docker, cmd.exe, wslpath, wscript.exe, curl) that log their
arguments. TASKMGR_AGENT_TEST_ROOT puts every system path (/opt, /etc, /Library, /usr/local,
/var/log) under a temporary directory, so nothing real is installed. The WSL scripts run with
WSL_SKIP_CHECK=1. The Windows .bat files cannot run here; they are checked statically (labels,
line endings, strict exit codes, the same options as the Unix scripts).
"""
import json
import os
import re
import shutil
import stat
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
UNIX = ['linux', 'macos', 'wsl', 'wsl2']
ACTIONS = ['install', 'reinstall', 'uninstall']

FAKE = '''#!/usr/bin/env python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
if name == 'uname':
    if args == ['-s']: print(os.environ['MOCK_UNAME'])
    elif args == ['-m']: print(os.environ.get('MOCK_ARCH', 'x86_64'))
    else: print('5.15.0-microsoft-standard-WSL2' if os.environ.get('MOCK_WSL2') else '4.4.0-19041-Microsoft')
    sys.exit(0)
if name == 'ps':
    if args[:2] == ['-p', '1']:
        print(os.environ.get('MOCK_PID1', 'init')); sys.exit(0)
    os.execv('/bin/ps', ['ps', *args])
if name == 'sysctl':
    print(os.environ.get('MOCK_ARM64', '0')); sys.exit(0)
if name == 'cmd.exe':
    if os.environ.get('MOCK_NO_APPDATA'): sys.exit(0)
    print('C:\\\\Users\\\\test\\\\AppData\\\\Roaming\\r'); sys.exit(0)
if name == 'wslpath':
    print(os.environ['MOCK_APPDATA'] if args[0] == '-u' else 'C:\\\\fake\\\\' + pathlib.Path(args[1]).name); sys.exit(0)
if name == 'ip':
    print('default via 172.20.0.1 dev eth0'); sys.exit(0)
with open(os.environ['CALL_LOG'], 'a') as f:
    f.write(json.dumps([name, *args]) + '\\n')
if name == 'sudo':
    if args[0] == 'chown':
        sys.exit(0)  # the tests do not run as root
    os.execvp(args[0], args)
if name == 'curl':
    sys.exit(7)
if name == 'launchctl':
    state = pathlib.Path(os.environ['MOCK_LAUNCHD'])
    if args[:1] == ['bootstrap']: state.write_text('loaded')
    if args[:1] == ['bootout'] and not os.environ.get('MOCK_BOOTOUT_STUCK'): state.unlink(missing_ok=True)
    if args[:1] == ['print']: sys.exit(0 if state.exists() else 113)
    sys.exit(0)
if name == 'pgrep':
    sys.exit(0 if os.environ.get('MOCK_RUNNING') else 1)
if name == 'pkill':
    sys.exit(1)
if name == 'docker':
    sys.exit(0)
if name == 'dotnet':
    if args and args[0].endswith('taskmgr.dll') and os.environ.get('MOCK_ENV_OUT'):
        pathlib.Path(os.environ['MOCK_ENV_OUT']).write_text(json.dumps({'PATH': os.environ['PATH'], 'BROWSER': os.environ.get('BROWSER')}))
    if args == ['--list-sdks']:
        print(os.environ.get('MOCK_SDKS', '8.0.425 [mock]\\n10.0.401 [mock]')); sys.exit(0)
    if args[:1] == ['publish']:
        if os.environ.get('MOCK_BUILD_FAIL'): sys.exit(1)
        out = pathlib.Path(args[args.index('-o') + 1]); out.mkdir(parents=True, exist_ok=True)
        (out / 'taskmgr-agent').write_text('agent'); (out / 'taskmgr-agent.pdb').write_text('pdb')
    sys.exit(0)
'''

count = 0


def check(cond, *info):
    global count
    assert cond, info
    count += 1


# --- Static checks -----------------------------------------------------------------------------
agent = root / 'scripts/agent'
for p in UNIX:
    check(sorted(f.stem for f in (agent / p).glob('*.sh') if not f.name.startswith('_')) == sorted(ACTIONS), p)
    extra = sorted(f.name for f in (agent / p).iterdir() if f.stem not in ACTIONS and f.name != '_wsl-common.sh')
    check(not extra, p, extra)
check(sorted(f.stem for f in (agent / 'windows').glob('*.bat')) == sorted(ACTIONS))
check(sorted(f.name for f in (agent / 'windows').iterdir()) == sorted(a + '.bat' for a in ACTIONS))
check((agent / 'wsl/_wsl-common.sh').read_bytes() == (agent / 'wsl2/_wsl-common.sh').read_bytes())
check('wsl_launcher_file()' in (agent / 'wsl/_wsl-common.sh').read_text())

for p in UNIX:
    for a in ACTIONS:
        path = agent / p / f'{a}.sh'
        text = path.read_text()
        for line in (x for x in text.splitlines() if not x.lstrip().startswith('#')):
            check(not re.search(r'\.\./linux|wsl_run|(^|[\s;(])(bash|sh|exec|source|\.)\s+"?\$SCRIPT_DIR/(?!_wsl-common\.sh)', line), p, a, line)
        check(text.startswith('#!/usr/bin/env bash\n') and '\nset -euo pipefail\n' in text and 'die() {' in text, p, a)
        check(b'\r' not in path.read_bytes() and os.access(path, os.X_OK), p, a)
        if p.startswith('wsl'):
            check(f'WSL_FLAVOR={p}\n' in text and 'wsl_require\nwsl_sanitize_path\nwsl_check_repo "$REPO_ROOT"' in text, p, a)
            check('taskmgr-agent-wsl.vbs' not in text, p, a, 'launcher name must come from wsl_launcher_file')
        # WSL 1 has no systemd and no NAT: no WSL 2-only text or code in scripts/agent/wsl.
        if p == 'wsl':
            check('systemctl' not in text and 'NAT' not in text and 'mirrored' not in text, p, a)
        for ref in re.findall(r'scripts[/\\]+agent[/\\]+(?:linux|macos|wsl2?|windows)[/\\]+[\w-]+\.(?:sh|bat)', text):
            check((root / ref.replace('\\', '/')).is_file(), p, a, ref)

for a in ACTIONS:
    raw = (agent / f'windows/{a}.bat').read_bytes()
    check(raw.count(b'\n') == raw.count(b'\r\n'), a, 'bat must be CRLF')
    text = raw.decode().replace('\r\n', '\n')
    check(text.startswith('@echo off\n') and '\nsetlocal\n' in text, a)
    labels = set(re.findall(r'^:(\w+)', text, re.M))
    targets = set(re.findall(r'\bgoto\s+(\w+)', text)) | set(re.findall(r'\bcall\s+:(\w+)', text))
    check(targets <= labels, a, targets - labels)
    # Every exit outside a subroutine goes through endlocal; failures exit 1.
    check('endlocal & exit /b 1' in text and 'endlocal & exit /b 0' in text, a)
    for opt in ['-h', '--help', '/?']:
        check(f'"{opt}" goto help' in text, a, opt)
    if a != 'uninstall':
        for opt in ['--server', '--key', '--host-id', '--allowed-actions']:
            check(f'if /i "%~1"=="{opt}" goto' in text, a, opt)
        check('Read-Host' in text and '-AsSecureString' in text, a)
        check('dotnet publish' in text and '--self-contained true' in text, a)
        check(text.index('dotnet publish') < text.index('sc.exe create'), a)
    if a == 'reinstall':
        check(text.index('dotnet publish') < text.index('call :remove_agent'), 'reinstall builds before removing')
    if a == 'install':
        check('already installed' in text and 'reinstall.bat' in text, a)
        check(':remove_agent' not in text and ':wait_gone' not in text, 'install.bat has no removal code')
        check(text.index('call :agent_installed') < text.index('Read-Host'), 'existing-install check before the key prompt')
    if a != 'uninstall':
        check(text.index('net session') < text.index('Read-Host') and text.index('Read-Host') < text.index('dotnet publish'), a)
        check("'k:' +" in text and 'set "KEY=%KEY:~2%"' in text and 'IsInputRedirected' in text, a, 'key prompt')
        check('icacls "%SETTINGS%" /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F"' in text
              and text.index('icacls') < text.index('WriteAllText') and 'Environment' not in text.split('REM Usage')[1].replace('Environment value is readable', ''), a)
        check('Unsupported processor architecture' in text and 'PROCESSOR_ARCHITEW6432' in text, a)
    check('setlocal EnableDelayedExpansion\necho Error: !MSG! 1>&2' in text, a)
    if a == 'uninstall':
        check('DeleteEventSource' in text, a)
    if a == 'uninstall':
        check('nothing to remove' in text, a)
    # Lesson: no %VAR% echo inside a parenthesized block (paths may contain parentheses).
    depth = 0
    for line in text.splitlines():
        stripped = line.strip()
        if depth > 0 and stripped.lower().startswith('echo') and '%' in stripped:
            check(False, a, 'echo with a variable inside a block', line)
        depth += line.count('(') - line.count(')') if not stripped.lower().startswith(('rem', 'echo', 'set "')) else 0
        depth = max(depth, 0)
for rule in ['*.sh text eol=lf', '*.bat text eol=crlf', '*.cmd text eol=crlf']:
    check(rule in (root / '.gitattributes').read_text(), rule)

# --- Runs against fakes ------------------------------------------------------------------------
with tempfile.TemporaryDirectory(prefix='perfstats scripts ') as temp:
    temp = Path(temp)
    repo = temp / 'repo'
    shutil.copytree(root / 'scripts', repo / 'scripts', ignore=shutil.ignore_patterns('__pycache__', 'tests'))
    for f in ['run-tui.sh', 'run-docker.sh']:
        shutil.copy2(root / f, repo / f)
    (repo / 'docker').mkdir()
    shutil.copy2(root / 'docker/.env.example', repo / 'docker/.env.example')
    (repo / 'src/TaskManager.Agent').mkdir(parents=True)
    (repo / 'src/TaskManager.Agent/TaskManager.Agent.csproj').write_text('<Project />')
    fakes = temp / 'bin'
    fakes.mkdir()
    for name in ['sysctl', 'dotnet', 'uname', 'sudo', 'systemctl', 'launchctl', 'pkill', 'pgrep', 'ps', 'docker',
                 'cmd.exe', 'wslpath', 'wscript.exe', 'curl', 'ip']:
        (fakes / name).write_text(FAKE)
        (fakes / name).chmod(0o755)
    log = temp / 'calls.jsonl'
    sysroot = temp / 'root'
    home = temp / 'home'
    home.mkdir()
    appdata = temp / 'appdata'
    startup = appdata / 'Microsoft/Windows/Start Menu/Programs/Startup'
    launcher = startup / 'taskmgr-agent-wsl-Test.vbs'  # wsl_launcher_file: <name>-wsl-<distro>.vbs
    base_env = dict(os.environ, PATH=f'{fakes}:/usr/bin:/bin', CALL_LOG=str(log), HOME=str(home),
                    TASKMGR_AGENT_TEST_ROOT=str(sysroot), WSL_SKIP_CHECK='1', WSL_DISTRO_NAME='Test',
                    MOCK_APPDATA=str(appdata), TMPDIR=str(temp),
                    MOCK_LAUNCHD=str(temp / 'launchd-state'), MOCK_RUNNING='1')
    for k in ['DOTNET_ROOT', 'SUDO_USER', 'WSL_INTEROP']:
        base_env.pop(k, None)

    def run(platform, action, *args, **env):
        log.unlink(missing_ok=True)
        e = dict(base_env, MOCK_UNAME='Darwin' if platform == 'macos' else 'Linux')
        if platform == 'linux':
            e['MOCK_PID1'] = 'systemd'
        if platform == 'wsl2':
            e['MOCK_WSL2'] = '1'
        for k, v in env.items():
            if v is None:
                e.pop(k, None)
            else:
                e[k] = v
        p = subprocess.run(['bash', str(repo / f'scripts/agent/{platform}/{action}.sh'), *args], env=e,
                           stdin=subprocess.DEVNULL, capture_output=True, text=True)
        calls = [json.loads(x) for x in log.read_text().splitlines()] if log.exists() else []
        return p, calls

    def index(calls, pred):
        return next((i for i, c in enumerate(calls) if pred(c)), None)

    def tree():
        return sorted(str(p.relative_to(temp)) for p in [*sysroot.rglob('*'), *appdata.rglob('*')] if p.is_file())

    ARGS = ['--server', 'http://monitor:8080/?a=1&b=<2>', '--key', 'k3y', '--allowed-actions', 'Kill, suspend']

    # OS guards.
    for platform, wrong, label in [('linux', 'Darwin', 'for Linux'), ('macos', 'Linux', 'for macOS')]:
        for a in ACTIONS:
            p, c = run(platform, a, *([] if a == 'uninstall' else ARGS), MOCK_UNAME=wrong)
            check(p.returncode == 1 and f'This script is {label}' in p.stderr and not c, platform, a, p.stderr)

    for platform in UNIX:
        shutil.rmtree(sysroot, ignore_errors=True)
        shutil.rmtree(appdata, ignore_errors=True)
        label = {'linux': 'Linux', 'macos': 'macOS', 'wsl': 'WSL 1', 'wsl2': 'WSL 2'}[platform]
        # Argument handling changes nothing.
        for a in ACTIONS:
            p, c = run(platform, a, '--help')
            check(p.returncode == 0 and f'Usage: ./scripts/agent/{platform}/{a}.sh' in p.stdout and not c, platform, a)
            bad = [['extra']] if a == 'uninstall' else [
                [], ['--key', 'k'], ['--server'], ['--server', '--key', 'k'], ['--server', 'ftp://x', '--key', 'k'],
                ['--server', 'http://x', '--key', 'k', '--allowed-actions', 'Kill,Bogus'], ['--server', 'http://x'],
                ['--server', 'http://x', '--key', "it's"], ['--server', 'http://x', '--key', 'k', 'extra']]
            for args in bad:
                p, c = run(platform, a, *args)
                changed = [x for x in c if x[0] == 'sudo' or x[:2] == ['dotnet', 'publish']]
                check(p.returncode == 1 and p.stderr.startswith('Error: ') and not changed and not tree(), platform, a, args, p.stderr, changed)
                if args == ['--server', 'http://x']:
                    # The key is asked for (or required) only after the prerequisite checks.
                    check('[1/5] Checking prerequisites' in p.stdout and '--key is required' in p.stderr, platform, a, p.stdout)

        # Uninstall with nothing installed.
        p, c = run(platform, 'uninstall')
        check(p.returncode == 0 and 'not installed; nothing to remove' in p.stdout, platform, p.stdout, p.stderr)

        # Fresh install.
        p, c = run(platform, 'install', *ARGS)
        check(p.returncode == 0, platform, p.stdout, p.stderr)
        check(p.stdout.startswith(f'TaskManager agent install ({label})\n[1/5] Checking prerequisites...\n[2/5]'), platform, p.stdout)
        pub = next(x for x in c if x[:2] == ['dotnet', 'publish'])
        rid = 'osx-x64' if platform == 'macos' else 'linux-x64'
        check(pub[pub.index('-f') + 1] == 'net10.0' and pub[pub.index('-r') + 1] == rid and '--self-contained' in pub
              and '-p:PublishSingleFile=true' in pub and pub[0] == 'dotnet', platform, pub)
        dest = sysroot / ('usr/local/taskmgr-agent' if platform == 'macos' else 'opt/taskmgr-agent')
        check((dest / 'taskmgr-agent').read_text() == 'agent', platform)
        if platform == 'macos':
            plist = (sysroot / 'Library/LaunchDaemons/com.taskmgr.agent.plist').read_text()
            # No settings in the plist ('launchctl print' shows its environment to every user).
            check('k3y' not in plist and 'EnvironmentVariables' not in plist and 'monitor' not in plist, plist)
            check(f'<string>{dest}/taskmgr-agent</string>' in plist and f'<key>WorkingDirectory</key><string>{dest}</string>' in plist
                  and '<key>KeepAlive</key><true/>' in plist, plist)
            settings = dest / 'appsettings.json'
            check(json.loads(settings.read_text()) == {'TASKMGR': {'Server': 'http://monitor:8080/?a=1&b=<2>', 'AgentKey': 'k3y',
                                                                    'HostId': '', 'AllowedActions': 'Kill,suspend'}}, settings.read_text())
            check(stat.S_IMODE(settings.stat().st_mode) == 0o600)
            check(['sudo', 'chown', 'root:wheel', str(settings)] in c, c)
            check(subprocess.run(['plutil', '-lint', str(sysroot / 'Library/LaunchDaemons/com.taskmgr.agent.plist')],
                                 capture_output=True).returncode == 0 if shutil.which('plutil') else True)
            check(['sudo', 'launchctl', 'bootstrap', 'system', str(sysroot / 'Library/LaunchDaemons/com.taskmgr.agent.plist')] in c, c)
            check(['sudo', 'chown', 'root:wheel', str(sysroot / 'Library/LaunchDaemons/com.taskmgr.agent.plist')] in c, c)
        else:
            env_file = sysroot / 'etc/taskmgr-agent.env'
            host = '' if platform == 'linux' else subprocess.run(['hostname'], capture_output=True, text=True).stdout.strip().lower() + '-wsl'
            check(env_file.read_text() == f"TASKMGR__SERVER='http://monitor:8080/?a=1&b=<2>'\nTASKMGR__AGENTKEY='k3y'\n"
                  f"TASKMGR__HOSTID='{host}'\nTASKMGR__ALLOWEDACTIONS='Kill,suspend'\n", platform, env_file.read_text())
            check(stat.S_IMODE(env_file.stat().st_mode) == 0o600 and ['sudo', 'chown', 'root:root', str(env_file)] in c, platform)
        unit = sysroot / 'etc/systemd/system/taskmgr-agent.service'
        if platform == 'linux':
            text = unit.read_text()
            check(f'ExecStart={dest}/taskmgr-agent\n' in text and f'EnvironmentFile={sysroot}/etc/taskmgr-agent.env\n' in text
                  and 'Restart=always' in text and 'Type=notify' in text and 'WantedBy=multi-user.target' in text, text)
            check(['sudo', 'systemctl', 'enable', '--now', 'taskmgr-agent'] in c, c)
        if platform in ('wsl', 'wsl2'):
            check(not unit.exists(), platform)
            vbs = launcher.read_bytes().decode()
            check(vbs.startswith('CreateObject("WScript.Shell").Run "wsl.exe -d ""Test"" -u root --cd ~ --exec /bin/sh -lc ""exec'), vbs)
            check(f"'/bin/sh' '{dest}/run-wsl.sh'" in vbs and vbs.endswith(', 0, False\r\n'), vbs)
            check(['wscript.exe', 'C:\\fake\\taskmgr-agent-wsl-Test.vbs'] in c, c)
            run_wsl = (dest / 'run-wsl.sh').read_text()
            check(f". '{sysroot}/etc/taskmgr-agent.env'" in run_wsl and f"'{dest}/taskmgr-agent' >>'{sysroot}/var/log/taskmgr-agent.log'" in run_wsl, run_wsl)
            check(not any(x[:2] == ['sudo', 'systemctl'] for x in c), c)
        check(index(c, lambda x: x[:2] == ['dotnet', 'publish']) < index(c, lambda x: x[0] == 'sudo'), platform, 'builds before any root step')
        installed = tree()

        # Installing again fails before building.
        p, c = run(platform, 'install', *ARGS)
        check(p.returncode == 1 and 'already installed' in p.stderr and f'scripts/agent/{platform}/reinstall.sh' in p.stderr
              and not any(x[:2] == ['dotnet', 'publish'] for x in c), platform, p.stderr)

        # Reinstall: build, then stop through the supervisor, then remove, then install and start.
        p, c = run(platform, 'reinstall', '--server', 'http://new:9', '--key', 'k2', '--host-id', 'h1')
        check(p.returncode == 0 and tree() == installed, platform, p.stderr, tree())
        build = index(c, lambda x: x[:2] == ['dotnet', 'publish'])
        if platform == 'linux':
            stop = index(c, lambda x: x == ['sudo', 'systemctl', 'disable', '--now', 'taskmgr-agent'])
            start = index(c, lambda x: x == ['sudo', 'systemctl', 'enable', '--now', 'taskmgr-agent'])
        elif platform == 'macos':
            stop = index(c, lambda x: x == ['sudo', 'launchctl', 'bootout', 'system/com.taskmgr.agent'])
            start = index(c, lambda x: x[:3] == ['sudo', 'launchctl', 'bootstrap'])
        else:
            stop = index(c, lambda x: x == ['sudo', 'pkill', '-f', f'{dest}/[r]un-wsl.sh'])
            check(stop < index(c, lambda x: x == ['sudo', 'pkill', '-x', 'taskmgr-agent']), 'restart loop is stopped first')
            start = index(c, lambda x: x[0] == 'wscript.exe')
        check(None not in (build, stop, start) and build < stop < start, platform, c)
        if platform == 'macos':
            check(json.loads((dest / 'appsettings.json').read_text())['TASKMGR']['Server'] == 'http://new:9')
        else:
            check("TASKMGR__HOSTID='h1'" in (sysroot / 'etc/taskmgr-agent.env').read_text(), platform)

        # A failed build leaves the installed agent alone.
        p, c = run(platform, 'reinstall', *ARGS, MOCK_BUILD_FAIL='1')
        check(p.returncode == 1 and tree() == installed and not any(x[0] == 'sudo' for x in c), platform, p.stderr)

        # Uninstall removes everything the install wrote (including the launcher and an enablement
        # link systemd could not remove), then has nothing to do.
        if platform != 'macos':
            wants = sysroot / 'etc/systemd/system/multi-user.target.wants/taskmgr-agent.service'
            wants.parent.mkdir(parents=True, exist_ok=True)
            wants.symlink_to(sysroot / 'etc/systemd/system/taskmgr-agent.service')
        p, c = run(platform, 'uninstall')
        check('[1/2] Checking prerequisites...' in p.stdout, platform)
        check(p.returncode == 0 and 'TaskManager agent removed.' in p.stdout and tree() == [], platform, p.stderr, tree())
        p, c = run(platform, 'uninstall')
        check(p.returncode == 0 and 'nothing to remove' in p.stdout and not any(x[0] == 'sudo' for x in c), platform)

        # Reinstall with nothing installed.
        p, c = run(platform, 'reinstall', *ARGS)
        check(p.returncode == 0 and 'not installed; continuing' in p.stdout and tree() == installed, platform, p.stderr)
        run(platform, 'uninstall')

        # Only a .NET 8 SDK: builds net8.0. No SDK: fails before anything changes.
        p, c = run(platform, 'install', *ARGS, MOCK_SDKS='8.0.425 [mock]', MOCK_ARCH='aarch64' if platform != 'macos' else 'arm64')
        pub = next(x for x in c if x[:2] == ['dotnet', 'publish'])
        check(p.returncode == 0 and pub[pub.index('-f') + 1] == 'net8.0' and pub[pub.index('-r') + 1].endswith('arm64'), platform, pub)
        run(platform, 'uninstall')
        p, c = run(platform, 'install', *ARGS, MOCK_SDKS='')
        check(p.returncode == 1 and 'No .NET SDK' in p.stderr and tree() == [], platform, p.stderr)

    # Architectures: armv7l maps to linux-arm, unknown ones fail before anything changes; on macOS
    # an x86_64 shell under Rosetta on Apple silicon still publishes osx-arm64.
    for platform in ['linux', 'wsl', 'wsl2']:
        p, c = run(platform, 'install', *ARGS, MOCK_ARCH='armv7l')
        pub = next(x for x in c if x[:2] == ['dotnet', 'publish'])
        check(p.returncode == 0 and pub[pub.index('-r') + 1] == 'linux-arm', platform, pub)
        run(platform, 'uninstall')
        p, c = run(platform, 'install', *ARGS, MOCK_ARCH='mips64')
        check(p.returncode == 1 and 'Unsupported processor architecture: mips64' in p.stderr and tree() == [], platform, p.stderr)
    for arm64, rid in [('1', 'osx-arm64'), ('0', 'osx-x64')]:
        p, c = run('macos', 'install', *ARGS, MOCK_ARCH='x86_64', MOCK_ARM64=arm64)
        pub = next(x for x in c if x[:2] == ['dotnet', 'publish'])
        check(p.returncode == 0 and pub[pub.index('-r') + 1] == rid, arm64, pub)
        run('macos', 'uninstall')
    # macOS: reinstall waits until launchd has unloaded the daemon, and fails (bounded) if it never does.
    run('macos', 'install', *ARGS)
    p, c = run('macos', 'reinstall', *ARGS, MOCK_BOOTOUT_STUCK='1')
    check(p.returncode == 1 and 'did not unload com.taskmgr.agent' in p.stderr
          and not any(x[:3] == ['sudo', 'launchctl', 'bootstrap'] for x in c), p.stderr)
    run('macos', 'uninstall')
    check(tree() == [], tree())
    # WSL: without Windows interop (no Startup folder) nothing is written; "running now" is only
    # claimed once the restart loop is up.
    for platform in ['wsl', 'wsl2']:
        p, c = run(platform, 'install', *ARGS, MOCK_NO_APPDATA='1')
        check(p.returncode == 1 and 'APPDATA' in p.stderr and tree() == []
              and not any(x[:2] == ['dotnet', 'publish'] for x in c), platform, p.stderr)
        p, c = run(platform, 'install', *ARGS, MOCK_RUNNING=None)
        check(p.returncode == 0 and 'running now' not in p.stdout and 'not running yet' in p.stderr, platform, p.stdout)
        p, c = run(platform, 'uninstall', MOCK_NO_APPDATA='1')
        check(p.returncode == 1 and launcher.exists(), platform, 'uninstall stops before removing anything')
        run(platform, 'uninstall')
        check(tree() == [], platform, tree())
    # Installing over an existing install fails on "already installed" before asking for the key.
    run('linux', 'install', *ARGS)
    p, c = run('linux', 'install', '--server', 'http://x')
    check(p.returncode == 1 and 'already installed' in p.stderr, p.stderr)
    run('linux', 'uninstall')

    # Linux needs systemd.
    p, c = run('linux', 'install', *ARGS, MOCK_PID1='init')
    check(p.returncode == 1 and 'systemd is not running' in p.stderr and tree() == [], p.stderr)
    # WSL 2 with systemd: a system service and no launcher.
    p, c = run('wsl2', 'install', *ARGS, MOCK_PID1='systemd')
    check(p.returncode == 0 and (sysroot / 'etc/systemd/system/taskmgr-agent.service').is_file() and not launcher.exists(), p.stderr)
    check(['sudo', 'systemctl', 'enable', '--now', 'taskmgr-agent'] in c and 'WSL stops a distro' in p.stdout, c)
    p, c = run('wsl2', 'uninstall', MOCK_PID1='systemd')
    check(p.returncode == 0 and tree() == [] and ['sudo', 'systemctl', 'disable', '--now', 'taskmgr-agent'] in c, tree())
    # A unit left by scripts/agent/linux makes install refuse; reinstall/uninstall clean it up (WSL 1 too).
    for platform in ['wsl', 'wsl2']:
        unit = sysroot / 'etc/systemd/system/taskmgr-agent.service'
        unit.parent.mkdir(parents=True, exist_ok=True)
        unit.write_text('[Unit]\n')
        p, c = run(platform, 'install', *ARGS)
        check(p.returncode == 1 and 'already installed' in p.stderr, platform)
        p, c = run(platform, 'reinstall', *ARGS)
        check(p.returncode == 0 and not unit.exists() and launcher.exists(), platform, p.stderr)
        run(platform, 'uninstall')
        check(tree() == [], platform, tree())
        # Startup mode needs the distro name (sudo drops it; the script recovers it only under sudo).
        p, c = run(platform, 'install', *ARGS, WSL_DISTRO_NAME=None)
        check(p.returncode == 1 and 'WSL_DISTRO_NAME is not set' in p.stderr and tree() == [], platform, p.stderr)
    # A launcher from the earlier scripts (<name>-wsl.vbs) for this distro counts as installed and is removed;
    # one that belongs to another distro is left alone.
    legacy = startup / 'taskmgr-agent-wsl.vbs'
    for platform in ['wsl', 'wsl2']:
        startup.mkdir(parents=True, exist_ok=True)
        legacy.write_bytes(b'CreateObject("WScript.Shell").Run "wsl.exe -d ""Test"" -u root --cd ~ --exec /bin/sh -lc ""exec x""", 0, False\r\n')
        p, c = run(platform, 'install', *ARGS)
        check(p.returncode == 1 and 'already installed' in p.stderr and legacy.exists(), platform, p.stderr)
        p, c = run(platform, 'uninstall')
        check(p.returncode == 0 and not legacy.exists() and tree() == [], platform, p.stderr, tree())
        legacy.write_bytes(b'CreateObject("WScript.Shell").Run "wsl.exe -d ""Other"" --cd ~", 0, False\r\n')
        p, c = run(platform, 'uninstall')
        check(p.returncode == 0 and 'nothing to remove' in p.stdout and legacy.exists(), platform, p.stdout)
        legacy.unlink()

    # WSL 2 warns (and does not rewrite) when a localhost server does not answer.
    p, c = run('wsl2', 'install', '--server', 'http://localhost:8080', '--key', 'k')
    check(p.returncode == 0 and 'nothing answers at http://localhost:8080' in p.stderr
          and "TASKMGR__SERVER='http://localhost:8080'" in (sysroot / 'etc/taskmgr-agent.env').read_text(), p.stderr)
    run('wsl2', 'uninstall')

    # ICU. A fake ldconfig (off PATH, so every other run has no ldconfig and skips the checks) reports
    # MOCK_LDCACHE until the fake apt-get has "installed" something, then a cache with libicuuc.
    # apt-get and apt-cache live in their own directory, added to PATH only here (sudo is the fake above).
    (temp / 'ldc').mkdir()
    (temp / 'apt').mkdir()
    have_icu = '\tlibicuuc.so.74 (libc6,x86-64) => /usr/lib/x86_64-linux-gnu/libicuuc.so.74'
    i386_icu = '\tlibicuuc.so.74 (libc6) => /usr/lib/i386-linux-gnu/libicuuc.so.74'
    no_icu = '\tlibc.so.6 (libc6,x86-64) => /lib/libc.so.6'
    apt_done = temp / 'apt-installed'
    for d, name, body in [('ldc', 'ldconfig', f'#!/bin/sh\nif [ -f "{apt_done}" ]; then printf "%s\\n" "{have_icu}"; else printf "%s\\n" "$MOCK_LDCACHE"; fi\n'),
                          ('apt', 'apt-get', f'#!/bin/sh\necho "[\\"apt-get\\", \\"$*\\"]" >> "$CALL_LOG"\n[ "$1" = install ] && touch "{apt_done}"\nexit 0\n'),
                          ('apt', 'apt-cache', '#!/bin/sh\nprintf "libicu-dev\\nlibicu74\\nlibicu72\\n"\n')]:
        (temp / d / name).write_text(body)
        (temp / d / name).chmod(0o755)
    ldconfig = str(temp / 'ldc/ldconfig')
    apt_path = f'{temp / "apt"}:{fakes}:/usr/bin:/bin'

    def run_icu(platform, a, *args, **env):
        apt_done.unlink(missing_ok=True)
        var = 'NATIVE_LDCONFIG' if platform == 'linux' else 'WSL_LDCONFIG'
        p, c = run(platform, a, *args, **{var: ldconfig, **env})
        return p, [x for x in c if x[0] == 'apt-get'], [x for x in c if x[:2] == ['dotnet', 'publish']]

    for platform in ['linux', 'wsl', 'wsl2']:
        for a in ['install', 'reinstall']:
            for cache in [no_icu, i386_icu]:
                # The SDK needs ICU to build the agent: install-type scripts install it, then build.
                p, apt, build = run_icu(platform, a, *ARGS, MOCK_LDCACHE=cache, PATH=apt_path)
                check(p.returncode == 0 and apt == [['apt-get', 'update -qq'], ['apt-get', 'install -y -q libicu74']] and build,
                      platform, a, p.stderr, apt)
                run(platform, 'uninstall')
            for env in [dict(INSTALL_SYSTEM_PACKAGES='0', PATH=apt_path), {}]:
                p, apt, build = run_icu(platform, a, *ARGS, MOCK_LDCACHE=no_icu, **env)
                check(p.returncode == 1 and 'needs system packages that are not installed' in p.stderr
                      and 'libicu-dev' in p.stderr and not apt and not build and tree() == [], platform, a, env, p.stderr)
            p, apt, build = run_icu(platform, a, '--help', MOCK_LDCACHE=no_icu, PATH=apt_path)
            check(p.returncode == 0 and not apt and not build, platform, a)
            for env in [dict(MOCK_LDCACHE=have_icu), dict(MOCK_LDCACHE=no_icu, DOTNET_SYSTEM_GLOBALIZATION_INVARIANT='1')]:
                p, apt, build = run_icu(platform, a, *ARGS, PATH=apt_path, **env)
                check(p.returncode == 0 and not apt and build, platform, a, env, p.stderr)
                run(platform, 'uninstall')
        # uninstall needs no dotnet, so it neither checks nor installs ICU.
        p, apt, build = run_icu(platform, 'uninstall', MOCK_LDCACHE=no_icu, PATH=apt_path)
        check(p.returncode == 0 and not apt, platform, p.stderr)
    check(tree() == [], tree())

    # run-tui.sh is a launcher: it only checks. Missing ICU stops it before building, and it never installs.
    e = dict(base_env, MOCK_UNAME='Linux', DOTNET=str(fakes / 'dotnet'), NATIVE_LDCONFIG=ldconfig, PATH=apt_path)
    for cache in [no_icu, i386_icu]:
        log.unlink(missing_ok=True)
        apt_done.unlink(missing_ok=True)
        p = subprocess.run(['bash', str(repo / 'run-tui.sh')], env=dict(e, MOCK_LDCACHE=cache), capture_output=True, text=True)
        calls = [json.loads(x) for x in log.read_text().splitlines()] if log.exists() else []
        check(p.returncode == 1 and 'needs ICU (libicu)' in p.stderr and 'sudo apt install libicu74' in p.stderr
              and not any(x[0] in ('apt-get', 'sudo') or x[1:2] == ['build'] for x in calls), p.stderr, calls)
    # With ICU, the clipboard note appears on Linux when no wl-copy/xclip/xsel exists; under WSL the TUI
    # also gets the Windows PATH entries (after the Linux ones) and BROWSER=wslview.
    (temp / 'wslu').mkdir()
    (temp / 'wslu/wslview').write_text('#!/bin/sh\nexit 0\n')
    (temp / 'wslu/wslview').chmod(0o755)
    env_out = temp / 'tui-env.json'
    p = subprocess.run(['bash', str(repo / 'run-tui.sh')], env=dict(e, MOCK_LDCACHE=have_icu, MOCK_ENV_OUT=str(env_out)),
                       capture_output=True, text=True)
    check(p.returncode == 0 and 'copy to clipboard will not work in taskmgr' in p.stderr and 'sudo apt install xclip' in p.stderr, p.stderr)
    check(json.loads(env_out.read_text())['BROWSER'] is None, 'no BROWSER outside WSL')
    p = subprocess.run(['bash', str(repo / 'run-tui.sh')], env=dict(e, MOCK_LDCACHE=have_icu, MOCK_ENV_OUT=str(env_out), TASKMGR_ASSUME_WSL='1',
                                                                    PATH=f'/mnt/c/Windows/System32:{temp / "wslu"}:{apt_path}'),
                       capture_output=True, text=True)
    seen = json.loads(env_out.read_text())
    check(p.returncode == 0 and seen['BROWSER'] == 'wslview' and seen['PATH'].endswith(':/mnt/c/Windows/System32')
          and seen['PATH'].startswith(str(temp / 'wslu')), p.stderr, seen)
    p = subprocess.run(['bash', str(repo / 'run-tui.sh')], env=dict(e, MOCK_LDCACHE=have_icu, TASKMGR_ASSUME_WSL='1'), capture_output=True, text=True)
    check(p.returncode == 0 and 'sudo apt install wslu xdg-utils' in p.stderr, p.stderr)

    # run-tui.sh picks net10.0 with a .NET 10 SDK listed after a .NET 8 one (pipefail/SIGPIPE fix).
    e = dict(base_env, MOCK_UNAME='Linux', DOTNET=str(fakes / 'dotnet'))
    log.unlink(missing_ok=True)
    p = subprocess.run(['bash', str(repo / 'run-tui.sh'), '--version'], env=e, capture_output=True, text=True)
    calls = [json.loads(x) for x in log.read_text().splitlines()]
    check(p.returncode == 0 and ['dotnet', 'build', 'src/TaskManager.Tui/TaskManager.Tui.csproj', '-c', 'Release', '-f', 'net10.0', '-nologo', '-v', 'quiet'] in calls
          and calls[-1] == ['dotnet', 'src/TaskManager.Tui/bin/Release/net10.0/taskmgr.dll', '--version'], calls, p.stderr)
    log.unlink(missing_ok=True)
    p = subprocess.run(['bash', str(repo / 'run-tui.sh')], env=dict(e, MOCK_SDKS='8.0.425 [mock]'), capture_output=True, text=True)
    calls = [json.loads(x) for x in log.read_text().splitlines()]
    check(p.returncode == 0 and calls[-1] == ['dotnet', 'src/TaskManager.Tui/bin/Release/net8.0/taskmgr.dll'], calls)
    # run-docker.sh: creates docker/.env, and an env file without the port keys no longer aborts after 'up'.
    p = subprocess.run(['bash', str(repo / 'run-docker.sh')], env=e, capture_output=True, text=True)
    env_text = (repo / 'docker/.env').read_text()
    check(p.returncode == 0 and 'TASKMGR_ADMIN_PASSWORD=change-me\n' not in env_text and 'http://localhost:3000' in p.stdout, p.stderr)
    (repo / 'docker/.env').write_text('TASKMGR_ADMIN_PASSWORD=x\nTASKMGR_AGENT_KEY=y\n')
    p = subprocess.run(['bash', str(repo / 'run-docker.sh'), 'up'], env=e, capture_output=True, text=True)
    check(p.returncode == 0 and 'Web app:   http://localhost:3000' in p.stdout and 'Server:    http://localhost:8080' in p.stdout, p.stdout, p.stderr)
    p = subprocess.run(['bash', str(repo / 'run-docker.sh'), 'bogus'], env=e, capture_output=True, text=True)
    check(p.returncode == 2, p.stderr)

# Shell syntax for every script.
for path in [*root.glob('scripts/**/*.sh'), root / 'run-tui.sh', root / 'run-docker.sh']:
    subprocess.run(['bash', '-n', str(path)], check=True)
print(f'{count} checks passed: same-set rule, self-contained scripts, OS guards, arguments, build and service '
      'commands, install/reinstall/uninstall semantics, files written (env, unit, plist, launcher), Windows batch '
      'structure, run-tui.sh and run-docker.sh.')
