using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DesiLudo3D.Gameplay
{
    /// <summary>Gray-box input helper — compiles under ANY Active Input
    /// Handling setting (legacy, new, or both) via the engine's own defines,
    /// so first-open behaviour is deterministic regardless of project input
    /// config (IL-017).</summary>
    public static class GrayBoxInput
    {
        public static bool TapDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.press.wasPressedThisFrame) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) return true;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
            return false;
#else
            return false;
#endif
        }

        public static Vector2 PointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null) return Touchscreen.current.position.ReadValue();
            return Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0) return Input.GetTouch(0).position;
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }
    }
}
