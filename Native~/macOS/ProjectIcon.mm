#import <Cocoa/Cocoa.h>
#import <dispatch/dispatch.h>
#import <objc/runtime.h>
#include <stdint.h>
#include <sys/stat.h>

enum ProjectIconMode : int32_t
{
    ProjectIconModeBadge = 0,
    ProjectIconModeReplacement = 1
};

static NSImage *s_pinnedApplicationIcon = nil;
static NSString *s_pinnedIconPath = nil;
static ProjectIconMode s_pinnedIconMode;
static struct stat s_pinnedIconFileStatus;
static BOOL s_hasPinnedIconFileStatus = NO;
static NSMutableArray *s_notificationObserverTokens = nil;
static void (*s_originalSetApplicationIconImage)(id, SEL, NSImage *) = nullptr;
static BOOL s_applicationIconHookInstalled = NO;

static void ProjectIconSetApplicationIconImageUsingOriginal(NSImage *image)
{
    NSApplication *application = [NSApplication sharedApplication];
    SEL selector = @selector(setApplicationIconImage:);

    if (s_originalSetApplicationIconImage != nullptr)
    {
        s_originalSetApplicationIconImage(application, selector, image);
        return;
    }

    [application setApplicationIconImage:image];
}

static void ProjectIconSetApplicationIconImageHook(id self, SEL selector, NSImage *image)
{
    if (s_originalSetApplicationIconImage == nullptr)
        return;

    NSImage *effectiveImage = s_pinnedApplicationIcon != nil
        ? s_pinnedApplicationIcon
        : image;

    @try
    {
        s_originalSetApplicationIconImage(self, selector, effectiveImage);
    }
    @catch (__unused NSException *exception)
    {
        // A native exception must not unwind through Unity or a deferred AppKit callback.
    }
}

static void ProjectIconInstallApplicationIconHookOnMainThread(void)
{
    if (s_applicationIconHookInstalled)
        return;

    NSApplication *application = [NSApplication sharedApplication];
    Class applicationClass = [application class];
    SEL selector = @selector(setApplicationIconImage:);
    Method method = class_getInstanceMethod(applicationClass, selector);
    if (method == nullptr)
        return;

    s_originalSetApplicationIconImage = (void (*)(id, SEL, NSImage *))class_getMethodImplementation(applicationClass, selector);
    const char *typeEncoding = method_getTypeEncoding(method);
    class_replaceMethod(applicationClass, selector, (IMP)ProjectIconSetApplicationIconImageHook, typeEncoding);
    s_applicationIconHookInstalled = YES;
}

static NSImage *ProjectIconCreateDefaultApplicationIcon(void)
{
    NSString *bundlePath = [[NSBundle mainBundle] bundlePath];
    if ([bundlePath length] > 0)
    {
        NSImage *bundleIcon = [[NSWorkspace sharedWorkspace] iconForFile:bundlePath];
        if (bundleIcon != nil)
            return bundleIcon;
    }

    return [[NSApplication sharedApplication] applicationIconImage];
}

static NSImage *ProjectIconCreateBadgedApplicationIcon(NSImage *badgeImage)
{
    NSImage *baseImage = ProjectIconCreateDefaultApplicationIcon();
    if (baseImage == nil || badgeImage == nil)
        return nil;

    const CGFloat canvasSize = 1024.0;
    const CGFloat badgeSize = canvasSize * 0.20;
    const CGFloat badgeLeftInset = canvasSize * 0.13;
    const CGFloat badgeTopInset = canvasSize * 0.12;

    NSImage *compositeImage = [[NSImage alloc] initWithSize:NSMakeSize(canvasSize, canvasSize)];
    [compositeImage lockFocus];
    @try
    {
        NSGraphicsContext *context = [NSGraphicsContext currentContext];
        [context setImageInterpolation:NSImageInterpolationHigh];

        NSRect fullRect = NSMakeRect(0.0, 0.0, canvasSize, canvasSize);
        [baseImage drawInRect:fullRect
                     fromRect:NSZeroRect
                    operation:NSCompositingOperationSourceOver
                     fraction:1.0];

        NSRect badgeRect = NSMakeRect(
            badgeLeftInset,
            canvasSize - badgeSize - badgeTopInset,
            badgeSize,
            badgeSize);

        [badgeImage drawInRect:badgeRect
                      fromRect:NSZeroRect
                     operation:NSCompositingOperationSourceOver
                      fraction:1.0];
    }
    @finally
    {
        [compositeImage unlockFocus];
    }
    [compositeImage setTemplate:NO];
    return compositeImage;
}

