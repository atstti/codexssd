import AppKit

// Render the vector artwork at each native icon size, without external assets.
let output = CommandLine.arguments[1]
try FileManager.default.createDirectory(atPath: output, withIntermediateDirectories: true)

func color(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat) -> NSColor {
    NSColor(srgbRed: r, green: g, blue: b, alpha: 1)
}

func render(pixels: Int, name: String) throws {
    let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels,
        bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
        colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState()
    let context = NSGraphicsContext(bitmapImageRep: bitmap)!
    NSGraphicsContext.current = context
    context.cgContext.scaleBy(x: CGFloat(pixels) / 1024, y: CGFloat(pixels) / 1024)

    let background = NSBezierPath(roundedRect: NSRect(x: 94, y: 94, width: 836, height: 836), xRadius: 190, yRadius: 190)
    NSGradient(starting: color(0.10, 0.18, 0.31), ending: color(0.025, 0.045, 0.10))!.draw(in: background, angle: -90)

    let drive = NSBezierPath(roundedRect: NSRect(x: 238, y: 256, width: 548, height: 508), xRadius: 94, yRadius: 94)
    NSGraphicsContext.saveGraphicsState()
    let shadow = NSShadow()
    shadow.shadowColor = NSColor.black.withAlphaComponent(0.45)
    shadow.shadowBlurRadius = 36
    shadow.shadowOffset = NSSize(width: 0, height: -16)
    shadow.set()
    NSGradient(starting: color(0.73, 0.83, 0.94), ending: color(0.28, 0.39, 0.54))!.draw(in: drive, angle: -90)
    NSGraphicsContext.restoreGraphicsState()

    let face = NSBezierPath(roundedRect: NSRect(x: 260, y: 300, width: 504, height: 440), xRadius: 76, yRadius: 76)
    NSGradient(starting: color(0.12, 0.21, 0.34), ending: color(0.055, 0.10, 0.18))!.draw(in: face, angle: -90)

    let center = NSPoint(x: 512, y: 540)
    let track = NSBezierPath()
    track.appendArc(withCenter: center, radius: 131, startAngle: -90, endAngle: 270)
    track.lineWidth = 36
    color(0.20, 0.29, 0.39).setStroke()
    track.stroke()
    let gauge = NSBezierPath()
    gauge.appendArc(withCenter: center, radius: 131, startAngle: 90, endAngle: -172, clockwise: true)
    gauge.lineWidth = 36
    gauge.lineCapStyle = .round
    color(0.20, 0.90, 0.84).setStroke()
    gauge.stroke()

    // A central disk spindle gives the gauge a hardware identity.
    color(0.80, 0.91, 0.98).setFill()
    NSBezierPath(ovalIn: NSRect(x: 478, y: 506, width: 68, height: 68)).fill()
    color(0.10, 0.18, 0.29).setFill()
    NSBezierPath(ovalIn: NSRect(x: 499, y: 527, width: 26, height: 26)).fill()

    color(0.15, 0.24, 0.35).setFill()
    NSBezierPath(roundedRect: NSRect(x: 300, y: 268, width: 424, height: 16), xRadius: 8, yRadius: 8).fill()
    color(0.20, 0.90, 0.84).setFill()
    NSBezierPath(roundedRect: NSRect(x: 568, y: 334, width: 112, height: 12), xRadius: 6, yRadius: 6).fill()
    NSGraphicsContext.restoreGraphicsState()
    try bitmap.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: output).appendingPathComponent(name))
}

for size in [16, 32, 128, 256, 512] {
    try render(pixels: size, name: "icon_\(size)x\(size).png")
    try render(pixels: size * 2, name: "icon_\(size)x\(size)@2x.png")
}
