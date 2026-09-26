#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>

static float g_minAspect = 0.0f;
static float g_maxAspect = 0.0f;
static int g_minWidth = 0;
static int g_minHeight = 0;
static BOOL g_enabled = NO;
static IMP g_originalIMP = NULL;
static BOOL g_initialized = NO;

static NSSize hooked_windowWillResize(id self, SEL _cmd, NSWindow *sender, NSSize frameSize)
{
    if (g_originalIMP)
    {
        frameSize = ((NSSize (*)(id, SEL, NSWindow *, NSSize))g_originalIMP)(self, _cmd, sender, frameSize);
    }

    if (!g_enabled) return frameSize;

    NSRect frameRect = NSMakeRect(0, 0, frameSize.width, frameSize.height);
    NSRect contentRect = [sender contentRectForFrameRect:frameRect];

    CGFloat w = contentRect.size.width;
    CGFloat h = contentRect.size.height;

    if (h <= 0) return frameSize;

    BOOL changed = NO;

    if (g_minWidth > 0 && w < g_minWidth)  { w = g_minWidth; changed = YES; }
    if (g_minHeight > 0 && h < g_minHeight) { h = g_minHeight; changed = YES; }

    CGFloat aspect = w / h;

    if (g_maxAspect > 0 && aspect > g_maxAspect)
    {
        w = h * g_maxAspect;
        changed = YES;
    }
    else if (g_minAspect > 0 && aspect < g_minAspect)
    {
        h = w / g_minAspect;
        changed = YES;
    }

    if (!changed) return frameSize;

    contentRect.size.width = w;
    contentRect.size.height = h;
    NSRect newFrame = [sender frameRectForContentRect:contentRect];
    frameSize.width = newFrame.size.width;
    frameSize.height = newFrame.size.height;

    return frameSize;
}

extern "C"
{
    void WindowConstraint_Init()
    {
        if (g_initialized) return;

        @autoreleasepool
        {
            NSWindow *window = nil;

            for (NSWindow *w in [NSApp windows])
            {
                if ([w isVisible])
                {
                    window = w;
                    break;
                }
            }

            if (!window) return;

            id delegate = [window delegate];
            if (!delegate) return;

            Class cls = object_getClass(delegate);
            SEL sel = @selector(windowWillResize:toSize:);
            Method m = class_getInstanceMethod(cls, sel);

            if (m)
            {
                g_originalIMP = method_setImplementation(m, (IMP)hooked_windowWillResize);
            }
            else
            {
                class_addMethod(cls, sel, (IMP)hooked_windowWillResize, "{CGSize=dd}@:@{CGSize=dd}");
            }

            g_initialized = YES;
        }
    }

    void WindowConstraint_Set(int enabled, float minAspect, float maxAspect, int minWidth, int minHeight)
    {
        g_enabled = enabled;
        g_minAspect = minAspect;
        g_maxAspect = maxAspect;
        g_minWidth = minWidth;
        g_minHeight = minHeight;
    }
}
