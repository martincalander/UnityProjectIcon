#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>
#include <dlfcn.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>

static NSImage *s_submittedIcon;
static void (*s_originalSetter)(id, SEL, NSImage *);
static void (*s_originalDraw)(id, SEL, NSRect, NSRect, NSCompositingOperation, CGFloat);
static BOOL s_throwNextSetter;
static BOOL s_throwNextResetSetter;
static BOOL s_throwNextChangedSetter;
static BOOL s_throwNextDraw;
static unsigned s_setterCalls;

static void ObserveApplicationIcon(id application, SEL selector, NSImage *image)
{
    ++s_setterCalls;
    if (s_throwNextSetter || (s_throwNextResetSetter && image == nil) ||
        (s_throwNextChangedSetter && image != s_submittedIcon))
    {
        s_throwNextSetter = NO;
        s_throwNextResetSetter = NO;
        s_throwNextChangedSetter = NO;
        [NSException raise:@"InjectedIconSetterFailure" format:@"Exercise native failure recovery."];
    }
    s_submittedIcon = image;
    s_originalSetter(application, selector, image);
}

static void ObserveImageDraw(id image, SEL selector, NSRect destination, NSRect source,
                             NSCompositingOperation operation, CGFloat fraction)
{
    if (s_throwNextDraw)
    {
        s_throwNextDraw = NO;
        [NSException raise:@"InjectedIconDrawFailure" format:@"Exercise image focus cleanup."];
    }
    s_originalDraw(image, selector, destination, source, operation, fraction);
}

static void Require(bool condition, const char *message)
{
    if (condition) return;
    fprintf(stderr, "FAIL: %s\n", message);
    exit(1);
}

static void RunOnBackground(void (^action)(void))
{
    dispatch_group_t group = dispatch_group_create();
    dispatch_group_async(group, dispatch_get_global_queue(QOS_CLASS_DEFAULT, 0), action);
    NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:5];
    while (dispatch_group_wait(group, DISPATCH_TIME_NOW) != 0)
    {
        Require(deadline.timeIntervalSinceNow > 0, "Background native call must complete.");
        [[NSRunLoop currentRunLoop] runUntilDate:[NSDate dateWithTimeIntervalSinceNow:0.01]];
    }
}

static void DrainDeferredIconWork()
{
    // A run-loop deadline can expire between callbacks on slower or translated processes.
    // Wait for a main-queue marker scheduled after the icon's final retry deadline instead.
    __block BOOL complete = NO;
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        complete = YES;
    });
    NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:5];
    while (!complete)
    {
        Require(deadline.timeIntervalSinceNow > 0, "Deferred native callbacks must complete.");
        [[NSRunLoop currentRunLoop] runUntilDate:[NSDate dateWithTimeIntervalSinceNow:0.01]];
    }
}

