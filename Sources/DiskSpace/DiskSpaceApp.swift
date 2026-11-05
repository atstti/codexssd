import AppKit
import SwiftUI
import DiskSpaceCore

@main
struct DiskSpaceApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    var body: some Scene {
        Settings { EmptyView() }
    }
}

final class DiskModel: ObservableObject {
    @Published var volumes: [Volume] = []
    @Published var selectedID = VolumeReader.startupID
    @Published var refreshing = false
    private var refreshPending = false
    var didChange: (() -> Void)?
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
                self.didChange?()
                if self.refreshPending {
                    self.refreshPending = false
                    self.refresh()
                }
            }
        }
    }

    func select(_ volume: Volume) {
        selectedID = volume.id
        didChange?()
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var item: NSStatusItem!
    private let popover = NSPopover()
    private let model = DiskModel()
    private var timer: Timer?
    private var observers: [NSObjectProtocol] = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.button?.image = NSImage(systemSymbolName: "internaldrive", accessibilityDescription: "DiskSpace")
        item.button?.image?.isTemplate = true
        item.button?.imagePosition = .imageLeading
        item.button?.title = "残り（推定）取得中…"
        item.button?.target = self
        item.button?.action = #selector(toggle)
        popover.behavior = .transient
        popover.contentSize = NSSize(width: 430, height: 540)
        popover.contentViewController = NSHostingController(rootView: DiskView(model: model))
        model.didChange = { [weak self] in
            guard let self else { return }
            self.item.button?.title = "残り（推定）\(Capacity.display(self.model.selected?.important))"
            self.item.button?.toolTip = self.model.selected.map { "\($0.name) — \($0.path)\n重要なファイル用（推定）" }
        }
        timer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in self?.model.refresh() }
        for name in [NSWorkspace.didMountNotification, NSWorkspace.didUnmountNotification, NSWorkspace.didRenameVolumeNotification, NSWorkspace.didWakeNotification] {
            observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                self?.model.refresh()
            })
        }
        model.refresh()
    }

    @objc private func toggle() {
        if popover.isShown { popover.performClose(nil) }
        else if let button = item.button {
            model.refresh()
            NSApp.activate(ignoringOtherApps: true)
            popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        }
    }
}

struct DiskView: View {
    @ObservedObject var model: DiskModel
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("DiskSpace").font(.title2.bold())
            Text("メニューバーの対象：\(model.selected?.name ?? "取得中…")")
                .font(.callout).textSelection(.enabled)
            Text("表示する容量：重要なファイル用（推定）")
                .font(.caption).foregroundColor(.secondary)
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    ForEach(model.volumes) { volume in
                        VStack(alignment: .leading, spacing: 6) {
                            HStack {
                                Text(volume.name).font(.headline)
                                Spacer()
                                Button(model.selectedID == volume.id ? "選択中" : "表示する") { model.select(volume) }
                                    .disabled(model.selectedID == volume.id)
                            }
                            Text(volume.path).font(.caption).foregroundColor(.secondary).textSelection(.enabled)
                            row("総容量", volume.total)
                            row("現在の空き", volume.available)
                            row("重要なファイル用（推定）", volume.important)
                            Text("測定：\(volume.measuredAt.formatted(date: .omitted, time: .standard))")
                                .font(.caption).foregroundColor(.secondary)
                            Button("比較情報をコピー") {
                                NSPasteboard.general.clearContents()
                                NSPasteboard.general.setString(volume.comparison, forType: .string)
                            }
                        }
                        Divider()
                    }
                }
            }
            Text("現在の空き：statfs の利用可能ブロック数から計算。\n重要なファイル用：OSが返す推定値で、Finderの空き領域やパージ可能容量そのものではありません。\n1 GB = 1,000,000,000 バイト。30秒ごとに更新。")
                .font(.caption).foregroundColor(.secondary)
            HStack {
                Button(model.refreshing ? "更新中…" : "手動更新") { model.refresh() }.disabled(model.refreshing)
                Spacer()
                Button("終了") { NSApp.terminate(nil) }
            }
        }
        .padding(16).frame(width: 430, height: 540)
    }

    private func row(_ label: String, _ value: UInt64?) -> some View {
        HStack { Text(label); Spacer(); Text(Capacity.display(value)).monospacedDigit() }
    }
}
