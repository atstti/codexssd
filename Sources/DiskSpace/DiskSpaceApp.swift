import AppKit
import SwiftUI
import DiskSpaceCore

@main
enum DiskSpaceApp {
    static func main() {
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        app.setActivationPolicy(.regular)
        withExtendedLifetime(delegate) { app.run() }
    }
}

final class DiskModel: ObservableObject {
    @Published var volumes: [Volume] = []
    @Published var selectedID = VolumeReader.startupID
    @Published var refreshing = false
    private var refreshPending = false
    var selected: Volume? { volumes.first { $0.id == selectedID } }

    func refresh() {
        guard !refreshing else {
            refreshPending = true
            return
        }
        refreshing = true
        DispatchQueue.global(qos: .utility).async {
            let volumes = VolumeReader.read()
            DispatchQueue.main.async {
                self.volumes = volumes
                if !volumes.contains(where: { $0.id == self.selectedID }) {
                    self.selectedID = VolumeReader.startupID
                }
                self.refreshing = false
                if self.refreshPending {
                    self.refreshPending = false
                    self.refresh()
                }
            }
        }
    }

    func select(_ volume: Volume) {
        selectedID = volume.id
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let model = DiskModel()
    private lazy var panel = makePanel()
    private var timer: Timer?
    private var observers: [NSObjectProtocol] = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "DiskSpaceを終了", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let menuItem = NSMenuItem()
        menuItem.submenu = appMenu
        NSApp.mainMenu = NSMenu()
        NSApp.mainMenu?.addItem(menuItem)

        let refreshTimer = Timer(timeInterval: 30, repeats: true) { [weak self] _ in self?.model.refresh() }
        RunLoop.main.add(refreshTimer, forMode: .common)
        timer = refreshTimer
        for name in [NSWorkspace.didMountNotification, NSWorkspace.didUnmountNotification, NSWorkspace.didRenameVolumeNotification, NSWorkspace.didWakeNotification] {
            observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                self?.model.refresh()
            })
        }
        showPanel()
        if CommandLine.arguments.contains("--smoke-test") {
            DispatchQueue.main.async { self.runSmokeTest() }
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showPanel()
        return false
    }

    private func showPanel() {
        if panel.isMiniaturized { panel.deminiaturize(nil) }
        panel.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        model.refresh()
    }

    private func makePanel() -> NSWindow {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 460, height: 500),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.title = "DiskSpace"
        window.appearance = NSAppearance(named: .darkAqua)
        window.backgroundColor = NSColor(srgbRed: 0.07, green: 0.12, blue: 0.21, alpha: 1)
        window.isReleasedWhenClosed = false
        window.center()
        window.contentViewController = NSHostingController(rootView: DiskView(
            model: model,
            setOpacity: { [weak window] in window?.alphaValue = $0 },
            setAlwaysOnTop: { [weak window] in window?.level = $0 ? .floating : .normal }
        ))
        window.contentMinSize = NSSize(width: 360, height: 330)
        return window
    }

    private func runSmokeTest() {
        precondition(panel.isVisible && panel.styleMask.contains(.resizable))
        panel.setContentSize(NSSize(width: 380, height: 350))
        precondition(panel.contentView?.bounds.size == NSSize(width: 380, height: 350))
        panel.close()
        precondition(!panel.isVisible)
        _ = applicationShouldHandleReopen(NSApp, hasVisibleWindows: false)
        precondition(panel.isVisible)
        print("PASS: panel launch, resize, close and reopen")
        NSApp.terminate(nil)
    }
}

struct DiskView: View {
    @ObservedObject var model: DiskModel
    let setOpacity: (Double) -> Void
    let setAlwaysOnTop: (Bool) -> Void
    @State private var opacity = 1.0
    @State private var alwaysOnTop = false

    private let navy = Color(red: 0.07, green: 0.12, blue: 0.21)
    private let surface = Color(red: 0.10, green: 0.17, blue: 0.28)
    private let turquoise = Color(red: 0.18, green: 0.87, blue: 0.82)

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Picker("ドライブ", selection: $model.selectedID) {
                    if model.volumes.isEmpty {
                        Text(model.refreshing ? "取得中…" : "取得できません").tag(model.selectedID)
                    }
                    ForEach(model.volumes) { volume in
                        Text(volume.name).tag(volume.id)
                    }
                }
                .disabled(model.volumes.isEmpty)
                Spacer()
                Toggle("最前面", isOn: $alwaysOnTop)
                    .toggleStyle(.checkbox)
                    .onChange(of: alwaysOnTop, perform: setAlwaysOnTop)
            }

            VStack(alignment: .leading, spacing: 5) {
                Label("重要なファイル用（推定）", systemImage: "internaldrive")
                    .font(.subheadline)
                Text(model.selected == nil && model.refreshing ? "取得中…" : Capacity.display(model.selected?.important))
                    .font(.system(size: 46, weight: .semibold, design: .rounded))
                    .monospacedDigit()
                    .lineLimit(1)
                    .minimumScaleFactor(0.55)
                    .accessibilityLabel("重要なファイル用（推定）")
                    .accessibilityValue(model.selected == nil && model.refreshing ? "取得中…" : Capacity.display(model.selected?.important))
            }
            .foregroundStyle(turquoise)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(14)
            .background(surface, in: RoundedRectangle(cornerRadius: 12))

            ScrollView {
                VStack(alignment: .leading, spacing: 10) {
                    ForEach(model.volumes) { volume in
                        VStack(alignment: .leading, spacing: 6) {
                            HStack {
                                Text(volume.name).font(.headline)
                                Spacer()
                                Button(model.selectedID == volume.id ? "選択中" : "表示する") { model.select(volume) }
                                    .disabled(model.selectedID == volume.id)
                            }
                            Text(volume.path).font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
                            row("総容量", volume.total)
                            row("現在の空き", volume.available)
                            row("重要なファイル用（推定）", volume.important)
                            Text("測定：\(volume.measuredAt.formatted(date: .omitted, time: .standard))")
                                .font(.caption).foregroundStyle(.secondary)
                            Button("比較情報をコピー") {
                                NSPasteboard.general.clearContents()
                                NSPasteboard.general.setString(volume.comparison, forType: .string)
                            }
                        }
                        .padding(12)
                        .background(surface, in: RoundedRectangle(cornerRadius: 9))
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }

            HStack {
                Slider(value: $opacity, in: 0.4...1, step: 0.05) { Text("不透明度") }
                    .onChange(of: opacity, perform: setOpacity)
                Text("\(Int((opacity * 100).rounded()))%")
                    .monospacedDigit()
                    .frame(width: 38, alignment: .trailing)
                Spacer()
                Button(model.refreshing ? "更新中…" : "手動更新") { model.refresh() }
                    .disabled(model.refreshing)
            }
        }
        .padding(16)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(navy)
        .tint(turquoise)
        .environment(\.colorScheme, .dark)
    }

    private func row(_ label: String, _ value: UInt64?) -> some View {
        HStack {
            Text(label)
            Spacer()
            Text(Capacity.display(value)).monospacedDigit()
        }
    }
}