int main(int argc, const char *argv[])
{
    @autoreleasepool
    {
        Require(argc >= 3, "Usage: native-tests <bundle executable> <PNG> [--benchmark-only]");
        [NSApplication sharedApplication];
        [NSApp setActivationPolicy:NSApplicationActivationPolicyProhibited];
        // Observe actual submitted images; AppKit can normalize the getter's representation.
        Class applicationClass = [NSApp class];
        SEL setter = @selector(setApplicationIconImage:);
        Method method = class_getInstanceMethod(applicationClass, setter);
        s_originalSetter = (void (*)(id, SEL, NSImage *))class_getMethodImplementation(applicationClass, setter);
        class_replaceMethod(applicationClass, setter, (IMP)ObserveApplicationIcon, method_getTypeEncoding(method));
        SEL draw = @selector(drawInRect:fromRect:operation:fraction:);
        Method drawMethod = class_getInstanceMethod([NSImage class], draw);
        s_originalDraw = (void (*)(id, SEL, NSRect, NSRect, NSCompositingOperation, CGFloat))method_getImplementation(drawMethod);
        class_replaceMethod([NSImage class], draw, (IMP)ObserveImageDraw, method_getTypeEncoding(drawMethod));
        void *library = dlopen(argv[1], RTLD_NOW | RTLD_LOCAL);
        Require(library != nullptr, dlerror());
        auto apply = (int (*)(const char *, int32_t))dlsym(library, "ProjectIconSetApplicationIconWithMode");
        auto applyBadge = (int (*)(const char *))dlsym(library, "ProjectIconSetApplicationIcon");
        auto reset = (void (*)())dlsym(library, "ProjectIconResetApplicationIcon");
        Require(apply && applyBadge && reset, "All supported entry points must be exported.");

        NSFileManager *files = [NSFileManager defaultManager];
        NSString *directory = [NSTemporaryDirectory() stringByAppendingPathComponent:NSUUID.UUID.UUIDString];
        Require([files createDirectoryAtPath:directory withIntermediateDirectories:NO attributes:nil error:nullptr],
                "Create isolated test directory.");
        NSString *path = [directory stringByAppendingPathComponent:@"icon.png"];
        NSData *png = [NSData dataWithContentsOfFile:[NSString stringWithUTF8String:argv[2]]];
        Require(png != nil && [png writeToFile:path atomically:YES], "Copy source PNG.");

        if (argc > 3 && strcmp(argv[3], "--benchmark-only") == 0)
        {
            Require(apply(path.fileSystemRepresentation, 1) == 1, "Warm up replacement icon.");
            CFAbsoluteTime start = CFAbsoluteTimeGetCurrent();
            for (int i = 0; i < 40; i++)
                Require(apply(path.fileSystemRepresentation, 1) == 1, "Reapply replacement icon.");
            printf("40 unchanged applies: %.3f ms\n", (CFAbsoluteTimeGetCurrent() - start) * 1000.0);
        }
        else
        {
            for (int mode = 0; mode <= 1; mode++)
            {
                reset();
                Require(apply(path.fileSystemRepresentation, mode) == 1, "Apply supported mode.");
                NSImage *first = s_submittedIcon;
                Require(first != nil, "Application icon is available.");
                for (int i = 0; i < 3; i++)
                    Require(apply(path.fileSystemRepresentation, mode) == 1, "Reapply unchanged source.");
                Require(s_submittedIcon == first, "Unchanged source reuses its composed image.");

                NSApp.applicationIconImage = [[NSImage alloc] initWithSize:NSMakeSize(8, 8)];
                Require(s_submittedIcon == first, "Pinned image survives an external icon reset.");

                struct stat beforeWrite, afterWrite;
                Require(stat(path.fileSystemRepresentation, &beforeWrite) == 0, "Read original file metadata.");
                NSFileHandle *writer = [NSFileHandle fileHandleForWritingAtPath:path];
                Require(writer != nil, "Open existing icon for an in-place update.");
                [writer writeData:png];
                [writer closeFile];
                Require([files setAttributes:@{NSFileModificationDate: [NSDate dateWithTimeIntervalSinceNow:2]}
                                ofItemAtPath:path error:nullptr], "Advance source modification time.");
                Require(stat(path.fileSystemRepresentation, &afterWrite) == 0 &&
                        beforeWrite.st_ino == afterWrite.st_ino && beforeWrite.st_size == afterWrite.st_size,
                        "In-place update preserves file identity and size.");
                Require(apply(path.fileSystemRepresentation, mode) == 1, "Apply modified source.");
                NSImage *modified = s_submittedIcon;
                Require(modified != first, "Timestamp changes invalidate the cache.");

                Require([png writeToFile:path atomically:YES], "Replace source at the same path.");
                Require(apply(path.fileSystemRepresentation, mode) == 1, "Apply replaced source.");
                NSImage *replaced = s_submittedIcon;
                Require(replaced != modified, "Replacing the source invalidates the cache.");

                NSString *otherPath = [directory stringByAppendingPathComponent:@"other.png"];
                Require([png writeToFile:otherPath atomically:YES], "Copy source to a different path.");
                Require(apply(otherPath.fileSystemRepresentation, mode) == 1, "Apply different path.");
                NSImage *other = s_submittedIcon;
                Require(other != replaced, "Source path participates in cache identity.");
                Require(apply(otherPath.fileSystemRepresentation, 1 - mode) == 1, "Change appearance.");
                NSImage *changedMode = s_submittedIcon;
                Require(changedMode != other, "Appearance participates in cache identity.");

                Require([files removeItemAtPath:otherPath error:nullptr], "Remove source.");
                Require(apply(otherPath.fileSystemRepresentation, 1 - mode) == 0, "Missing source is rejected.");
                Require(s_submittedIcon == changedMode, "Failed load preserves the pinned icon.");
                Require(apply(path.fileSystemRepresentation, 99) == 0, "Unsupported appearance is rejected.");

                Require(apply(path.fileSystemRepresentation, mode) == 1, "Restore source before failure checks.");
                NSImage *beforeFailure = s_submittedIcon;
                s_throwNextSetter = YES;
                Require(apply(path.fileSystemRepresentation, 1 - mode) == 0, "Setter exception reports failure.");
                Require(!s_throwNextSetter && s_submittedIcon == beforeFailure, "Failed setter restores the previous icon.");
                Require(apply(path.fileSystemRepresentation, mode) == 1 && s_submittedIcon == beforeFailure,
                        "Failed setter preserves the previous cached composition.");
                s_throwNextSetter = YES;
                NSApp.applicationIconImage = [[NSImage alloc] initWithSize:NSMakeSize(8, 8)];
                Require(!s_throwNextSetter && s_submittedIcon == beforeFailure,
                        "The pinned setter hook contains exceptions.");

                s_throwNextChangedSetter = YES;
                __block int backgroundResult = -1;
                RunOnBackground(^{ backgroundResult = apply(path.fileSystemRepresentation, 1 - mode); });
                Require(backgroundResult == 0 && !s_throwNextChangedSetter,
                        "Background setter exception reports failure.");
                Require(s_submittedIcon == beforeFailure, "Failed background apply restores the previous icon.");

                NSGraphicsContext *previousContext = [NSGraphicsContext currentContext];
                s_throwNextDraw = YES;
                int drawResult = apply(path.fileSystemRepresentation, 1 - mode);
                Require(drawResult == 0 && !s_throwNextDraw, "Drawing exception reports failure.");
                Require([NSGraphicsContext currentContext] == previousContext, "Failed drawing releases image focus.");
                Require(s_submittedIcon == beforeFailure, "Failed drawing preserves the pinned icon.");

                s_throwNextSetter = YES;
                [[NSNotificationCenter defaultCenter] postNotificationName:NSApplicationDidBecomeActiveNotification object:NSApp];
                Require(!s_throwNextSetter, "Notification reapply contains setter exceptions.");
                s_throwNextSetter = YES;
                DrainDeferredIconWork();
                Require(!s_throwNextSetter, "Deferred reapply contains setter exceptions.");

                Require(apply(path.fileSystemRepresentation, mode) == 1, "Restore source before reset.");
                NSImage *beforeReset = s_submittedIcon;
                s_throwNextResetSetter = YES;
                RunOnBackground(^{ reset(); });
                Require(!s_throwNextResetSetter, "Background reset contains setter exceptions.");
                reset();
                s_throwNextSetter = YES;
                [[NSNotificationCenter defaultCenter] postNotificationName:NSApplicationDidBecomeActiveNotification object:NSApp];
                Require(s_throwNextSetter, "Reset removes notification observers.");
                s_throwNextSetter = NO;
                NSImage *external = [[NSImage alloc] initWithSize:NSMakeSize(8, 8)];
                NSApp.applicationIconImage = external;
                Require(s_submittedIcon == external, "Reset releases icon pinning.");
                Require(apply(path.fileSystemRepresentation, mode) == 1, "Reapply after reset.");
                Require(s_submittedIcon != beforeReset, "Reset releases the cached composition.");
            }
            reset();
            s_throwNextSetter = YES;
            Require(apply(path.fileSystemRepresentation, 1) == 0 && !s_throwNextSetter,
                    "A failed first apply reports failure.");
            s_throwNextSetter = YES;
            [[NSNotificationCenter defaultCenter] postNotificationName:NSApplicationDidBecomeActiveNotification object:NSApp];
            Require(s_throwNextSetter, "A failed first apply leaves no notification observers.");
            s_throwNextSetter = NO;
            NSImage *external = [[NSImage alloc] initWithSize:NSMakeSize(8, 8)];
            NSApp.applicationIconImage = external;
            Require(s_submittedIcon == external, "A failed first apply leaves the hook unpinned.");
            Require(applyBadge(path.fileSystemRepresentation) == 1, "Badge entry point applies an icon.");

            reset();
            unsigned beforeBurst = s_setterCalls;
            for (int i = 0; i < 20; i++)
                Require(apply(path.fileSystemRepresentation, 1) == 1, "Apply during a burst.");
            unsigned afterBurst = s_setterCalls;
            Require(afterBurst - beforeBurst == 20, "Every explicit apply still submits immediately.");
            DrainDeferredIconWork();
            Require(s_setterCalls - afterBurst == 3, "A burst shares one set of deferred retries.");

            beforeBurst = s_setterCalls;
            for (int i = 0; i < 20; i++)
                [[NSNotificationCenter defaultCenter] postNotificationName:NSApplicationDidBecomeActiveNotification object:NSApp];
            afterBurst = s_setterCalls;
            Require(afterBurst - beforeBurst == 20, "Notifications still submit immediately.");
            DrainDeferredIconWork();
            Require(s_setterCalls - afterBurst == 3, "Notification bursts share deferred retries.");

            Require(apply(path.fileSystemRepresentation, 1) == 1, "Schedule retries before reset.");
            reset();
            Require(apply(path.fileSystemRepresentation, 0) == 1, "Schedule a new retry generation.");
            afterBurst = s_setterCalls;
            DrainDeferredIconWork();
            Require(s_setterCalls - afterBurst == 3, "Reset cancels retries from the earlier generation.");
            puts("Native cache, invalidation, pinning, exception recovery, coalesced retries, reset and ABI checks passed.");
        }

        reset();
        [files removeItemAtPath:directory error:nullptr];
        // The AppKit method hook remains installed until process exit.
        return 0;
    }
}
