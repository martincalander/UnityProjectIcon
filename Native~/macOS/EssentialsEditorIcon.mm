#import <Cocoa/Cocoa.h>
#import <dispatch/dispatch.h>
#import <objc/runtime.h>
#include <stdint.h>

enum EssentialsEditorIconMode : int32_t
{
    EssentialsEditorIconModeBadge = 0,
    EssentialsEditorIconModeReplacement = 1
};

static NSImage *s_pinnedApplicationIcon = nil;
static NSMutableArray *s_notificationObserverTokens = nil;
static void (*s_originalSetApplicationIconImage)(id, SEL, NSImage *) = nullptr;
static BOOL s_applicationIconHookInstalled = NO;

static void EssentialsEditorIconSetApplicationIconImageUsingOriginal(NSImage *image)
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

static void EssentialsEditorIconSetApplicationIconImageHook(id self, SEL selector, NSImage *image)
{
    if (s_originalSetApplicationIconImage == nullptr)
        return;

    NSImage *effectiveImage = s_pinnedApplicationIcon != nil
        ? s_pinnedApplicationIcon
        : image;

    s_originalSetApplicationIconImage(self, selector, effectiveImage);
}

static void EssentialsEditorIconInstallApplicationIconHookOnMainThread(void)
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
    class_replaceMethod(applicationClass, selector, (IMP)EssentialsEditorIconSetApplicationIconImageHook, typeEncoding);
    s_applicationIconHookInstalled = YES;
}

static NSImage *EssentialsEditorIconCreateDefaultApplicationIcon(void)
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

static NSImage *EssentialsEditorIconCreateBadgedApplicationIcon(NSImage *badgeImage)
{
    NSImage *baseImage = EssentialsEditorIconCreateDefaultApplicationIcon();
    if (baseImage == nil || badgeImage == nil)
        return nil;

    const CGFloat canvasSize = 1024.0;
    const CGFloat badgeSize = canvasSize * 0.20;
    const CGFloat badgeLeftInset = canvasSize * 0.13;
    const CGFloat badgeTopInset = canvasSize * 0.12;

    NSImage *compositeImage = [[NSImage alloc] initWithSize:NSMakeSize(canvasSize, canvasSize)];
    [compositeImage lockFocus];

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

    [compositeImage unlockFocus];
    [compositeImage setTemplate:NO];
    return compositeImage;
}

static NSImage *EssentialsEditorIconCreateReplacementApplicationIcon(NSImage *sourceImage)
{
    if (sourceImage == nil)
        return nil;

    const CGFloat canvasSize = 1024.0;
    NSImage *replacementImage = [[NSImage alloc] initWithSize:NSMakeSize(canvasSize, canvasSize)];
    [replacementImage lockFocus];

    NSGraphicsContext *context = [NSGraphicsContext currentContext];
    [context setImageInterpolation:NSImageInterpolationHigh];

    NSRect fullRect = NSMakeRect(0.0, 0.0, canvasSize, canvasSize);
    [sourceImage drawInRect:fullRect
                   fromRect:NSZeroRect
                  operation:NSCompositingOperationCopy
                   fraction:1.0];

    [replacementImage unlockFocus];
    [replacementImage setTemplate:NO];
    return replacementImage;
}

static void EssentialsEditorIconApplyPinnedApplicationIconOnMainThread(void)
{
    if (s_pinnedApplicationIcon == nil)
        return;

    EssentialsEditorIconSetApplicationIconImageUsingOriginal(s_pinnedApplicationIcon);
}

static void EssentialsEditorIconApplyPinnedApplicationIconDeferred(void)
{
    if (s_pinnedApplicationIcon == nil)
        return;

    dispatch_async(dispatch_get_main_queue(), ^{
        EssentialsEditorIconApplyPinnedApplicationIconOnMainThread();
    });

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.05 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        EssentialsEditorIconApplyPinnedApplicationIconOnMainThread();
    });

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.25 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        EssentialsEditorIconApplyPinnedApplicationIconOnMainThread();
    });
}

static void EssentialsEditorIconAddNotificationObserver(NSString *name, id object)
{
    NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
    id token = [center addObserverForName:name object:object queue:[NSOperationQueue mainQueue] usingBlock:^(__unused NSNotification *notification) {
        EssentialsEditorIconApplyPinnedApplicationIconOnMainThread();
        EssentialsEditorIconApplyPinnedApplicationIconDeferred();
    }];

    [s_notificationObserverTokens addObject:token];
}

