using UnityEngine;

/// <summary>
/// Keeps a focused mobile input above the software keyboard. Android does not
/// expose TouchScreenKeyboard.area, so use the keyboard's visible state and a
/// conservative screen-space target instead.
/// </summary>
public static class MobileKeyboardInputAssist
{
    private const float KeyboardTargetFraction = 0.48f;
    private const float ResizedWindowTargetFraction = 0.58f;

    public static bool IsKeyboardVisible
    {
        get => Application.isMobilePlatform
            && TouchScreenKeyboard.isSupported
            && TouchScreenKeyboard.visible;
    }

    public static Vector2 MoveRootForInput(
        RectTransform root,
        RectTransform focusedInput,
        Vector2 basePosition,
        ref Vector2 velocity,
        float smoothTime = 0.12f)
    {
        if (root == null)
            return basePosition;

        Vector2 targetPosition = basePosition;
        if (IsKeyboardVisible && focusedInput != null && root.parent is RectTransform parent)
        {
            Canvas canvas = focusedInput.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;

            Vector3 inputWorldCenter = focusedInput.TransformPoint(focusedInput.rect.center);
            Vector2 inputScreenCenter = RectTransformUtility.WorldToScreenPoint(camera, inputWorldCenter);
            float targetScreenY = GetTargetScreenY();
            Vector2 targetScreenCenter = new Vector2(inputScreenCenter.x, targetScreenY);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, inputScreenCenter, camera, out Vector2 inputLocal)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, targetScreenCenter, camera, out Vector2 targetLocal))
            {
                targetPosition = root.anchoredPosition + targetLocal - inputLocal;
            }
        }

        root.anchoredPosition = Vector2.SmoothDamp(
            root.anchoredPosition,
            targetPosition,
            ref velocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if (Vector2.SqrMagnitude(root.anchoredPosition - targetPosition) < 0.25f)
        {
            root.anchoredPosition = targetPosition;
            velocity = Vector2.zero;
        }

        return targetPosition;
    }

    private static float GetTargetScreenY()
    {
        Rect keyboardArea = TouchScreenKeyboard.area;
        if (keyboardArea.height > 0f)
            return keyboardArea.yMax + Mathf.Max(24f, Screen.height * 0.02f);

        // Android returns a zero Rect for TouchScreenKeyboard.area. When the
        // window is resized for IME, Screen.height is already the usable area.
        float displayHeight = Screen.currentResolution.height;
        bool windowResized = displayHeight > 0f && Screen.height < displayHeight * 0.85f;
        float fraction = windowResized ? ResizedWindowTargetFraction : KeyboardTargetFraction;
        return Screen.height * fraction;
    }
}