#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>
#include <dlfcn.h>
#include <algorithm>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <functional>
#include <vector>
#include <sys/resource.h>

static unsigned long s_setterCalls;
static NSImage *s_submittedIcon;
static void (*s_originalSetter)(id, SEL, NSImage *);

static void ObserveSetter(id application, SEL selector, NSImage *image)
{
    ++s_setterCalls;
    s_submittedIcon = image;
    s_originalSetter(application, selector, image);
}

static void Require(bool success)
{
    if (!success) { fprintf(stderr, "Native benchmark operation failed.\n"); exit(1); }
}

static NSDictionary *Measure(NSString *name, int iterations, const std::function<void()> &action)
{
    @autoreleasepool { action(); }
    std::vector<double> times;
    NSMutableArray *samples = [NSMutableArray array];
    unsigned long beforeCalls = s_setterCalls;
    for (int sample = 0; sample < 9; ++sample)
    {
        auto start = std::chrono::steady_clock::now();
        for (int i = 0; i < iterations; ++i)
        {
            @autoreleasepool { action(); }
        }
        double microseconds = std::chrono::duration<double, std::micro>(
            std::chrono::steady_clock::now() - start).count() / iterations;
        times.push_back(microseconds);
        [samples addObject:@(microseconds)];
    }
    std::sort(times.begin(), times.end());
    return @{@"name": name, @"iterations": @(iterations), @"medianMicroseconds": @(times[4]),
             @"minMicroseconds": @(times.front()), @"maxMicroseconds": @(times.back()),
             @"setterCallsPerOperation": @((s_setterCalls - beforeCalls) / (double)(9 * iterations)),
             @"sampleMicroseconds": samples};
}

