"""Exercise real desktop callbacks and CLI on Linux under Xvfb or a desktop."""
from pathlib import Path
import importlib.util
import os
import subprocess
import sys
import tempfile
import time
import tkinter as tk

package = Path(sys.argv[1]).resolve()
spec = importlib.util.spec_from_file_location('linux_app', package / 'LinuxApp.py')
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)

def run(*args):
    return subprocess.run(args, check=True, capture_output=True, text=True).stdout

with tempfile.TemporaryDirectory(prefix='GitChunk GUI 한글 ') as temp:
    repo = Path(temp) / 'repository'; repo.mkdir()
    run('git', '-C', str(repo), 'init')
    run('git', '-C', str(repo), 'config', 'user.name', 'Test')
    run('git', '-C', str(repo), 'config', 'user.email', 'test@example.invalid')
    window = tk.Tk(); app = module.App(window, str(repo))
    def wait(predicate):
        until = time.monotonic() + 90
        while time.monotonic() < until:
            window.update()
            if predicate(): return
            time.sleep(.05)
        raise AssertionError('Timed out: ' + app.status.get() + '\n' + app.log.get('1.0','end'))
    wait(lambda: app.loaded == str(repo))
    assert app.installation == 'not-installed'
    app.rules.delete('1.0','end'); app.rules.insert('1.0', '# Unicode 정책\nBuild/**\n')
    app.action('save'); wait(lambda: not app.busy and app.loaded == str(repo))
    assert 'Build/**' in (repo / '.chunkignore').read_text()
    app.action('install'); wait(lambda: not app.busy and app.installation == 'installed')
    assert list((repo / '.git/gitchunk').glob('GitChunk-linux-*'))
    run('git','-C',str(repo),'add','.')
    run('git','-C',str(repo),'commit','-m','Configure Git Chunk')
    app.action('update'); wait(lambda: not app.busy and app.loaded == str(repo))
    assert app.status.get() == 'Completed', app.log.get('1.0','end')
    screenshot = os.environ.get('GITCHUNK_GUI_SCREENSHOT')
    if screenshot:
        subprocess.run(['import','-window',str(window.winfo_id()),screenshot],check=True)
    app.action('uninstall'); wait(lambda: not app.busy and app.installation == 'not-installed')
    assert run(str(package/'git-chunk'),'status',str(repo)).strip() == 'not-installed'
    app.folder.set(temp); wait(lambda: app.installation == 'not-repository')
    assert str(app.install['state']) == 'disabled'
    window.destroy()
print('PASS: desktop loads repository, saves Unicode rules, installs, updates, uninstalls, disables invalid repository')
