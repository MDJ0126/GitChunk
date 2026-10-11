import SwiftUI
import AppKit

@MainActor final class Model: ObservableObject {
    @Published var folder = FileManager.default.homeDirectoryForCurrentUser.path
    @Published var installation = "unknown"
    @Published var rules = ""
    @Published var busy = false
    @Published var status = "Ready"
    @Published var log = "Select a repository and install Git Chunk.\n"
    private var revision = 0
    private var loadedFolder = ""
    var valid: Bool { ["installed", "incomplete", "legacy", "not-installed"].contains(installation) && loadedFolder == folder }
    var installationText: String {
        switch installation {
        case "installed": return "✓ Installed · Automatic chunking and restoration are enabled for this repository on this computer."
        case "legacy": return "A legacy installation was found. Upgrade to Git Chunk to migrate its storage."
        case "incomplete": return "Some settings are missing. Reinstall to repair them."
        case "not-installed": return "Not installed · Install to convert current LFS files or restore a fresh clone. Commit or stash tracked changes first. Past LFS history and server storage are preserved."
        case "not-root": return "Select the root folder of your Git repository."
        case "not-repository": return "The selected folder is not a Git repository."
        default: return "Unable to check installation. Verify the folder path and Git installation."
        }
    }
    func command(_ args: [String], input: String? = nil, completion: @escaping (Bool, String) -> Void) {
        guard let exe = Bundle.main.executableURL else { completion(false, "Application path unavailable"); return }
        let backend = exe.deletingLastPathComponent().appendingPathComponent("GitChunk-cli")
        DispatchQueue.global(qos: .utility).async {
            let process = Process(), stdout = Pipe(), stderr = Pipe(), stdin = Pipe()
            process.executableURL = backend; process.arguments = args
            process.standardOutput = stdout; process.standardError = stderr
            process.standardInput = input == nil ? FileHandle.nullDevice : stdin
            let group = DispatchGroup()
            // Read stderr concurrently with stdout to avoid pipe deadlocks.
            var errorData = Data()
            do {
                try process.run()
                group.enter()
                DispatchQueue.global(qos: .utility).async { errorData = stderr.fileHandleForReading.readDataToEndOfFile(); group.leave() }
                if let input { stdin.fileHandleForWriting.write(Data(input.utf8)); try? stdin.fileHandleForWriting.close() }
                let data = stdout.fileHandleForReading.readDataToEndOfFile()
                process.waitUntilExit(); group.wait()
                let text = String(decoding: process.terminationStatus == 0 ? data : errorData, as: UTF8.self)
                let success = process.terminationStatus == 0
                DispatchQueue.main.async { completion(success, text) }
            } catch { let message = error.localizedDescription; DispatchQueue.main.async { completion(false, message) } }
        }
    }
    func refresh() {
        revision += 1
        let current = revision, path = folder
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            guard current == self.revision, !self.busy else { return }
            self.command(["git-status", path]) { ok, text in
                guard current == self.revision else { return }
                self.installation = ok ? text.trimmingCharacters(in: .whitespacesAndNewlines) : "unknown"
                if ["installed", "incomplete", "legacy", "not-installed"].contains(self.installation), self.loadedFolder != path {
                    self.command(["git-ignore-read", path]) { ok, text in
                        guard current == self.revision else { return }
                        if ok { self.rules = text; self.loadedFolder = path }
                    }
                }
            }
        }
    }
    func choose() {
        let panel = NSOpenPanel(); panel.canChooseDirectories = true; panel.canChooseFiles = false; panel.allowsMultipleSelection = false
        panel.directoryURL = URL(fileURLWithPath: folder)
        if panel.runModal() == .OK, let url = panel.url { folder = url.path }
    }
    func saveRules(install: Bool = false, update: Bool = false) {
        guard valid, !busy else { return }; busy = true; status = update ? "Updating…" : install ? "Installing…" : "Saving…"
        let path = folder
        command(["git-ignore-save", path], input: rules) { ok, text in
            guard ok else { self.busy = false; self.status = "Save failed"; self.log += text + "\n"; return }
            if install || update {
                self.command([update ? "update" : "git-install", path]) { ok, text in
                    self.busy = false; self.status = ok ? "Git Chunk installed" : "Installation failed"
                    self.log += text + "\n"
                    if ok { self.log += "git add -u\ngit add --renormalize .\ngit add .gitattributes .gitignore .gitchunk\ngit commit -m \"Configure Git Chunk\"\n" }
                    self.refresh()
                }
            } else {
                self.busy = false; self.status = "Ignore list saved"; self.log += "Saved .chunkignore. Apply to existing files: git add --renormalize .\n"
            }
        }
    }
}

struct ContentView: View {
    @ObservedObject var model: Model
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(spacing: 14) {
                if let url = Bundle.main.url(forResource: "AppIcon", withExtension: "png"), let icon = NSImage(contentsOf: url) { Image(nsImage: icon).resizable().frame(width: 54, height: 54) }
                VStack(alignment: .leading, spacing: 4) { Text("Git Chunk").font(.system(size: 29, weight: .bold)); Text("Install once. Use Git as usual.").foregroundStyle(.secondary) }
            }
            VStack(alignment: .leading, spacing: 12) {
                Text("Git repository").fontWeight(.semibold)
                HStack { TextField("Repository root folder", text: $model.folder).textFieldStyle(.roundedBorder); Button("Browse") { model.choose() } }
                Text(model.installationText).font(.caption).foregroundStyle(.secondary)
            }.padding(20).background(.white).cornerRadius(12).disabled(model.busy)
            VStack(alignment: .leading, spacing: 12) {
                Text("Chunk ignore (.chunkignore)").fontWeight(.semibold)
                Text("Files matching these patterns will not be chunked · Example: Build/**").font(.caption).foregroundStyle(.secondary)
                TextEditor(text: $model.rules).font(.system(size: 13, design: .monospaced)).frame(minHeight: 140).overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.gray.opacity(0.2)))
                Button("Save ignore list") { model.saveRules() }
            }.padding(20).background(.white).cornerRadius(12).disabled(model.busy || !model.valid)
            HStack {
                VStack(alignment: .leading) { Text(model.status).fontWeight(.semibold); Text("Files over 50 MiB → 49 MiB chunks · local originals preserved").font(.caption).foregroundStyle(.secondary) }
                if model.busy { ProgressView().controlSize(.small) }; Spacer()
                Button("Update to latest") { model.saveRules(update: true) }.disabled(model.busy || !model.valid)
                Button(model.installation == "installed" ? "Reinstall this version  →" : "Install Git Chunk  →") { model.saveRules(install: true) }.buttonStyle(.borderedProminent).controlSize(.large).disabled(model.busy || !model.valid)
            }
            VStack(alignment: .leading, spacing: 8) {
                HStack { Text("Activity log").fontWeight(.semibold); Spacer(); Button("Copy log") { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(model.log, forType: .string) } }
                ScrollView { Text(model.log).font(.system(size: 11, design: .monospaced)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
            }.padding(16).frame(height: 110).background(.white).cornerRadius(12)
        }.padding(28).frame(minWidth: 740, minHeight: 740).background(Color(red: 0.96, green: 0.97, blue: 0.99))
            .onAppear { model.refresh() }.onChange(of: model.folder) { _ in model.refresh() }
            .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in model.refresh() }
    }
}
@main struct GitChunkMac: App {
    @StateObject private var model = Model()
    var body: some Scene { WindowGroup("Git Chunk") { ContentView(model: model) }.defaultSize(width: 850, height: 840) }
}
