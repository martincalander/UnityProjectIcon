# Project Icon performance

Project Icon has inexpensive idle updates and effective PNG caching, but it is
not fully optimized. The main remaining cost on the tested Mac is AppKit's
application-icon setter: about 40 ms even when Project Icon reuses its composed
image. Faster native experiments either changed pixels or weakened reapplication.
They remain experiments, not package defaults.

The accepted change filters build-target enum values that Unity cannot represent
before fallback searches. It preserves active-target, general-icon and other-target
priority, including valid targets whose build modules are not installed. Actual
icon assignments and per-call failures are not cached.

The final full benchmark reduced empty-project lookup from 1.028 ms to 0.786 ms,
about 23%, and from 370,231 to 349,376 managed bytes per lookup, about 6%.
Idle cost and native apply behavior are unchanged.

## Environment and method

Measured on 2026-11-08 with an Apple M2 Max, 32 GiB memory, macOS 27.2 (26B123),
Unity 7000.0.0a16 (0745a91df592), .NET 10.0.10-dev and Metal. The CLI was
1.0.0-beta.17 and Pipeline was 0.10.0-exp.1. Native experiments used Apple clang
21.0.0. The baseline package commit was
`eee27f93c46d445ccb8674196c8901d1d485ed99`.

- Managed measurements use nine warmed batches and report the median time and
  managed bytes allocated per operation. Private methods are bound to delegates
  before timing. The cold PNG writer also includes cache invalidation by reflection.
- Native measurements use nine batches in each of two separate processes. The
  second pass reverses variant order. The tables use the median of the two process
  medians. Native calls reach the real AppKit setter; they are not stubbed.
  The standalone harness prohibits application activation; it does not measure
  perceived Dock animation or user-input latency.
- Native input is the package's 64 by 64 PNG. Both modes normally compose a
  1024-point square, which became a 2048 by 2048, 64-bit bitmap on this Retina Mac.
  A cold native measurement alternates two paths containing identical PNG data.
  It measures a composition cache miss with warm filesystem caches, not Editor
  launch or first-ever library loading.
- Generated managed textures use deterministic RGB patterns at 32, 256, 1024
  and 2048 pixels. The unreadable-texture cases include GPU readback and encoding.
- Native experiments are compiled into temporary directories. They never replace
  the shipped bundle. Their timings exclude compilation and process launch.
- These are local microbenchmarks. Other development Editors were running.
  Small differences among compiler variants are within the observed variation;
  they are not evidence of a reliable compiler win.

The shared workspace Editor was restarted during an initial managed measurement.
That incomplete run was discarded. Completed managed runs and package tests used
a separate visible Editor and an empty local project with Metal enabled. An
initial native stress harness retained too much temporary AppKit data; that run
was stopped and discarded. The recorded harness drains an autorelease pool for
each operation and each notification.

## Managed costs

Baseline medians, rounded:

| Operation | Time per operation | Managed allocation |
| --- | ---: | ---: |
| Disabled update | 0.94 microseconds | 0 B |
| Enabled idle update | 0.99 microseconds | 0 B |
| Empty-project Player icon lookup | 1.03 ms | 370 KB |
| Selection alone, assigned active target | 0.010 microseconds | 0 B |
| Selection alone, no icons, synthetic provider | 0.208 microseconds | 0 B |
| Source file existence check | 2.88 microseconds | 208 B |
| Source file metadata check | 3.06 microseconds | 456 B |
| Manual UTF-8 path preparation | 0.16 microseconds | 328 B |
| Cached generated PNG | 1.4 to 1.5 microseconds | 248 B |
| Complete warm apply | 40 to 45 ms | 576 to 1496 B |

The selector itself is cheap. Unity's PlayerSettings queries account for almost
all lookup time and allocations in an empty project. The baseline made 66
target/kind queries, 30 of which threw because 15 obsolete enum values had no
NamedBuildTarget. Filtering those targets in a prototype reduced three of four
paired candidate batches to 0.77 to 0.78 ms and approximately 349 KB. One candidate
batch was slower, at 1.16 ms, so the speedup is workload-dependent.

The production filter uses Unity's own mapping once during initialization on
Unity 6 and newer. Older Editors keep their previous enumeration behavior.
Unity's [reference implementation of the icon API](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/PlatformSupport/PlayerSettingsPlatformIcons.bindings.cs)
performs this same mapping before fetching icons. This avoids caching a missing
icon or skipping a valid target merely because its build module is absent.