static NSImage *ProjectIconCreateReplacementApplicationIcon(NSImage *sourceImage)
{
    if (sourceImage == nil)
        return nil;

    const CGFloat canvasSize = 1024.0;
    NSImage *replacementImage = [[NSImage alloc] initWithSize:NSMakeSize(canvasSize, canvasSize)];
    [replacementImage lockFocus];
    @try
    {
        NSGraphicsContext *context = [NSGraphicsContext currentContext];
        [context setImageInterpolation:NSImageInterpolationHigh];

        NSRect fullRect = NSMakeRect(0.0, 0.0, canvasSize, canvasSize);
        [sourceImage drawInRect:fullRect
                       fromRect:NSZeroRect
                      operation:NSCompositingOperationCopy
                       fraction:1.0];
    }
    @finally
    {
        [replacementImage unlockFocus];
    }
    [replacementImage setTemplate:NO];
    return replacementImage;
}

static BOOL ProjectIconApplyPinnedApplicationIconOnMainThread(void)
{
    @try
    {
        if (s_pinnedApplicationIcon != nil)
            ProjectIconSetApplicationIconImageUsingOriginal(s_pinnedApplicationIcon);
        return YES;
    }
    @catch (__unused NSException *exception)
    {
        return NO;
    }
}

static void ProjectIconApplyPinnedApplicationIconDeferred(void)
{
    if (s_pinnedApplicationIcon == nil)
        return;

    dispatch_async(dispatch_get_main_queue(), ^{
        ProjectIconApplyPinnedApplicationIconOnMainThread();
    });

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.05 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        ProjectIconApplyPinnedApplicationIconOnMainThread();
    });

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.25 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        ProjectIconApplyPinnedApplicationIconOnMainThread();
    });
}

static void ProjectIconAddNotificationObserver(NSMutableArray *tokens, NSString *name, id object)
{
    NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
    id token = [center addObserverForName:name object:object queue:[NSOperationQueue mainQueue] usingBlock:^(__unused NSNotification *notification) {
        ProjectIconApplyPinnedApplicationIconOnMainThread();
        ProjectIconApplyPinnedApplicationIconDeferred();
    }];

    [tokens addObject:token];
}

static void ProjectIconRemoveNotificationObservers(NSMutableArray *tokens)
{
    NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
    for (id token in tokens)
    {
        @try
        {
            [center removeObserver:token];
        }
        @catch (__unused NSException *exception)
        {
        }
    }
    [tokens removeAllObjects];
}

static void ProjectIconInstallNotificationObserversOnMainThread(void)
{
    if (s_notificationObserverTokens != nil)
        return;

    NSMutableArray *tokens = [[NSMutableArray alloc] init];
    @try
    {
        NSApplication *application = [NSApplication sharedApplication];
        ProjectIconAddNotificationObserver(tokens, NSApplicationWillBecomeActiveNotification, application);
        ProjectIconAddNotificationObserver(tokens, NSApplicationDidBecomeActiveNotification, application);
        ProjectIconAddNotificationObserver(tokens, NSApplicationWillResignActiveNotification, application);
        ProjectIconAddNotificationObserver(tokens, NSApplicationDidResignActiveNotification, application);
        ProjectIconAddNotificationObserver(tokens, NSWindowDidBecomeKeyNotification, nil);
        ProjectIconAddNotificationObserver(tokens, NSWindowDidBecomeMainNotification, nil);
        s_notificationObserverTokens = tokens;
    }
    @catch (NSException *exception)
    {
        ProjectIconRemoveNotificationObservers(tokens);
        @throw exception;
    }
}

static void ProjectIconRemoveNotificationObserversOnMainThread(void)
{
    ProjectIconRemoveNotificationObservers(s_notificationObserverTokens);
    s_notificationObserverTokens = nil;
}

static BOOL ProjectIconFileStatusMatches(const struct stat &left, const struct stat &right)
{
    return left.st_dev == right.st_dev &&
        left.st_ino == right.st_ino &&
        left.st_size == right.st_size &&
        left.st_mtimespec.tv_sec == right.st_mtimespec.tv_sec &&
        left.st_mtimespec.tv_nsec == right.st_mtimespec.tv_nsec &&
        left.st_ctimespec.tv_sec == right.st_ctimespec.tv_sec &&
        left.st_ctimespec.tv_nsec == right.st_ctimespec.tv_nsec;
}

