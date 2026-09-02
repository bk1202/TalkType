// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "TalkTypeCore",
    platforms: [.macOS(.v14), .iOS(.v17)],
    products: [.library(name: "TalkTypeCore", targets: ["TalkTypeCore"])],
    targets: [
        .target(name: "TalkTypeCore"),
        .testTarget(name: "TalkTypeCoreTests", dependencies: ["TalkTypeCore"])
    ]
)