int main(int argc, const char *argv[])
{
    @autoreleasepool
    {
        Require(argc == 4 || argc == 5);
        [NSApplication sharedApplication];
        [NSApp setActivationPolicy:NSApplicationActivationPolicyProhibited];
        Class applicationClass = [NSApp class];
        SEL setter = @selector(setApplicationIconImage:);
        Method method = class_getInstanceMethod(applicationClass, setter);
        s_originalSetter = (void (*)(id, SEL, NSImage *))class_getMethodImplementation(applicationClass, setter);
        class_replaceMethod(applicationClass, setter, (IMP)ObserveSetter, method_getTypeEncoding(method));
        void *library = dlopen(argv[1], RTLD_NOW | RTLD_LOCAL);
        if (!library) { fprintf(stderr, "%s\n", dlerror()); return 1; }
        auto apply = (int (*)(const char *, int32_t))dlsym(library, "ProjectIconSetApplicationIconWithMode");
        auto reset = (void (*)())dlsym(library, "ProjectIconResetApplicationIcon");
        Require(apply && reset);
        NSString *source = [NSString stringWithUTF8String:argv[2]];
        NSString *directory = [NSTemporaryDirectory() stringByAppendingPathComponent:NSUUID.UUID.UUIDString];
        NSFileManager *files = [NSFileManager defaultManager];
        Require([files createDirectoryAtPath:directory withIntermediateDirectories:NO attributes:nil error:nil]);
        NSMutableArray *measurements = [NSMutableArray array];
        NSMutableArray *representations = [NSMutableArray array];
        @try
        {
            for (int mode = 0; mode < 2; ++mode)
            {
                Require(apply(source.fileSystemRepresentation, mode) == 1);
                CGImageRef image = [s_submittedIcon CGImageForProposedRect:nullptr context:nil hints:nil];
                Require(image != nullptr);
                [representations addObject:@{@"mode": @(mode), @"width": @(CGImageGetWidth(image)),
                    @"height": @(CGImageGetHeight(image)), @"bitsPerPixel": @(CGImageGetBitsPerPixel(image)),
                    @"bitsPerComponent": @(CGImageGetBitsPerComponent(image))}];
                if (argc == 5)
                {
                    for (size_t size : {128, 1024})
                    {
                        CGColorSpaceRef colors = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
                        CGContextRef context = CGBitmapContextCreate(nullptr, size, size, 8, size * 4,
                            colors, kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big);
                        Require(context != nullptr);
                        CGContextSetInterpolationQuality(context, kCGInterpolationHigh);
                        CGContextDrawImage(context, CGRectMake(0, 0, size, size), image);
                        NSData *pixels = [NSData dataWithBytes:CGBitmapContextGetData(context) length:size * size * 4];
                        NSString *path = [NSString stringWithFormat:@"%s/%s-mode%d-%zu.rgba", argv[4], argv[3], mode, size];
                        Require([pixels writeToFile:path atomically:YES]);
                        CGContextRelease(context);
                        CGColorSpaceRelease(colors);
                    }
                }
                [measurements addObject:Measure([NSString stringWithFormat:@"warm.mode%d", mode], 5, [&] {
                    Require(apply(source.fileSystemRepresentation, mode) == 1);
                })];
                NSString *copy = [directory stringByAppendingPathComponent:@"copy.png"];
                [files removeItemAtPath:copy error:nil];
                Require([files copyItemAtPath:source toPath:copy error:nil]);
                bool alternate = false;
                [measurements addObject:Measure([NSString stringWithFormat:@"cold.mode%d", mode], 1, [&] {
                    alternate = !alternate;
                    Require(apply((alternate ? copy : source).fileSystemRepresentation, mode) == 1);
                })];
                [measurements addObject:Measure([NSString stringWithFormat:@"notification-burst10.mode%d", mode], 1, [&] {
                    for (int i = 0; i < 10; ++i)
                        @autoreleasepool {
                            [[NSNotificationCenter defaultCenter] postNotificationName:NSApplicationDidBecomeActiveNotification object:NSApp];
                        }
                })];
                reset();
            }
            [measurements addObject:Measure(@"missing-source", 100, [&] {
                Require(apply("/nonexistent-project-icon-benchmark.png", 1) == 0);
            })];
            NSString *corrupt = [directory stringByAppendingPathComponent:@"corrupt.png"];
            Require([@"invalid image" writeToFile:corrupt atomically:YES encoding:NSUTF8StringEncoding error:nil]);
            [measurements addObject:Measure(@"corrupt-source", 100, [&] {
                Require(apply(corrupt.fileSystemRepresentation, 1) == 0);
            })];
            // Count the complete coalesced retry burst, including scheduled callbacks.
            Require(apply(source.fileSystemRepresentation, 1) == 1);
            unsigned long before = s_setterCalls;
            for (int i = 0; i < 100; ++i)
                @autoreleasepool {
                    [[NSNotificationCenter defaultCenter] postNotificationName:NSApplicationDidBecomeActiveNotification object:NSApp];
                }
            __block bool drained = false;
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, NSEC_PER_SEC), dispatch_get_main_queue(), ^{ drained = true; });
            NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:10];
            while (!drained && deadline.timeIntervalSinceNow > 0)
                [[NSRunLoop currentRunLoop] runUntilDate:[NSDate dateWithTimeIntervalSinceNow:0.01]];
            Require(drained);
            struct rusage usage;
            getrusage(RUSAGE_SELF, &usage);
            NSDictionary *report = @{@"variant": [NSString stringWithUTF8String:argv[3]],
                @"operatingSystem": NSProcessInfo.processInfo.operatingSystemVersionString,
                @"measurements": measurements, @"burst100TotalSetterCalls": @(s_setterCalls - before),
                @"representations": representations, @"peakResidentBytes": @(usage.ru_maxrss)};
            NSData *json = [NSJSONSerialization dataWithJSONObject:report options:NSJSONWritingPrettyPrinted error:nil];
            Require(json != nil);
            fwrite(json.bytes, 1, json.length, stdout);
            printf("\n");
        }
        @finally
        {
            reset();
            [files removeItemAtPath:directory error:nil];
        }
    }
}
