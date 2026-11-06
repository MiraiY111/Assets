using UnityEngine;
using UnityEngine.EventSystems;

namespace HeyHeyThere.PixelBarsFree
{
    /// <summary>
    /// The demo's EventSystem: with the Input System's UI module when that is the active input (looked
    /// up by name, so the scripts compile without the package), else the Input Manager's. A demo played
    /// on the keys turns `navigation` off, so the arrows don't walk its buttons and Space or Enter
    /// doesn't press the one last clicked.
    /// </summary>
    static class DemoInput
    {
        public static void Ensure(bool navigation = true)
        {
            var events = Object.FindAnyObjectByType<EventSystem>();
            if (events == null)
            {
                events = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                Component module = null;
#if ENABLE_INPUT_SYSTEM
                var type = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (type != null)
                    module = events.gameObject.AddComponent(type);
#endif
                if (module == null)
                    events.gameObject.AddComponent<StandaloneInputModule>();
            }
            events.sendNavigationEvents = navigation;
        }
    }
}
