import CoreGraphics
import CoreMedia
import CoreVideo
import Foundation
import ScreenCaptureKit

private final class FrameOutput: NSObject, SCStreamOutput {
    private let lock = NSLock()
    private var latestFrame: CVPixelBuffer?

    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .screen, let image = CMSampleBufferGetImageBuffer(sampleBuffer) else {
            return
        }

        lock.lock()
        latestFrame = image
        lock.unlock()
    }

    func copyLatestFrame(to destination: UnsafeMutableRawPointer?, capacity: Int, rowBytes: Int,
                         cropX: Int, cropY: Int, cropWidth: Int, cropHeight: Int) -> Int32 {
        lock.lock()
        defer { lock.unlock() }
        guard let frame = latestFrame else {
            return 1
        }

        let frameWidth = CVPixelBufferGetWidth(frame)
        let frameHeight = CVPixelBufferGetHeight(frame)
        let bytesPerRow = CVPixelBufferGetBytesPerRow(frame)
        guard cropX >= 0, cropY >= 0, cropWidth > 0, cropHeight > 0,
              cropX + cropWidth <= frameWidth, cropY + cropHeight <= frameHeight else {
            return -1
        }
        guard let destination, rowBytes >= cropWidth * 4, capacity >= rowBytes * cropHeight else {
            return 2
        }
        guard CVPixelBufferLockBaseAddress(frame, .readOnly) == kCVReturnSuccess,
              let baseAddress = CVPixelBufferGetBaseAddress(frame) else {
            return -1
        }
        defer { CVPixelBufferUnlockBaseAddress(frame, .readOnly) }

        let copyBytes = cropWidth * 4
        for row in 0..<cropHeight {
            let sourceOffset = (cropY + row) * bytesPerRow + cropX * 4
            memcpy(destination.advanced(by: row * rowBytes), baseAddress.advanced(by: sourceOffset), copyBytes)
        }
        return 0
    }
}

private final class CaptureContext {
    let stream: SCStream
    let output: FrameOutput
    let displayWidth: Int32
    let displayHeight: Int32

    init(stream: SCStream, output: FrameOutput, displayWidth: Int32, displayHeight: Int32) {
        self.stream = stream
        self.output = output
        self.displayWidth = displayWidth
        self.displayHeight = displayHeight
    }
}

@_cdecl("pixelpointer_sck_create")
public func pixelpointer_sck_create(_ errorCode: UnsafeMutablePointer<Int32>?,
                                    _ displayWidth: UnsafeMutablePointer<Int32>?,
                                    _ displayHeight: UnsafeMutablePointer<Int32>?) -> UnsafeMutableRawPointer? {
    func fail(_ code: Int32) -> UnsafeMutableRawPointer? {
        errorCode?.pointee = code
        return nil
    }

    let contentSemaphore = DispatchSemaphore(value: 0)
    var content: SCShareableContent?
    SCShareableContent.getExcludingDesktopWindows(false, onScreenWindowsOnly: false) { result, _ in
        content = result
        contentSemaphore.signal()
    }
        guard let displayWidth, let displayHeight,
            contentSemaphore.wait(timeout: .now() + 10) == .success,
          let shareableContent = content,
          let display = shareableContent.displays.first(where: { $0.displayID == CGMainDisplayID() }) else {
        return fail(1)
    }

    let processId = ProcessInfo.processInfo.processIdentifier
    guard let ownApplication = shareableContent.applications.first(where: { $0.processID == processId }) else {
        return fail(4)
    }
    let filter = SCContentFilter(display: display, excludingApplications: [ownApplication], exceptingWindows: [])
    let configuration = SCStreamConfiguration()
    configuration.width = display.width
    configuration.height = display.height
    configuration.pixelFormat = kCVPixelFormatType_32BGRA
    configuration.queueDepth = 3
    configuration.showsCursor = false
    configuration.capturesAudio = false

    let output = FrameOutput()
    let stream = SCStream(filter: filter, configuration: configuration, delegate: nil)
    let frameQueue = DispatchQueue(label: "io.github.lzocateli.PixelPointer.screen-capture")
    do {
        try stream.addStreamOutput(output, type: .screen, sampleHandlerQueue: frameQueue)
    } catch {
        return fail(2)
    }

    let startSemaphore = DispatchSemaphore(value: 0)
    var startSucceeded = false
    stream.startCapture { error in
        startSucceeded = error == nil
        startSemaphore.signal()
    }
    guard startSemaphore.wait(timeout: .now() + 10) == .success, startSucceeded else {
        return fail(3)
    }

    errorCode?.pointee = 0
    displayWidth.pointee = Int32(display.width)
    displayHeight.pointee = Int32(display.height)
    return Unmanaged.passRetained(CaptureContext(stream: stream, output: output,
                                                  displayWidth: Int32(display.width),
                                                  displayHeight: Int32(display.height))).toOpaque()
}

@_cdecl("pixelpointer_sck_copy_frame")
public func pixelpointer_sck_copy_frame(_ handle: UnsafeMutableRawPointer?, _ destination: UnsafeMutableRawPointer?,
                                         _ capacity: Int32, _ rowBytes: Int32, _ cropX: Int32, _ cropY: Int32,
                                         _ cropWidth: Int32, _ cropHeight: Int32) -> Int32 {
    guard let handle else {
        return -1
    }
    let context = Unmanaged<CaptureContext>.fromOpaque(handle).takeUnretainedValue()
    return context.output.copyLatestFrame(to: destination, capacity: Int(capacity), rowBytes: Int(rowBytes),
                                          cropX: Int(cropX), cropY: Int(cropY), cropWidth: Int(cropWidth),
                                          cropHeight: Int(cropHeight))
}

@_cdecl("pixelpointer_sck_destroy")
public func pixelpointer_sck_destroy(_ handle: UnsafeMutableRawPointer?) {
    guard let handle else {
        return
    }
    let context = Unmanaged<CaptureContext>.fromOpaque(handle).takeRetainedValue()
    let stopSemaphore = DispatchSemaphore(value: 0)
    context.stream.stopCapture { _ in
        stopSemaphore.signal()
    }
    _ = stopSemaphore.wait(timeout: .now() + 5)
}