static void EssentialsEditorIconInstallNotificationObserversOnMainThread(void)
{
    if (s_notificationObserverTokens != nil)
        return;

    s_notificationObserverTokens = [[NSMutableArray alloc] init];
    NSApplication *application = [NSApplication sharedApplication];

    EssentialsEditorIconAddNotificationObserver(NSApplicationWillBecomeActiveNotification, application);
    EssentialsEditorIconAddNotificationObserver(NSApplicationDidBecomeActiveNotification, application);
    EssentialsEditorIconAddNotificationObserver(NSApplicationWillResignActiveNotification, application);
    EssentialsEditorIconAddNotificationObserver(NSApplicationDidResignActiveNotification, application);
    EssentialsEditorIconAddNotificationObserver(NSWindowDidBecomeKeyNotification, nil);
    EssentialsEditorIconAddNotificationObserver(NSWindowDidBecomeMainNotification, nil);
}

static void EssentialsEditorIconRemoveNotificationObserversOnMainThread(void)
{
    if (s_notificationObserverTokens == nil)
        return;

    NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
    for (id token in s_notificationObserverTokens)
        [center removeObserver:token];

    [s_notificationObserverTokens removeAllObjects];
    s_notificationObserverTokens = nil;
}

static int EssentialsEditorIconSetApplicationIconOnMainThread(
    NSString *path,
    EssentialsEditorIconMode mode)
{
    NSImage *sourceImage = [[NSImage alloc] initWithContentsOfFile:path];
    if (sourceImage == nil)
        return 0;

    NSImage *applicationIcon = nil;
    switch (mode)
    {
        case EssentialsEditorIconModeBadge:
            applicationIcon = EssentialsEditorIconCreateBadgedApplicationIcon(sourceImage);
            break;
        case EssentialsEditorIconModeReplacement:
            applicationIcon = EssentialsEditorIconCreateReplacementApplicationIcon(sourceImage);
            break;
        default:
            return 0;
    }

    if (applicationIcon == nil)
        return 0;

    s_pinnedApplicationIcon = applicationIcon;
    EssentialsEditorIconInstallApplicationIconHookOnMainThread();
    EssentialsEditorIconInstallNotificationObserversOnMainThread();
    EssentialsEditorIconApplyPinnedApplicationIconOnMainThread();
    EssentialsEditorIconApplyPinnedApplicationIconDeferred();
    return 1;
}

extern "C" int EssentialsEditorIconSetApplicationIconWithMode(
    const char *utf8Path,
    int32_t rawMode)
{
    @autoreleasepool
    {
        if (utf8Path == nullptr || utf8Path[0] == '\0')
            return 0;

        NSString *path = [NSString stringWithUTF8String:utf8Path];
        if (path == nil)
            return 0;

        EssentialsEditorIconMode mode = static_cast<EssentialsEditorIconMode>(rawMode);
        __block int result = 0;
        if ([NSThread isMainThread])
        {
            result = EssentialsEditorIconSetApplicationIconOnMainThread(path, mode);
        }
        else
        {
            dispatch_sync(dispatch_get_main_queue(), ^{
                result = EssentialsEditorIconSetApplicationIconOnMainThread(path, mode);
            });
        }

        return result;
    }
}

extern "C" int EssentialsEditorIconSetApplicationIcon(const char *utf8Path)
{
    return EssentialsEditorIconSetApplicationIconWithMode(
        utf8Path,
        EssentialsEditorIconModeBadge);
}

extern "C" void EssentialsEditorIconResetApplicationIcon()
{
    @autoreleasepool
    {
        if ([NSThread isMainThread])
        {
            s_pinnedApplicationIcon = nil;
            EssentialsEditorIconRemoveNotificationObserversOnMainThread();
            EssentialsEditorIconSetApplicationIconImageUsingOriginal(nil);
        }
        else
        {
            dispatch_sync(dispatch_get_main_queue(), ^{
                s_pinnedApplicationIcon = nil;
                EssentialsEditorIconRemoveNotificationObserversOnMainThread();
                EssentialsEditorIconSetApplicationIconImageUsingOriginal(nil);
            });
        }
    }
}
