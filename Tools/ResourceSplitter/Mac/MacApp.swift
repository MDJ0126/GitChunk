import SwiftUI
import AppKit

enum Operation: String, CaseIterable {
    case split, join, automate = "git-install"
    var title: String { switch self { case .split: return "파일 분할"; case .join: return "원본 복원"; case .automate: return "Git 자동화" } }
    var caption: String { switch self { case .split: return "원본을 복원 정보와 조각으로 나눕니다."; case .join: return "조각을 검증하고 원본으로 되돌립니다."; case .automate: return "Git 작업에 자동 분할·복원을 연결합니다." } }
    var action: String { switch self { case .split: return "분할 시작"; case .join: return "복원 시작"; case .automate: return "자동화 설치" } }
}

@MainActor final class Model: ObservableObject {
    @Published var folder = FileManager.default.homeDirectoryForCurrentUser.path
    @Published var operation = Operation.automate
    @Published var busy = false
    @Published var status = "준비됨"
    @Published var installation = "unknown"
    private var statusRevision = 0
    var installationText: String {
        switch installation {
        case "installed": return "✓ 이 PC의 선택한 저장소에 Git 자동화가 설치되어 있습니다."
        case "incomplete": return "⚠ 일부 설정이 누락되었거나 다른 운영체제용입니다. 재설치해 주세요."
        case "not-installed": return "이 PC의 선택한 저장소에는 Git 자동화가 설치되지 않았습니다."
        case "not-root": return "Git 저장소의 최상위 폴더를 선택해 주세요."
        case "not-repository": return "선택한 폴더는 Git 저장소가 아닙니다."
        default: return "설치 상태 확인 불가 · 폴더 경로와 Git 설치를 확인해 주세요."
        }
    }
    func refreshInstallation() {
        statusRevision += 1
        let revision = statusRevision, path = folder
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            guard revision == self.statusRevision, !self.busy, let executable = Bundle.main.executableURL else { return }
            let backend = executable.deletingLastPathComponent().appendingPathComponent("ResourceSplitter-cli")
            DispatchQueue.global(qos: .utility).async {
                let process = Process(), output = Pipe()
                process.executableURL = backend; process.arguments = ["git-status", path]
                process.standardOutput = output; process.standardError = FileHandle.nullDevice
                var state = "unknown"
                do { try process.run(); let data = output.fileHandleForReading.readDataToEndOfFile(); process.waitUntilExit(); if process.terminationStatus == 0 { state = String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines) } } catch {}
                let result = state
                DispatchQueue.main.async { if revision == self.statusRevision { self.installation = result } }
            }
        }
    }
    @Published var log = "프로젝트 폴더와 작업을 선택하세요.\n"
    private var worker: Process?
    func choose() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true; panel.canChooseFiles = false; panel.allowsMultipleSelection = false
        panel.directoryURL = URL(fileURLWithPath: folder)
        if panel.runModal() == .OK, let url = panel.url { folder = url.path }
    }
    func append(_ text: String) { log += text }
    func execute() {
        guard !busy else { return }
        var directory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: folder, isDirectory: &directory), directory.boolValue else {
            status = "폴더 확인 필요"; append("존재하는 프로젝트 폴더를 선택해 주세요.\n"); return
        }
        guard let executable = Bundle.main.executableURL else { status = "실행 파일 경로 오류"; return }
        let process = Process()
        process.executableURL = executable.deletingLastPathComponent().appendingPathComponent("ResourceSplitter-cli")
        process.arguments = [operation.rawValue, folder]
        process.currentDirectoryURL = URL(fileURLWithPath: folder)
        let stdout = Pipe(), stderr = Pipe()
        process.standardOutput = stdout; process.standardError = stderr
        let selected = operation
        busy = true; status = "처리 중…"; append("\n\(selected.title) 시작 · \(folder)\n")
        worker = process
        // Drain both pipes concurrently so large logs cannot block the child.
        let group = DispatchGroup()
        for pipe in [stdout, stderr] {
            group.enter()
            DispatchQueue.global(qos: .utility).async {
                let data = pipe.fileHandleForReading.readDataToEndOfFile()
                let text = String(decoding: data, as: UTF8.self)
                DispatchQueue.main.async { self.append(text) }
                group.leave()
            }
        }
        process.terminationHandler = { finished in
            group.notify(queue: .main) {
                self.busy = false; self.worker = nil
                self.refreshInstallation()
                self.status = finished.terminationStatus == 0 ? "\(selected.title) 완료" : "작업 실패 · 기록을 확인해 주세요"
                if finished.terminationStatus == 0 && selected == .automate {
                    self.append("\n최초 설치 후 실행할 명령:\ngit add --renormalize .\ngit add .gitattributes .gitignore .resourcesplitter/ResourceSplitter-osx-*\ngit commit -m \"Enable automatic resource splitting\"\n")
                }
            }
        }
        do { try process.run() }
        catch {
            // Closing the writers also releases readers if process launch failed.
            try? stdout.fileHandleForWriting.close(); try? stderr.fileHandleForWriting.close()
            worker = nil; busy = false; status = "작업 실패"; append("\(error.localizedDescription)\n")
        }
    }
}

