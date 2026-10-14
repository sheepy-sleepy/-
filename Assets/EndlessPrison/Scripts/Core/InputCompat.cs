using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EndlessPrison
{
    /// <summary>
    /// 同時支援 Unity 新版 Input System 和舊版 Input Manager。
    /// Unity 6 新專案預設用新版，舊專案用舊版，這裡會自動判斷。
    /// </summary>
    public static class InputCompat
    {
        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null) return Mouse.current.position.ReadValue();
                return Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool LeftClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool RightClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(1);
#endif
            }
        }

        /// <summary>滾輪：往前滾是正數，大約一格 = 1。</summary>
        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current == null) return 0f;
                float v = Mouse.current.scroll.ReadValue().y;
                return Mathf.Abs(v) > 10f ? v / 120f : v;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        public static bool KeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case KeyCode.Q: return kb.qKey.wasPressedThisFrame;
                case KeyCode.E: return kb.eKey.wasPressedThisFrame;
                case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
                case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
                case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
                case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame;
                case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame;
                case KeyCode.Alpha4: return kb.digit4Key.wasPressedThisFrame;
                case KeyCode.Alpha5: return kb.digit5Key.wasPressedThisFrame;
                case KeyCode.Alpha6: return kb.digit6Key.wasPressedThisFrame;
                case KeyCode.Alpha7: return kb.digit7Key.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
