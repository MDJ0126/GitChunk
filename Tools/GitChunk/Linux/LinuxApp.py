#!/usr/bin/env python3
"""Linux desktop for the same Git Chunk commands used by Windows and macOS."""
import os
from pathlib import Path
import queue
import subprocess
import sys
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

BASE = Path(__file__).resolve().parent
VALID = {'installed', 'incomplete', 'legacy', 'not-installed'}
MESSAGES = {
    'installed': 'Installed · Automatic chunking and restoration are enabled on this computer.',
    'incomplete': 'Some settings are missing. Reinstall to repair them.',
    'legacy': 'A legacy installation was found. Install Git Chunk to migrate its storage.',
    'not-installed': 'Not installed · Commit or stash tracked changes before installing. Past LFS history is preserved.',
    'not-root': 'Select the root folder of your Git repository.',
    'not-repository': 'The selected folder is not a Git repository.',
}


class App:
    def __init__(self, window, folder=None):
        self.window = window
        self.events = queue.Queue()
        self.busy = False
        self.installation = 'unknown'
        self.loaded = ''
        self.revision = 0
        self.pending = None
        self.dirty = False
        self.window.title('Git Chunk')
        self.window.geometry('900x820')
        self.window.minsize(740, 730)
        self.window.configure(bg='#f3f6fb')
        style = ttk.Style(window)
        style.theme_use('clam')
        style.configure('TFrame', background='#f3f6fb')
        style.configure('TLabel', background='#f3f6fb', foreground='#20304a')
        style.configure('Title.TLabel', font=('Sans', 26, 'bold'))
        style.configure('Heading.TLabel', font=('Sans', 11, 'bold'))
        style.configure('TButton', padding=(12, 9))
        style.configure('Primary.TButton', background='#2464d7', foreground='white')
        style.map('Primary.TButton', background=[('active', '#174ca8'), ('disabled', '#aab8cf')])
        frame = ttk.Frame(window, padding=24)
        frame.pack(fill='both', expand=True)
        header = ttk.Frame(frame)
        header.pack(fill='x', pady=(0, 18))
        try:
            self.icon = tk.PhotoImage(file=str(BASE / 'AppIcon.png'))
            window.iconphoto(True, self.icon)
            ratio = max(1, self.icon.width() // 56)
            self.small_icon = self.icon.subsample(ratio, ratio)
            ttk.Label(header, image=self.small_icon).pack(side='left', padx=(0, 14))
        except tk.TclError:
            pass
        titles = ttk.Frame(header)
        titles.pack(side='left')
        ttk.Label(titles, text='Git Chunk', style='Title.TLabel').pack(anchor='w')
        ttk.Label(titles, text='Install once. Use Git as usual.').pack(anchor='w')
        ttk.Label(frame, text='Git repository', style='Heading.TLabel').pack(anchor='w')
        row = ttk.Frame(frame)
        row.pack(fill='x', pady=(8, 6))
        self.folder = tk.StringVar(value=folder or str(Path.home()))
        self.path_entry = ttk.Entry(row, textvariable=self.folder)
        self.path_entry.pack(side='left', fill='x', expand=True)
        self.browse = ttk.Button(row, text='Browse', command=self.choose)
        self.browse.pack(side='left', padx=(10, 0))
        self.info = tk.StringVar(value='Checking repository…')
        ttk.Label(frame, textvariable=self.info, wraplength=800).pack(fill='x', pady=(0, 20))
        ttk.Label(frame, text='Chunk ignore (.chunkignore)', style='Heading.TLabel').pack(anchor='w')
        ttk.Label(frame, text='Matching files will not be chunked · Example: Build/**').pack(anchor='w', pady=(4, 8))
        rules_frame = ttk.Frame(frame)
        rules_frame.pack(fill='both', expand=True)
        self.rules = tk.Text(rules_frame, height=9, undo=True, font=('Monospace', 11), wrap='none', relief='solid', borderwidth=1)
        scroll = ttk.Scrollbar(rules_frame, command=self.rules.yview)
        self.rules.configure(yscrollcommand=scroll.set)
        scroll.pack(side='right', fill='y')
        self.rules.pack(fill='both', expand=True)
        self.rules.bind('<<Modified>>', self.changed)
        self.save = ttk.Button(frame, text='Save ignore list', command=lambda: self.action('save'))
        self.save.pack(anchor='w', pady=(8, 18))
        self.status = tk.StringVar(value='Ready')
        ttk.Label(frame, textvariable=self.status, style='Heading.TLabel').pack(anchor='w')
        ttk.Label(frame, text='Files over 50 MiB → 49 MiB chunks · local originals preserved').pack(anchor='w', pady=(5, 10))
        buttons = ttk.Frame(frame)
        buttons.pack(fill='x', pady=(0, 18))
        self.remove = ttk.Button(buttons, text='Uninstall', command=lambda: self.action('uninstall'))
        self.remove.pack(side='left')
        self.update = ttk.Button(buttons, text='Update to latest', command=lambda: self.action('update'))
        self.update.pack(side='left', padx=8)
        self.install = ttk.Button(buttons, text='Install Git Chunk →', style='Primary.TButton', command=lambda: self.action('install'))
        self.install.pack(side='right')
        log_header = ttk.Frame(frame)
        log_header.pack(fill='x')
        ttk.Label(log_header, text='Activity log', style='Heading.TLabel').pack(side='left')
        ttk.Button(log_header, text='Copy log', command=self.copy_log).pack(side='right')
        self.log = tk.Text(frame, height=7, font=('Monospace', 10), wrap='word', state='disabled', relief='solid', borderwidth=1)
        self.log.pack(fill='x', pady=(5, 0))
        self.append('Select a repository and install Git Chunk.\n')
        self.folder.trace_add('write', self.schedule_refresh)
        window.protocol('WM_DELETE_WINDOW', self.close)
        self.controls()
        window.after(50, self.drain)
        self.schedule_refresh()

    def command(self, args, callback, data=None):
        def work():
            try:
                result = subprocess.run([str(BASE / 'git-chunk'), *args], input=data, text=True, encoding='utf-8', errors='replace', capture_output=True)
                ok = result.returncode == 0
                text = result.stdout + result.stderr
            except Exception as error:
                ok, text = False, str(error)
            self.events.put((callback, ok, text))
        threading.Thread(target=work, daemon=True).start()

    def drain(self):
        try:
            while True:
                callback, ok, text = self.events.get_nowait()
                callback(ok, text)
        except queue.Empty:
            pass
        self.window.after(50, self.drain)

    def controls(self):
        valid = not self.busy and self.loaded == self.folder.get() and self.installation in VALID
        for widget in (self.save, self.update, self.install):
            widget.configure(state='normal' if valid else 'disabled')
        self.remove.configure(state='normal' if valid and self.installation != 'not-installed' else 'disabled')
        self.rules.configure(state='normal' if valid else 'disabled')
        for widget in (self.path_entry, self.browse):
            widget.configure(state='disabled' if self.busy else 'normal')
        self.install.configure(text='Reinstall this version →' if self.installation == 'installed' else 'Install Git Chunk →')

    def schedule_refresh(self, *_):
        self.revision += 1
        if self.pending is not None:
            self.window.after_cancel(self.pending)
        self.controls()
        self.pending = self.window.after(250, self.refresh)

    def refresh(self):
        self.pending = None
        if self.busy:
            return
        revision, path = self.revision, self.folder.get()
        def status_done(ok, text):
            if revision != self.revision:
                return
            self.installation = text.strip() if ok else 'unknown'
            self.info.set(MESSAGES.get(self.installation, 'Unable to check installation. Verify the folder and Git installation.'))
            if self.installation in VALID and self.loaded != path:
                def rules_done(ok, text):
                    if revision != self.revision:
                        return
                    if ok:
                        self.rules.configure(state='normal')
                        self.rules.delete('1.0', 'end'); self.rules.insert('1.0', text)
                        self.rules.edit_modified(False)
                        self.dirty = False
                        self.loaded = path
                    else:
                        self.append(text)
                    self.controls()
                self.command(['git-ignore-read', path], rules_done)
            self.controls()
        self.command(['git-status', path], status_done)

    def changed(self, _=None):
        if self.rules.edit_modified():
            self.dirty = True
            self.status.set('Ignore list changed · not saved yet')
            self.rules.edit_modified(False)

    def choose(self):
        if self.dirty and not messagebox.askyesno('Unsaved ignore list', 'Discard unsaved changes and select another repository?'):
            return
        folder = filedialog.askdirectory(title='Select Git repository root', initialdir=self.folder.get() if Path(self.folder.get()).is_dir() else str(Path.home()))
        if folder:
            self.folder.set(folder)

    def action(self, action):
        if self.busy or self.loaded != self.folder.get() or self.installation not in VALID:
            return
        path, rules = self.folder.get(), self.rules.get('1.0', 'end-1c')
        self.busy = True
        self.status.set({'save': 'Saving…', 'install': 'Installing…', 'update': 'Updating…', 'uninstall': 'Uninstalling…'}[action])
        self.controls()
        def done(ok, text):
            self.busy = False
            self.status.set('Completed' if ok else 'Operation failed')
            self.append(text + '\n')
            if ok:
                self.dirty = False
                self.loaded = ''
            self.controls(); self.schedule_refresh()
        if action == 'uninstall':
            self.command(['git-uninstall', path], done)
            return
        def saved(ok, text):
            if not ok or action == 'save':
                done(ok, text if not ok else 'Saved .chunkignore. Apply to existing files: git add --renormalize .')
            else:
                self.command(['update' if action == 'update' else 'git-install', path], done)
        self.command(['git-ignore-save', path], saved, data=rules)

    def append(self, text):
        self.log.configure(state='normal'); self.log.insert('end', text); self.log.see('end'); self.log.configure(state='disabled')

    def copy_log(self):
        self.window.clipboard_clear(); self.window.clipboard_append(self.log.get('1.0', 'end-1c'))

    def close(self):
        if self.busy:
            messagebox.showinfo('Git Chunk', 'Please wait for the current operation to finish.')
        elif not self.dirty or messagebox.askyesno('Unsaved ignore list', 'Close without saving the ignore list?'):
            self.window.destroy()


if __name__ == '__main__':
    try:
        window = tk.Tk()
    except tk.TclError as error:
        raise SystemExit('A graphical desktop is required. Use ./git-chunk for the CLI.\n' + str(error))
    App(window, sys.argv[1] if len(sys.argv) > 1 else None)
    window.mainloop()