| Texture size | Direct PNG encode | Cold encode and file write | GPU readback and encode | Cached PNG check |
| --- | ---: | ---: | ---: | ---: |
| 32 | 0.027 ms | 0.073 ms | 0.487 ms | 0.0014 ms |
| 256 | 1.50 ms | 1.65 ms | 2.48 ms | 0.0014 ms |
| 1024 | 25.37 ms | 25.62 ms | 29.45 ms | 0.0014 ms |
| 2048 | 100.16 ms | 111.97 ms | 113.44 ms | 0.0015 ms |

Keep the existing PNG cache. The large conversion costs occur on cache misses,
not on every retry. Skipping file fingerprints would save only microseconds and
would lose recovery when the cached file is deleted or overwritten.

The alternate UTF-8 marshaler eliminated the 328 B path allocation but had no
consistent end-to-end timing benefit. Its older-runtime compatibility was not
tested, so the existing bridge remains unchanged. Caching EditorPrefs would save
less than a microsecond per update while introducing synchronization concerns.

## Native experiments

The first matrix compared the shipped binary with compiler, resolution, source
reuse and retry experiments. Times below are milliseconds per immediate apply.

| Configuration | Warm Badge | Warm Replacement | Cold Badge | Cold Replacement |
| --- | ---: | ---: | ---: | ---: |
| Shipped | 39.56 | 39.46 | 70.34 | 67.74 |
| `-O0` | 42.96 | 40.54 | 69.24 | 71.99 |
| `-O2` rebuild | 42.27 | 42.40 | 80.74 | 71.94 |
| `-O3` | 40.25 | 40.30 | 72.47 | 68.61 |
| `-Os` | 41.46 | 40.82 | 71.79 | 67.96 |
| `-O2 -flto` | 40.26 | 39.93 | 72.59 | 67.91 |
| 512-point canvas | 10.65 | 10.75 | 14.75 | 18.34 |
| 256-point canvas | 3.21 | 3.22 | 4.69 | 5.17 |
| Original source image for Replacement | 40.69 | 0.88 | 71.50 | 1.27 |
| Skip immediate setter on cache hit | 0.003 | 0.003 | 74.70 | 69.65 |
| Remove deferred retries | 44.96 | 41.53 | 84.61 | 70.23 |

The tiny skip-setter time measures metadata validation only. It excludes later
scheduled calls and fails the existing immediate-reapply contract. It must not be
presented as an equivalent implementation that is thousands of times faster.

Additional experiments preserved the 2048-pixel representation or changed
notification handling:

| Experiment | Outcome |
| --- | --- |
| Wrap the composed CGImage in a new NSImage | No reliable warm-call improvement |
| Round-trip through TIFF | No warm-call improvement; cold composition roughly doubled |
| Convert once to 8-bit sRGB RGBA | No warm-call improvement; additional cold conversion cost |
| Supply multiple cached image resolutions | No warm-call improvement; cold composition about 214 ms |
| Force `NSImageCacheAlways` | No warm-call improvement |
| Coalesce immediate notifications while retries are pending | 100 queued notifications dropped from 103 setter calls to 3 |

The final notification experiment was measured while an explicit apply already
had retries pending. Its near-zero synchronous notification time does not include
the three later setters. It postpones immediate reassertion, so it is a policy
change rather than a free improvement.

A process sample placed most native apply time in AppKit Dock display, image
resampling and color conversion. Changing compiler flags or the managed bridge
cannot materially reduce that work.

## Quality and scheduling tradeoffs

The faster image experiments changed the rendered output. Canonical 128-pixel
sRGB RGBA renders of this one source were compared byte by byte with the shipped
composition. These values measure this fixture, not every possible icon.

| Experiment | Maximum channel difference, Badge / Replacement | Mean absolute channel difference, Badge / Replacement |
| --- | ---: | ---: |
| 512-point canvas | 9 / 6 | 0.154 / 0.121 |
| 256-point canvas | 52 / 42 | 0.930 / 0.891 |
| Direct source image | 0 / 106 | 0 / 1.982 |

Channel differences use the 0 to 255 range on premultiplied RGBA data. Lower
canvases also remove the original highest-resolution representation. Direct
source reuse changes the scaling path and still needs tests with nonsquare,
transparent, large and color-profiled inputs. Neither becomes a default based on
a fast result with a 64-pixel source.