struct ContentView: View {
    @ObservedObject var model: Model
    private let ink = Color(red: 0.08, green: 0.13, blue: 0.23)
    var body: some View {
        HStack(spacing: 0) {
            VStack(alignment: .leading, spacing: 16) {
                if let url = Bundle.main.url(forResource: "AppIcon", withExtension: "png"), let icon = NSImage(contentsOf: url) {
                    Image(nsImage: icon).resizable().frame(width: 64, height: 64)
                }
                Text("RESOURCE\nSPLITTER").font(.system(size: 18, weight: .bold)).foregroundStyle(.white)
                Text("파일은 가볍게.\n작업은 그대로.").foregroundStyle(.white.opacity(0.6)).lineSpacing(6)
                Text("리소스 관리").fontWeight(.semibold).foregroundStyle(.white).padding(14).frame(maxWidth: .infinity, alignment: .leading).background(.white.opacity(0.1)).cornerRadius(8)
                Spacer()
                Text("50 MiB 초과 파일\n49 MiB 단위로 분할\nSHA-256 무결성 검증").font(.caption).lineSpacing(7).foregroundStyle(.white.opacity(0.65))
                Text("macOS / LOCAL FIRST").font(.system(size: 10)).foregroundStyle(.white.opacity(0.4))
            }.padding(24).frame(width: 160).frame(maxHeight: .infinity).background(ink)
            VStack(alignment: .leading, spacing: 18) {
                Text("리소스 관리").font(.system(size: 27, weight: .bold))
                Text("큰 파일을 나누고, 필요할 때 원본으로 되돌리세요.").foregroundStyle(.secondary)
                VStack(alignment: .leading, spacing: 12) {
                    Text("프로젝트 폴더").fontWeight(.semibold)
                    HStack {
                        TextField("프로젝트 경로", text: $model.folder).textFieldStyle(.roundedBorder)
                        Button("폴더 선택") { model.choose() }
                    }
                }.padding(20).background(.white).cornerRadius(12).disabled(model.busy)
                Text("작업 선택").fontWeight(.semibold)
                HStack(spacing: 10) {
                    ForEach(Operation.allCases, id: \.self) { operation in
                        Button { model.operation = operation } label: {
                            VStack(alignment: .leading, spacing: 12) {
                                HStack {
                                    Text(operation.title).font(.system(size: 17, weight: .semibold))
                                    if operation == .automate { Text("권장").font(.caption2.bold()).padding(4).background(Color.purple.opacity(0.12)).cornerRadius(4) }
                                }
                                Text(operation.caption).font(.caption).foregroundStyle(.secondary).frame(height: 34, alignment: .topLeading)
                            }.frame(maxWidth: .infinity, alignment: .leading).padding(18)
                                .background(model.operation == operation ? Color.blue.opacity(0.06) : .white)
                                .cornerRadius(12).overlay(RoundedRectangle(cornerRadius: 12).stroke(model.operation == operation ? Color.blue : Color.gray.opacity(0.18)))
                        }.buttonStyle(.plain)
                    }
                }.disabled(model.busy)
                Text(model.operation == .automate ? model.installationText : "작업 전 에디터를 닫으세요. 분할 상태에서는 먼저 원본을 복원하세요.")
                    .font(.caption).foregroundStyle(.secondary).padding(12).frame(maxWidth: .infinity, alignment: .leading).background(Color.blue.opacity(0.04)).cornerRadius(8)
                HStack {
                    Text(model.status).fontWeight(.semibold)
                    if model.busy { ProgressView().controlSize(.small) }
                    Spacer()
                    Button((model.operation == .automate && model.installation == "installed" ? "자동화 업데이트 / 재설치" : model.operation.action) + "  →") { model.execute() }.buttonStyle(.borderedProminent).controlSize(.large).disabled(model.busy)
                }
                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        Text("작업 기록").fontWeight(.semibold)
                        Spacer()
                        Button("로그 복사") { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(model.log, forType: .string) }.controlSize(.small)
                    }
                    ScrollView { Text(model.log).font(.system(size: 11, design: .monospaced)).foregroundStyle(.secondary).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
                }.padding(18).frame(maxHeight: .infinity).background(.white).cornerRadius(12)
            }.padding(28).frame(minWidth: 670, minHeight: 680).background(Color(red: 0.96, green: 0.97, blue: 0.99))
        }.foregroundStyle(ink).onAppear { model.refreshInstallation() }.onChange(of: model.folder) { _ in model.refreshInstallation() }.onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in model.refreshInstallation() }
    }
}

@main struct ResourceSplitterMac: App {
    @StateObject private var model = Model()
    var body: some Scene {
        WindowGroup("Resource Splitter") { ContentView(model: model) }
            .defaultSize(width: 1000, height: 760)
    }
}
