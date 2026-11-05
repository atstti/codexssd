import Foundation

public enum Capacity {
    public static func bytes(blocks: UInt64, blockSize: UInt64) -> UInt64? {
        let result = blocks.multipliedReportingOverflow(by: blockSize)
        return result.overflow ? nil : result.partialValue
    }

    public static func display(_ bytes: UInt64?) -> String {
        guard let bytes else { return "取得できません" }
        return String(format: "%.2f GB", locale: Locale(identifier: "en_US_POSIX"), Double(bytes) / 1_000_000_000)
    }
}