One isolated explicit apply schedules three deferred retries in addition to its
immediate setter. At approximately 40 ms each, that is about 160 ms of setter
work spread across the retry window. Closely spaced applies share deferred work,
but every explicit apply and notification still submits immediately. The current
100-notification stress burst therefore submits 103 times.

The highest-value next experiment is a less aggressive retry policy with visible
focus, activation, Play Mode and external-reset verification. Coalescing immediate
notifications could reduce bursts substantially without changing image pixels,
but requires evidence that delayed reassertion remains reliable. Reducing the
managed six-second focus retry window or the two-minute startup retry window has
the same correctness question. Their end-to-end behavior has not been changed.

## Coverage and limits

The audit measured idle scheduling, preference reads, actual empty-project
lookup, synthetic selection order, file checks, UTF-8 preparation, native cache
hits and misses, invalid sources, both appearance modes, notification bursts,
four PNG sizes and GPU conversion. It compared 17 native configurations and an
alternate managed marshaler, plus paired lookup filtering.

Unity compilation and the package's EditMode suite were run in the separate
Unity 7 project with Metal: 59 tests passed, with no failures or skips. The suite
includes three Play Mode cycles with domain
reload disabled, cache invalidation, image formats, preference restoration and
native contracts. The standalone native suite also passed cache, invalidation,
pinning, exception recovery, retry cancellation, Unicode and ABI checks.

Only Unity 7000.0.0a16 on Apple silicon was installed. Unity 2021.3/2022.3, Unity 6,
Intel execution, other macOS versions and Windows/Linux compilation were not run.
The native executable and importer were not changed. There is no runtime/player
assembly to benchmark. Actual Editor launch latency, full interactive focus
sequences, a new Dock backend, asynchronous GPU readback and persistent PNG
caching across reloads were not benchmarked. The results do not establish a
global optimum across those environments or designs.

## Reproduce

Run managed benchmarks in a disposable project with this package embedded and
listed in the manifest's `testables`, plus Pipeline and the Unity Test Framework.
Use a graphics-enabled Editor. The method temporarily changes Auto Apply,
application-icon ownership and the generated PNG cache, then restores preferences
and the previous cache contents. It blocks the Editor during timing.

Discover the commands supported by that Editor, refresh and compile, then invoke:

```bash
unity --format json command --group-by flat --detail full --project-path "$(pwd -P)"
unity --format json command script recompile --project-path "$(pwd -P)"
unity --format json command script recompile_status --project-path "$(pwd -P)"
unity --format json command script eval \
  --code 'return ProjectIcon.Editor.Tests.EditorIconBenchmarks.Run("/tmp/project-icon-managed.json");' \
  --timeout 180 --project-path "$(pwd -P)"
```

Wait for clean compilation before running the benchmark. The method deliberately
has no NUnit test attribute, so ordinary test runs do not execute benchmarks.

From the package root on macOS:

```bash
python3 Native~/macOS/benchmark.py --output /tmp/project-icon-native.json
python3 Native~/macOS/benchmark.py \
  --variants shipped,canvas512,canvas256,replacement-source-image \
  --capture /tmp/project-icon-pixels --output /tmp/project-icon-candidates.json
bash Native~/macOS/test.sh
```

The native script runs real AppKit operations and takes several minutes for the
full matrix. Capture files are raw premultiplied sRGB RGBA at the size in each
filename. Experiments that alter retry policy are diagnostic only. They are not
automatically accepted or used to rebuild the package bundle.

Raw samples, environment details, binary/source hashes and lookup diagnostics
are in [benchmarks/2026-11-08](benchmarks/2026-11-08/environment.json). The native
matrices are split into [initial](benchmarks/2026-11-08/native-benchmark.json),
[image formats](benchmarks/2026-11-08/native-representations.json),
[multiple resolutions](benchmarks/2026-11-08/native-mipmaps.json) and
[final candidates](benchmarks/2026-11-08/native-final-candidates.json).
The [managed baseline](benchmarks/2026-11-08/managed-before.json),
[managed result](benchmarks/2026-11-08/managed-after.json) and
[pixel comparisons](benchmarks/2026-11-08/pixel-comparison.json) retain the samples
behind the lookup and quality conclusions. The
[verification record](benchmarks/2026-11-08/verification.json) records the final
checks and source hashes.
