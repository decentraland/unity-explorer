/*
 * Linux (X11 / XWayland) implementation of the WindowResizeConstraint plugin.
 *
 * Same exported ABI as the Windows (.c) and macOS (.mm) builds:
 *   void WindowConstraint_Init(void);
 *   void WindowConstraint_Set(int enabled, float minAspect, float maxAspect,
 *                             int minWidth, int minHeight);
 *
 * X11 expresses resize constraints declaratively: WM_NORMAL_HINTS carries a
 * minimum client size (PMinSize) and an aspect-ratio range (PAspect) that the
 * window manager enforces while the user drags — the protocol-level analogue
 * of the WM_SIZING clamp on Windows. Setting the hints is therefore the whole
 * job; no event hook is required.
 *
 * The plugin keeps its own Display connection (safe alongside Unity/SDL's own)
 * and finds the player's top-level window by matching _NET_WM_PID against this
 * process, preferring the largest match so popups never shadow the main window.
 * The lookup retries on each Set() until a window exists, because Init() can
 * run before the window is mapped.
 *
 * Build (any gcc with libX11 headers):
 *   gcc -shared -fPIC -O2 -o libWindowResizeConstraint.so WindowResizeConstraint.c -lX11
 */

#include <X11/Xlib.h>
#include <X11/Xutil.h>
#include <X11/Xatom.h>
#include <stdint.h>
#include <unistd.h>

static Display *g_display = NULL;
static Window g_window = 0;
static Atom g_netWmPid = None;

/* Aspect ratios travel as X11 rationals; four decimals keeps 21:9 exact enough. */
#define ASPECT_DENOMINATOR 10000

static int WindowPid(Display *display, Window window)
{
    Atom actualType;
    int actualFormat;
    unsigned long itemCount = 0, bytesAfter = 0;
    unsigned char *data = NULL;
    int pid = -1;

    if (XGetWindowProperty(display, window, g_netWmPid, 0, 1, False, XA_CARDINAL,
                           &actualType, &actualFormat, &itemCount, &bytesAfter, &data) == Success
        && data != NULL)
    {
        if (itemCount == 1 && actualFormat == 32)
            pid = (int)*(unsigned long *)data;
        XFree(data);
    }

    return pid;
}

static void FindLargestWindowOfPid(Display *display, Window root, int pid,
                                   Window *best, unsigned long *bestArea)
{
    Window parent, *children = NULL;
    unsigned int childCount = 0;

    if (WindowPid(display, root) == pid)
    {
        XWindowAttributes attributes;

        if (XGetWindowAttributes(display, root, &attributes)
            && attributes.width > 0 && attributes.height > 0)
        {
            unsigned long area = (unsigned long)attributes.width * (unsigned long)attributes.height;

            if (area > *bestArea)
            {
                *best = root;
                *bestArea = area;
            }
        }
    }

    if (XQueryTree(display, root, &root, &parent, &children, &childCount) && children != NULL)
    {
        for (unsigned int i = 0; i < childCount; i++)
            FindLargestWindowOfPid(display, children[i], pid, best, bestArea);
        XFree(children);
    }
}

static Window FindPlayerWindow(Display *display)
{
    Window best = 0;
    unsigned long bestArea = 0;

    FindLargestWindowOfPid(display, DefaultRootWindow(display), (int)getpid(), &best, &bestArea);
    return best;
}

void WindowConstraint_Init(void)
{
    if (g_display != NULL) return;

    g_display = XOpenDisplay(NULL);
    if (g_display == NULL) return;

    g_netWmPid = XInternAtom(g_display, "_NET_WM_PID", False);
    g_window = FindPlayerWindow(g_display);
}

void WindowConstraint_Set(int enabled, float minAspect, float maxAspect, int minWidth, int minHeight)
{
    if (g_display == NULL) return;

    if (g_window == 0)
    {
        g_window = FindPlayerWindow(g_display);
        if (g_window == 0) return;
    }

    XSizeHints hints;
    long supplied = 0;

    if (XGetWMNormalHints(g_display, g_window, &hints, &supplied) == 0)
        hints.flags = 0;

    hints.flags &= ~(PMinSize | PAspect);

    if (enabled)
    {
        if (minWidth > 0 || minHeight > 0)
        {
            hints.flags |= PMinSize;
            hints.min_width = minWidth > 0 ? minWidth : 1;
            hints.min_height = minHeight > 0 ? minHeight : 1;
        }

        if (minAspect > 0.0f || maxAspect > 0.0f)
        {
            hints.flags |= PAspect;
            hints.min_aspect.x = minAspect > 0.0f ? (int)(minAspect * ASPECT_DENOMINATOR) : 1;
            hints.min_aspect.y = ASPECT_DENOMINATOR;
            hints.max_aspect.x = maxAspect > 0.0f ? (int)(maxAspect * ASPECT_DENOMINATOR) : INT32_MAX;
            hints.max_aspect.y = maxAspect > 0.0f ? ASPECT_DENOMINATOR : 1;
        }
    }

    XSetWMNormalHints(g_display, g_window, &hints);
    XFlush(g_display);
}
