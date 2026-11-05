import AppKit
import Darwin
import DiskSpaceCore

struct Volume: Identifiable {
    let id: String
    let name: String
    let path: String
    let measuredAt: Date
    let total: UInt64?
    let available: UInt64?
    let important: UInt64?
    let isStartup: Bool

    var comparison: String {
        func raw(_ value: UInt64?) -> String { value.map(String.init) ?? "取得できません" }
        return """
        DiskSpace 比較情報
        ボリューム名: \(name)
        測定対象のパス: \(path)
        測定時刻: \(ISO8601DateFormatter().string(from: measuredAt))
        総容量 (statfs f_blocks × f_bsize): \(raw(total)) bytes
        現在の空き (statfs f_bavail × f_bsize): \(raw(available)) bytes
        重要なファイル用（推定）(volumeAvailableCapacityForImportantUsage): \(raw(important)) bytes
        表示単位: 1 GB = 1,000,000,000 bytes
        Finderの空き領域・パージ可能容量との一致は保証しません。
        """
    }
}

enum VolumeReader {
    static let startupID = "startup"

    static func read() -> [Volume] {
        let keys: Set<URLResourceKey> = [.volumeNameKey, .volumeIsLocalKey,
            .volumeIsInternalKey, .volumeIsRemovableKey, .volumeIsEjectableKey,
            .volumeIsBrowsableKey]
        let root = URL(fileURLWithPath: "/")
        let rootName = (try? root.resourceValues(forKeys: [.volumeNameKey]))?.volumeName ?? "起動ディスク"
        // Modern macOS separates the read-only system from writable user data.
        let dataPath = "/System/Volumes/Data"
        let startupPath = FileManager.default.fileExists(atPath: dataPath) ? dataPath : "/"
        var result = [measure(id: startupID, name: "\(rootName)（起動ディスク）", path: startupPath, startup: true)]
        let mounts = FileManager.default.mountedVolumeURLs(includingResourceValuesForKeys: Array(keys), options: [.skipHiddenVolumes]) ?? []
        var devices = Set<String>()
        if let device = deviceID(startupPath) { devices.insert(device) }
        for url in mounts.sorted(by: { $0.path < $1.path }) {
            let path = url.path
            // Never expose macOS system/helper mounts as user disks.
            guard path != "/", !path.hasPrefix("/System/"), !path.hasPrefix("/private/"),
                  let values = try? url.resourceValues(forKeys: keys),
                  values.volumeIsLocal == true, values.volumeIsBrowsable != false,
                  let device = deviceID(path), !devices.contains(device) else { continue }
            var info = statfs()
            guard statfs(path, &info) == 0 else { continue }
            let fs = withUnsafeBytes(of: info.f_fstypename) { bytes in
                String(decoding: bytes.prefix(while: { $0 != 0 }), as: UTF8.self)
            }
            // Include common disk filesystems; omit pseudo filesystems.
            guard ["apfs", "hfs", "exfat", "msdos", "ntfs"].contains(fs) else { continue }
            let name = values.volumeName ?? url.lastPathComponent
            guard !["Preboot", "Recovery", "VM", "Update", "xarts", "iSCPreboot", "Hardware"].contains(name) else { continue }
            devices.insert(device)
            result.append(measure(id: device, name: name, path: path, startup: false))
        }
        return result
    }

    private static func deviceID(_ path: String) -> String? {
        var info = statfs()
        guard statfs(path, &info) == 0 else { return nil }
        return withUnsafeBytes(of: info.f_mntfromname) { bytes in
            String(decoding: bytes.prefix(while: { $0 != 0 }), as: UTF8.self)
        }
    }

    private static func measure(id: String, name: String, path: String, startup: Bool) -> Volume {
        var info = statfs()
        let success = statfs(path, &info) == 0
        let blockSize = success ? UInt64(info.f_bsize) : 0
        let values = try? URL(fileURLWithPath: path).resourceValues(forKeys: [.volumeAvailableCapacityForImportantUsageKey])
        let important = values?.volumeAvailableCapacityForImportantUsage.flatMap { $0 >= 0 ? UInt64($0) : nil }
        return Volume(id: id, name: name, path: path, measuredAt: Date(),
            total: success ? Capacity.bytes(blocks: info.f_blocks, blockSize: blockSize) : nil,
            available: success ? Capacity.bytes(blocks: info.f_bavail, blockSize: blockSize) : nil,
            important: important, isStartup: startup)
    }
}
