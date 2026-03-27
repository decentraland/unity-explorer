using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    /// <summary>
    /// Simple pool for <see cref="MessageReactionsView"/> instances.
    /// Views are reparented under a hidden root when returned and
    /// reparented under a chat entry when acquired.
    /// </summary>
    public class MessageReactionsViewPool
    {
        private readonly MessageReactionsView prefab;
        private readonly Transform poolRoot;
        private readonly List<MessageReactionsView> available = new ();

        public MessageReactionsViewPool(MessageReactionsView prefab, Transform poolRoot)
        {
            this.prefab = prefab;
            this.poolRoot = poolRoot;
        }

        public MessageReactionsView Get(Transform parent)
        {
            MessageReactionsView view;

            if (available.Count > 0)
            {
                int last = available.Count - 1;
                view = available[last];
                available.RemoveAt(last);
            }
            else
            {
                view = Object.Instantiate(prefab, parent);
            }

            view.transform.SetParent(parent, false);
            view.gameObject.SetActive(true);
            return view;
        }

        public void Return(MessageReactionsView view)
        {
            view.Clear();
            view.gameObject.SetActive(false);
            view.transform.SetParent(poolRoot, false);
            available.Add(view);
        }

        public void Dispose()
        {
            for (int i = 0; i < available.Count; i++)
                Object.Destroy(available[i].gameObject);

            available.Clear();
        }
    }
}
