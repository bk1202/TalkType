import Testing
@testable import LockInCore

@Test func removesOnlyStandaloneFillers() {
    #expect(TranscriptCleaner.clean("Um, this is the result.") == "this is the result.")
    #expect(TranscriptCleaner.clean("I visited UMass.") == "I visited UMass.")
}
