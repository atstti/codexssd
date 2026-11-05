import XCTest
@testable import DiskSpaceCore

final class CapacityTests: XCTestCase {
    func testObservedAvailableBlocks() {
        let bytes = Capacity.bytes(blocks: 16_242_320, blockSize: 1_024)
        XCTAssertEqual(bytes, 16_632_135_680)
        XCTAssertEqual(Capacity.display(bytes), "16.63 GB")
    }

    func testZeroAndFailureAreDifferent() {
        XCTAssertEqual(Capacity.display(0), "0.00 GB")
        XCTAssertEqual(Capacity.display(nil), "取得できません")
    }

    func testDecimalUnitsAndOverflow() {
        XCTAssertEqual(Capacity.display(1_000_000_000), "1.00 GB")
        XCTAssertNil(Capacity.bytes(blocks: .max, blockSize: 2))
        XCTAssertEqual(Capacity.bytes(blocks: 0, blockSize: 4096), 0)
    }
}
