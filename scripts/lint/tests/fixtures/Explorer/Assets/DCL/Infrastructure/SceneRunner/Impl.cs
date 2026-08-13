using System.Threading;

public class Impl
{
    [ThreadStatic] private static int ts;
    private ThreadLocal<int> tl;
}
