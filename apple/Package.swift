// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "TalkTypeCore",
    platforms: [.macOS(.v14), .iOS(.v17)],
    products: [.library(name: "TalkTypeCore", targets: ["LockInCore"])],
    targets: [
        .target(name: "LockInCore"),
        .testTarget(name: "LockInCoreTests", dependencies: ["LockInCore"])
    ]
)
