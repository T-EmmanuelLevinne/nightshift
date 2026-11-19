using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Single source of truth for HUD colors, fonts, sizing, and IMGUI rendering
/// shared across HouseScene, GasScene2, and HorrorInteractionReticle so
/// task headers, dialogue boxes, and interaction prompts look 100% consistent.
/// </summary>
public static class HorrorHudStyle
{
    public const int TitleFontSize = 26;
    public const int DialogueFontSize = 19;
    public const int TaskHeaderFontSize = 17;
    public const int TaskSubFontSize = 14;
    public const int PromptFontSize = 16;
    public const float ReticleDotDiameter = 8f;

    public const float TaskStartX = 26f;
    public const float TaskStartY = 24f;

    public static readonly string[] RetroFontNames = new string[]
    {
        "Lucida Console",
        "Courier New",
        "Consolas",
        "Courier"
    };

    // Task HUD colors
    public static readonly Color ActiveTaskColor = new Color(0.96f, 0.88f, 0.70f, 0.96f);
    public static readonly Color CompletedTaskColor = new Color(0.65f, 0.92f, 0.65f, 0.98f);
    public static readonly Color TaskSubColor = new Color(0.85f, 0.85f, 0.80f, 0.92f);
    public static readonly Color CompletedTaskSubColor = new Color(0.72f, 0.92f, 0.72f, 0.95f);
    public static readonly Color TaskShadowColor = new Color(0.01f, 0.01f, 0.01f, 0.96f);

    // Dialogue UI colors
    public static readonly Color DialogueBackgroundColor = new Color(0.03f, 0.03f, 0.04f, 0.75f);
    public static readonly Color DialogueBorderColor = new Color(0.35f, 0.35f, 0.32f, 0.55f);
    public static readonly Color DialogueTextColor = new Color(0.93f, 0.91f, 0.83f, 1.0f);
    public static readonly Color DialogueShadowColor = new Color(0.01f, 0.01f, 0.01f, 0.95f);

    // Reticle & "E to Interact" prompt colors
    public static readonly Color ReticleNormalDotColor = new Color(0.88f, 0.88f, 0.82f, 0.85f);
    public static readonly Color ReticleHoverDotColor = new Color(1.0f, 0.95f, 0.65f, 1.0f);
    public static readonly Color ReticleDotBorderColor = new Color(0f, 0f, 0f, 0.85f);
    public static readonly Color PromptTextColor = new Color(0.92f, 0.90f, 0.80f, 1.0f);
    public static readonly Color PromptShadowColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);

    private static Font cachedRetroFont;
    private static GUIStyle skipHintStyle;
    private static GUIStyle skipHintShadowStyle;

    public static bool WasSkipDialoguePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            return true;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.H);
