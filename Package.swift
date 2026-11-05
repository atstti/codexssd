// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "DiskSpace",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "DiskSpace", targets: ["DiskSpace"])],
    targets: [
        .target(name: "DiskSpaceCore"),
        .executableTarget(name: "DiskSpace", dependencies: ["DiskSpaceCore"]),
        .testTarget(name: "DiskSpaceCoreTests", dependencies: ["DiskSpaceCore"])
    ]
)
