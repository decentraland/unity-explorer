#include <windows.h>

static float g_minAspect = 0.0f;
static float g_maxAspect = 0.0f;
static int g_minWidth = 0;
static int g_minHeight = 0;
static BOOL g_enabled = FALSE;
static WNDPROC g_originalWndProc = NULL;
static BOOL g_initialized = FALSE;

static LRESULT CALLBACK HookedWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (msg == WM_SIZING && g_enabled)
    {
        RECT *rect = (RECT *)lParam;

        RECT windowRect, clientRect;
        GetWindowRect(hwnd, &windowRect);
        GetClientRect(hwnd, &clientRect);

        int borderW = (windowRect.right - windowRect.left) - (clientRect.right - clientRect.left);
        int borderH = (windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top);

        int clientW = (rect->right - rect->left) - borderW;
        int clientH = (rect->bottom - rect->top) - borderH;

        if (clientH <= 0)
            return CallWindowProcW(g_originalWndProc, hwnd, msg, wParam, lParam);

        int newW = clientW;
        int newH = clientH;
        BOOL changed = FALSE;

        if (g_minWidth > 0 && newW < g_minWidth)  { newW = g_minWidth; changed = TRUE; }
        if (g_minHeight > 0 && newH < g_minHeight) { newH = g_minHeight; changed = TRUE; }

        float aspect = (float)newW / (float)newH;

        if (g_maxAspect > 0 && aspect > g_maxAspect)
        {
            newW = (int)(newH * g_maxAspect);
            changed = TRUE;
        }
        else if (g_minAspect > 0 && aspect < g_minAspect)
        {
            newH = (int)(newW / g_minAspect);
            changed = TRUE;
        }

        if (!changed)
            return CallWindowProcW(g_originalWndProc, hwnd, msg, wParam, lParam);

        int windowW = newW + borderW;
        int windowH = newH + borderH;

        /* Adjust the edge the user is dragging */
        switch (wParam)
        {
        case WMSZ_LEFT:
        case WMSZ_TOPLEFT:
        case WMSZ_BOTTOMLEFT:
            rect->left = rect->right - windowW;
            break;
        default:
            rect->right = rect->left + windowW;
            break;
        }

        switch (wParam)
        {
        case WMSZ_TOP:
        case WMSZ_TOPLEFT:
        case WMSZ_TOPRIGHT:
            rect->top = rect->bottom - windowH;
            break;
        default:
            rect->bottom = rect->top + windowH;
            break;
        }

        return TRUE;
    }

    return CallWindowProcW(g_originalWndProc, hwnd, msg, wParam, lParam);
}

__declspec(dllexport) void WindowConstraint_Init(void)
{
    if (g_initialized) return;

    HWND hwnd = GetActiveWindow();
    if (!hwnd) return;

    g_originalWndProc = (WNDPROC)SetWindowLongPtrW(hwnd, GWLP_WNDPROC, (LONG_PTR)HookedWndProc);
    g_initialized = TRUE;
}

__declspec(dllexport) void WindowConstraint_Set(int enabled, float minAspect, float maxAspect, int minWidth, int minHeight)
{
    g_enabled = enabled;
    g_minAspect = minAspect;
    g_maxAspect = maxAspect;
    g_minWidth = minWidth;
    g_minHeight = minHeight;
}
