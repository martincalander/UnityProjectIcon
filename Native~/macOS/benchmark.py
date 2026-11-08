#!/usr/bin/env python3
"""Compare the shipped bundle with isolated native experiments. Never overwrite it."""

import argparse
import json
from pathlib import Path
import platform
import subprocess
import tempfile


def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--variants", help="Comma-separated names; defaults to all experiments.")
    parser.add_argument("--capture", type=Path, help="Optional directory for canonical RGBA comparison images.")
    args = parser.parse_args()
    if args.capture:
        args.capture.mkdir(parents=True, exist_ok=True)
    root = Path(__file__).resolve().parent
    package = root.parent.parent
    source = (root / "ProjectIcon.mm").read_text()
    mipmaps = """CGImageRef raster = [applicationIcon CGImageForProposedRect:nullptr context:nil hints:nil];
        NSImage *multiple = [[NSImage alloc] initWithSize:applicationIcon.size];
        CGColorSpaceRef colors = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
        for (size_t size = 16; size < CGImageGetWidth(raster); size *= 2)
        {
            CGContextRef bitmap = CGBitmapContextCreate(nullptr, size, size, 8, size * 4, colors,
                kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big);
            CGContextSetInterpolationQuality(bitmap, kCGInterpolationHigh);
            CGContextDrawImage(bitmap, CGRectMake(0, 0, size, size), raster);
            CGImageRef reduced = CGBitmapContextCreateImage(bitmap);
            NSBitmapImageRep *representation = [[NSBitmapImageRep alloc] initWithCGImage:reduced];
            representation.size = applicationIcon.size;
            [multiple addRepresentation:representation];
            CGImageRelease(reduced);
            CGContextRelease(bitmap);
        }
        NSBitmapImageRep *full = [[NSBitmapImageRep alloc] initWithCGImage:raster];
        full.size = applicationIcon.size;
        [multiple addRepresentation:full];
        CGColorSpaceRelease(colors);
        applicationIcon = multiple;
        // Drawing may decode lazily."""
    variants = [
        ("multiple-resolutions", source.replace("// Drawing may decode lazily.", mipmaps), "-O2"),
        ("O0", source, "-O0"),
        ("O2", source, "-O2"),
        ("O3", source, "-O3"),
        ("Os", source, "-Os"),
        ("O2-lto", source, "-O2", "-flto"),
        ("canvas512", source.replace("canvasSize = 1024.0", "canvasSize = 512.0"), "-O2"),
        ("canvas256", source.replace("canvasSize = 1024.0", "canvasSize = 256.0"), "-O2"),
        ("replacement-source-image", source.replace(
            "applicationIcon = ProjectIconCreateReplacementApplicationIcon(sourceImage);",
            "applicationIcon = sourceImage;"), "-O2"),
        ("skip-unchanged-setter", source.replace(
            "if (!ProjectIconApplyPinnedApplicationIconOnMainThread())\n                return 0;",
            "// Experimental: omit immediate reassertion on cache hits."), "-O2"),
        ("no-deferred-retries", source.replace(
            "if (s_pinnedApplicationIcon == nil || s_deferredReapplyPending)",
            "if (YES || s_pinnedApplicationIcon == nil || s_deferredReapplyPending)"), "-O2"),
        ("coalesced-notifications", source.replace(
            "ProjectIconApplyPinnedApplicationIconOnMainThread();\n        ProjectIconApplyPinnedApplicationIconDeferred();",
            "if (!s_deferredReapplyPending)\n            ProjectIconApplyPinnedApplicationIconOnMainThread();\n"
            "        ProjectIconApplyPinnedApplicationIconDeferred();"), "-O2"),
        ("cache-always", source.replace(
            "// Drawing may decode lazily.",
            "applicationIcon.cacheMode = NSImageCacheAlways;\n\n        // Drawing may decode lazily."), "-O2"),
        ("bitmap-representation", source.replace(
            "// Drawing may decode lazily.",
            "CGImageRef raster = [applicationIcon CGImageForProposedRect:nullptr context:nil hints:nil];\n"
            "        if (raster != nullptr)\n"
            "            applicationIcon = [[NSImage alloc] initWithCGImage:raster size:applicationIcon.size];\n\n"
            "        // Drawing may decode lazily."), "-O2"),
        ("tiff-representation", source.replace(
            "// Drawing may decode lazily.",
            "applicationIcon = [[NSImage alloc] initWithData:applicationIcon.TIFFRepresentation];\n\n"
            "        // Drawing may decode lazily."), "-O2"),
        ("rgba8-representation", source.replace(
            "// Drawing may decode lazily.",
            "CGImageRef raster = [applicationIcon CGImageForProposedRect:nullptr context:nil hints:nil];\n"
            "        size_t width = CGImageGetWidth(raster), height = CGImageGetHeight(raster);\n"
            "        CGColorSpaceRef colors = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);\n"
            "        CGContextRef bitmap = CGBitmapContextCreate(nullptr, width, height, 8, width * 4, colors, kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big);\n"
            "        CGContextDrawImage(bitmap, CGRectMake(0, 0, width, height), raster);\n"
            "        CGImageRef flattened = CGBitmapContextCreateImage(bitmap);\n"
            "        applicationIcon = [[NSImage alloc] initWithCGImage:flattened size:applicationIcon.size];\n"
            "        CGImageRelease(flattened); CGContextRelease(bitmap); CGColorSpaceRelease(colors);\n\n"
            "        // Drawing may decode lazily."), "-O2"),
    ]
    reports = []
    with tempfile.TemporaryDirectory(prefix="project-icon-benchmark-") as temporary:
        temp = Path(temporary)
        runner = temp / "benchmark"
        run("clang++", "-O2", "-std=c++17", "-fobjc-arc", "-framework", "Cocoa",
            str(root / "benchmarks.mm"), "-o", str(runner))
        shipped = package / "Editor/Plugins/macOS/ProjectIcon.bundle/Contents/MacOS/ProjectIcon"
        selected = set(args.variants.split(",")) if args.variants else None
        known = {"shipped"} | {entry[0] for entry in variants}
        if selected and selected - known:
            parser.error("Unknown variants: " + ", ".join(sorted(selected - known)))
        binaries = [("shipped", shipped)] if not selected or "shipped" in selected else []
        for name, code, *flags in variants:
            if selected and name not in selected:
                continue
            variant_source = temp / (name + ".mm")
            variant_source.write_text(code)
            binary = temp / (name + ".bundle")
            run("clang++", "-bundle", *flags, "-fobjc-arc", "-framework", "Cocoa",
                "-arch", platform.machine(), str(variant_source), "-o", str(binary))
            binaries.append((name, binary))
        # Reverse the second pass to expose order and warm-cache effects.
        for repetition in range(2):
            for name, binary in binaries if repetition == 0 else reversed(binaries):
                print(f"Pass {repetition + 1}: {name}", flush=True)
                extra = [str(args.capture.resolve())] if args.capture else []
                result = run(str(runner), str(binary), str(package / "ProjectIcon.png"), name, *extra,
                             capture_output=True)
                report = json.loads(result.stdout)
                report["repetition"] = repetition + 1
                report["binaryBytes"] = binary.stat().st_size
                reports.append(report)
                args.output.parent.mkdir(parents=True, exist_ok=True)
                args.output.write_text(json.dumps(reports, indent=2) + "\n")


if __name__ == "__main__":
    main()
