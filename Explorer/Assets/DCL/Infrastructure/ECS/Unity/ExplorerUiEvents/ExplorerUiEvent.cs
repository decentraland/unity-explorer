using DCL.ECSComponents;

namespace ECS.Unity.ExplorerUiEvents
{
    public enum ExplorerUiEventKind
    {
        Opened,
        Closed,
    }

    /// <summary>
    ///     One life cycle event of the explorer panel a scene asked for through <c>openExplorerUi</c>, waiting
    ///     to be written to that scene as a <see cref="PBExplorerUiEventsResult" />. The scene keeps a
    ///     <see cref="System.Collections.Generic.Queue{T}" /> of these; both ends run on the main thread, the
    ///     producer because it enqueues only after switching to it, the consumer because it is an ECS system.
    /// </summary>
    public readonly struct ExplorerUiEvent
    {
        public readonly ExplorerUi Ui;
        public readonly ExplorerUiEventKind Kind;

        public ExplorerUiEvent(ExplorerUi ui, ExplorerUiEventKind kind)
        {
            Ui = ui;
            Kind = kind;
        }
    }
}