static int ProjectIconSetApplicationIconOnMainThread(
    NSString *path,
    ProjectIconMode mode)
{
    NSImage *previousPinnedIcon = s_pinnedApplicationIcon;
    NSString *previousPath = s_pinnedIconPath;
    ProjectIconMode previousMode = s_pinnedIconMode;
    struct stat previousFileStatus = s_pinnedIconFileStatus;
    BOOL previousHasFileStatus = s_hasPinnedIconFileStatus;
    BOOL hadObservers = s_notificationObserverTokens != nil;
    __block NSImage *previousApplicationIcon = nil;
    BOOL stateChanged = NO;
    void (^restorePreviousState)(void) = ^{
        s_pinnedApplicationIcon = previousPinnedIcon;
        s_pinnedIconPath = previousPath;
        s_pinnedIconMode = previousMode;
        s_pinnedIconFileStatus = previousFileStatus;
        s_hasPinnedIconFileStatus = previousHasFileStatus;
        if (!hadObservers)
            ProjectIconRemoveNotificationObserversOnMainThread();
        @try
        {
            ProjectIconSetApplicationIconImageUsingOriginal(previousApplicationIcon);
        }
        @catch (__unused NSException *exception)
        {
        }
    };

    @try
    {
        struct stat fileStatus;
        BOOL hasFileStatus = stat([path fileSystemRepresentation], &fileStatus) == 0;
        if (s_pinnedApplicationIcon != nil &&
            s_hasPinnedIconFileStatus &&
            hasFileStatus &&
            s_pinnedIconMode == mode &&
            [s_pinnedIconPath isEqualToString:path] &&
            ProjectIconFileStatusMatches(s_pinnedIconFileStatus, fileStatus))
        {
            if (!ProjectIconApplyPinnedApplicationIconOnMainThread())
                return 0;
            ProjectIconApplyPinnedApplicationIconDeferred();
            return 1;
        }

        NSImage *sourceImage = [[NSImage alloc] initWithContentsOfFile:path];
        if (sourceImage == nil)
            return 0;

        NSImage *applicationIcon = nil;
        switch (mode)
        {
            case ProjectIconModeBadge:
                applicationIcon = ProjectIconCreateBadgedApplicationIcon(sourceImage);
                break;
            case ProjectIconModeReplacement:
                applicationIcon = ProjectIconCreateReplacementApplicationIcon(sourceImage);
                break;
            default:
                return 0;
        }

        if (applicationIcon == nil)
            return 0;

        // Drawing may decode lazily. Only cache a source that stayed unchanged through composition.
        struct stat composedFileStatus;
        BOOL hasStableFileStatus = hasFileStatus &&
            stat([path fileSystemRepresentation], &composedFileStatus) == 0 &&
            ProjectIconFileStatusMatches(fileStatus, composedFileStatus);

        previousApplicationIcon = previousPinnedIcon != nil
            ? previousPinnedIcon
            : [[NSApplication sharedApplication] applicationIconImage];
        stateChanged = YES;
        s_pinnedApplicationIcon = applicationIcon;
        s_pinnedIconPath = hasStableFileStatus ? [path copy] : nil;
        s_pinnedIconMode = mode;
        s_hasPinnedIconFileStatus = hasStableFileStatus;
        if (hasStableFileStatus)
            s_pinnedIconFileStatus = composedFileStatus;
        ProjectIconInstallApplicationIconHookOnMainThread();
        ProjectIconInstallNotificationObserversOnMainThread();
        if (!ProjectIconApplyPinnedApplicationIconOnMainThread())
        {
            restorePreviousState();
            return 0;
        }
        ProjectIconApplyPinnedApplicationIconDeferred();
        return 1;
    }
    @catch (__unused NSException *exception)
    {
        if (stateChanged)
            restorePreviousState();
        return 0;
    }
}

extern "C" int ProjectIconSetApplicationIconWithMode(
    const char *utf8Path,
    int32_t rawMode)
{
    @autoreleasepool
    {
        @try
        {
            if (utf8Path == nullptr || utf8Path[0] == '\0' ||
                (rawMode != ProjectIconModeBadge && rawMode != ProjectIconModeReplacement))
                return 0;

            NSString *path = [NSString stringWithUTF8String:utf8Path];
            if (path == nil)
                return 0;

            ProjectIconMode mode = static_cast<ProjectIconMode>(rawMode);
            __block int result = 0;
            void (^apply)(void) = ^{
                // Catch on the main thread itself, including dispatch_sync callers.
                @try
                {
                    result = ProjectIconSetApplicationIconOnMainThread(path, mode);
                }
                @catch (__unused NSException *exception)
                {
                    result = 0;
                }
            };
            if ([NSThread isMainThread])
                apply();
            else
                dispatch_sync(dispatch_get_main_queue(), apply);
            return result;
        }
        @catch (__unused NSException *exception)
        {
            return 0;
        }
    }
}

extern "C" int ProjectIconSetApplicationIcon(const char *utf8Path)
{
    return ProjectIconSetApplicationIconWithMode(
        utf8Path,
        ProjectIconModeBadge);
}

extern "C" void ProjectIconResetApplicationIcon()
{
    @autoreleasepool
    {
        void (^reset)(void) = ^{
            @try
            {
                s_pinnedApplicationIcon = nil;
                s_pinnedIconPath = nil;
                s_hasPinnedIconFileStatus = NO;
                ProjectIconRemoveNotificationObserversOnMainThread();
                ProjectIconSetApplicationIconImageUsingOriginal(nil);
            }
            @catch (__unused NSException *exception)
            {
            }
        };
        @try
        {
            if ([NSThread isMainThread])
                reset();
            else
                dispatch_sync(dispatch_get_main_queue(), reset);
        }
        @catch (__unused NSException *exception)
        {
        }
    }
}