#endif
    }

    public static IEnumerator WaitSkippableSeconds(float duration, Func<bool> shouldSkip)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (shouldSkip != null && shouldSkip())
            {
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static void DrawSkipHintLabel(Rect rect, TextAnchor alignment, float alpha)
    {
        if (alpha <= 0.05f) return;

        if (skipHintStyle == null)
        {
            skipHintStyle = new GUIStyle(GUI.skin.label)
            {
                font = GetRetroFont(),
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = alignment
            };
            skipHintShadowStyle = new GUIStyle(skipHintStyle);
        }

        skipHintStyle.alignment = alignment;
        skipHintShadowStyle.alignment = alignment;

        Color fg = new Color(0.84f, 0.82f, 0.74f, 0.88f * Mathf.Clamp01(alpha));
        Color shadow = new Color(0.01f, 0.01f, 0.01f, 0.92f * Mathf.Clamp01(alpha));
        DrawOutlinedLabel(rect, "[H] Skip", skipHintShadowStyle, shadow, skipHintStyle, fg);
    }

    public static Font GetRetroFont()
    {
        if (cachedRetroFont == null)
        {
            cachedRetroFont = Font.CreateDynamicFontFromOSFont(RetroFontNames, 24);
        }
        return cachedRetroFont;
    }

    public static GUIStyle CreateTitleStyle()
    {
        Font font = GetRetroFont();
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = TitleFontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        if (font != null) style.font = font;
        return style;
    }

    public static GUIStyle CreateDialogueStyle()
    {
        Font font = GetRetroFont();
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = DialogueFontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        if (font != null) style.font = font;
        return style;
    }

    public static GUIStyle CreateTaskHeaderStyle()
    {
        Font font = GetRetroFont();
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = TaskHeaderFontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        if (font != null) style.font = font;
        return style;
    }

    public static GUIStyle CreateTaskSubStyle(GUIStyle headerStyle)
    {
        GUIStyle style = new GUIStyle(headerStyle)
        {
            fontSize = TaskSubFontSize,
            fontStyle = FontStyle.Normal
        };
        return style;
    }

    public static GUIStyle CreatePromptStyle()
    {
        Font font = GetRetroFont();
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = PromptFontSize,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        if (font != null) style.font = font;
        style.normal.textColor = PromptTextColor;
        return style;
    }

    public static void DrawOutlinedLabel(
        Rect rect,
        string text,
        GUIStyle shadowStyle,
        Color shadowColor,
        GUIStyle foregroundStyle,
        Color foregroundColor)
    {
        shadowStyle.normal.textColor = shadowColor;
        GUI.Label(new Rect(rect.x - 1f, rect.y, rect.width, rect.height), text, shadowStyle);
        GUI.Label(new Rect(rect.x + 1f, rect.y, rect.width, rect.height), text, shadowStyle);
        GUI.Label(new Rect(rect.x, rect.y - 1f, rect.width, rect.height), text, shadowStyle);
        GUI.Label(new Rect(rect.x, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
        GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, shadowStyle);

        foregroundStyle.normal.textColor = foregroundColor;
        GUI.Label(rect, text, foregroundStyle);
    }

    public static void DrawDialogueBox(
        Texture2D pixelTexture,
        string displayedText,
        bool isActivelyTyping,
        float alpha,
        GUIStyle dialogueStyle,
        GUIStyle dialogueShadowStyle)
    {
        float sw = Screen.width;
        float sh = Screen.height;

        string textToDraw = displayedText ?? string.Empty;
        if (isActivelyTyping && Mathf.FloorToInt(Time.unscaledTime * 4.5f) % 2 == 0)
        {
            textToDraw += "_";
        }

        dialogueStyle.wordWrap = false;
        dialogueShadowStyle.wordWrap = false;

        GUIContent content = new GUIContent(textToDraw);
        float maxBoxWidth = Mathf.Max(520f, sw - 32f);
        float measuredWidth = dialogueStyle.CalcSize(content).x + 44f;
        float boxWidth = Mathf.Clamp(Mathf.Max(sw * 0.68f, measuredWidth), 520f, maxBoxWidth);
        float boxHeight = 56f;

        if (measuredWidth > maxBoxWidth)
        {
            dialogueStyle.wordWrap = true;
            dialogueShadowStyle.wordWrap = true;
            boxHeight = Mathf.Max(56f, dialogueStyle.CalcHeight(content, boxWidth - 32f) + 24f);
        }

        float boxX = (sw - boxWidth) * 0.5f;
        float boxY = sh - 155f;
        Rect boxRect = new Rect(boxX, boxY, boxWidth, boxHeight);

        GUI.color = new Color(
            DialogueBackgroundColor.r,
            DialogueBackgroundColor.g,
            DialogueBackgroundColor.b,
            DialogueBackgroundColor.a * alpha);
        GUI.DrawTexture(boxRect, pixelTexture);

        GUI.color = new Color(
            DialogueBorderColor.r,
            DialogueBorderColor.g,
            DialogueBorderColor.b,
            DialogueBorderColor.a * alpha);
        GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, boxRect.width, 1.5f), pixelTexture);
        GUI.DrawTexture(new Rect(boxRect.x, boxRect.yMax - 1.5f, boxRect.width, 1.5f), pixelTexture);

        GUI.color = Color.white;

        Rect textRect = new Rect(boxRect.x + 16f, boxRect.y + 10f, boxRect.width - 32f, boxRect.height - 20f);
        Color shadowCol = new Color(
            DialogueShadowColor.r,
            DialogueShadowColor.g,
            DialogueShadowColor.b,
            DialogueShadowColor.a * alpha);
        Color fgCol = new Color(
            DialogueTextColor.r,
            DialogueTextColor.g,
            DialogueTextColor.b,
            alpha);

        DrawOutlinedLabel(textRect, textToDraw, dialogueShadowStyle, shadowCol, dialogueStyle, fgCol);
        DrawSkipHintLabel(new Rect(boxRect.xMax - 124f, boxRect.yMax + 4f, 120f, 20f), TextAnchor.MiddleRight, alpha);
    }

    public static void DrawTaskLine(
        float x,
        float y,
        string text,
        Color textColor,
        GUIStyle taskStyle,
        GUIStyle shadowStyle)
    {
        shadowStyle.normal.textColor = TaskShadowColor;
        taskStyle.normal.textColor = textColor;

        Rect rect = new Rect(x, y, 620f, 22f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
        GUI.Label(rect, text, taskStyle);
    }

    public static void DrawCenteredTitle(
        string displayedText,
        bool isActivelyTyping,
        float alpha,
        GUIStyle titleStyle,
        GUIStyle titleShadowStyle)
    {
        float sw = Screen.width;
        float sh = Screen.height;

        string textToDraw = displayedText ?? string.Empty;
        if (isActivelyTyping && Mathf.FloorToInt(Time.unscaledTime * 4.5f) % 2 == 0)
        {
            textToDraw += "_";
        }

        float boxWidth = Mathf.Clamp(sw * 0.85f, 480f, 980f);
        float boxHeight = 110f;
        float boxX = (sw - boxWidth) * 0.5f;
        float boxY = sh * 0.5f - (boxHeight * 0.5f);
        Rect rect = new Rect(boxX, boxY, boxWidth, boxHeight);

        Color shadowCol = new Color(0f, 0f, 0f, 0.95f * alpha);
        titleShadowStyle.normal.textColor = shadowCol;
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), textToDraw, titleShadowStyle);

        Color textCol = new Color(DialogueTextColor.r, DialogueTextColor.g, DialogueTextColor.b, Mathf.Clamp01(alpha));
        titleStyle.normal.textColor = textCol;
        GUI.Label(rect, textToDraw, titleStyle);

        if (!string.IsNullOrEmpty(displayedText))
        {
            DrawSkipHintLabel(new Rect((sw - 160f) * 0.5f, boxY + boxHeight + 10f, 160f, 20f), TextAnchor.MiddleCenter, alpha);
        }
    }
}

