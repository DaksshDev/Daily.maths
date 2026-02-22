using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DaksshDev.Toaster
{
public class ToastHeaderAttribute : PropertyAttribute
{
    public string title;
    public ToastHeaderAttribute(string title) => this.title = title;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ToastHeaderAttribute))]
public class ToastHeaderDrawer : DecoratorDrawer
{
    private const string IconPath = "Assets/EditorTools/InspectorGUIS/icons/toast_icon.png";

    // Palette
    private static readonly Color FillColor      = new Color(1.00f, 0.88f, 0.30f, 1f); 
    private static readonly Color BorderColor     = new Color(1.00f, 0.55f, 0.00f, 1f);  
    private static readonly Color HighlightColor  = new Color(1.00f, 0.97f, 0.65f, 0.85f);
    private static readonly Color CrustColor      = new Color(0.85f, 0.55f, 0.10f, 1f);  
    private static readonly Color ShadowColor     = new Color(0.70f, 0.40f, 0.00f, 0.55f); 
    private static readonly Color InnerBevelColor = new Color(1.00f, 0.75f, 0.15f, 1f); 
    private static readonly Color TextColor       = new Color(0.10f, 0.05f, 0.00f, 1f);  

    private static Texture2D s_icon;
    private static GUIStyle  s_textStyle;

    private const float TotalHeight  = 80f;
    private const float CardH        = 58f;
    private const float CardMarginX  = 8f;
    private const float CardMarginY  = 10f;   //prevent-border clipping
    private const float IconSize     = 38f;
    private const float BorderW      = 4f;
    private const float TopRadius    = 10f;
    private const float BottomRadius = 3f;
    private const float CrustH       = 6f;   // bottom crust stripe height
    private const float HighlightH   = 5f;   // top shine strip height
    private const float BevelW       = 3f;   // inner bevel thickness

    public override float GetHeight() => TotalHeight;

    public override void OnGUI(Rect position)
    {
        var attr = (ToastHeaderAttribute)attribute;
        EnsureAssets();

        Rect card = new Rect(
            position.x + CardMarginX,
            position.y + CardMarginY,
            position.width - CardMarginX * 2f,
            CardH
        );
        
        Rect shadow = new Rect(card.x + 3f, card.y + 4f, card.width, card.height);
        DrawToast(shadow, ShadowColor, TopRadius, BottomRadius);
        Rect border = new Rect(card.x - BorderW, card.y - BorderW,
            card.width + BorderW * 2f, card.height + BorderW * 2f);
        DrawToast(border, BorderColor, TopRadius + BorderW, BottomRadius + BorderW);
        
        DrawToast(card, FillColor, TopRadius, BottomRadius);
        Rect bevel = new Rect(card.x + BevelW, card.y + BevelW,
            card.width - BevelW * 2f, card.height - BevelW * 2f);
        DrawToastOutlineOnly(bevel, InnerBevelColor, TopRadius - BevelW, BottomRadius, BevelW);
        
        Rect crustRect = new Rect(card.x, card.yMax - CrustH, card.width, CrustH);
        EditorGUI.DrawRect(new Rect(crustRect.x + BottomRadius, crustRect.y,
            crustRect.width - BottomRadius * 2f, CrustH), CrustColor);
        EditorGUI.DrawRect(new Rect(crustRect.x, crustRect.y, BottomRadius, CrustH), CrustColor);
        EditorGUI.DrawRect(new Rect(crustRect.xMax - BottomRadius, crustRect.y, BottomRadius, CrustH), CrustColor);
        
        Rect hlRect = new Rect(card.x + BevelW * 2f, card.y + BevelW * 2f,
            card.width - BevelW * 4f, HighlightH);
        EditorGUI.DrawRect(new Rect(hlRect.x + 4f, hlRect.y, hlRect.width - 8f, hlRect.height),
            HighlightColor);
        
        float iconY = card.y + (card.height - IconSize) * 0.5f;
        Rect iconRect = new Rect(card.x + 12f, iconY, IconSize, IconSize);

        if (s_icon != null)
            GUI.DrawTexture(iconRect, s_icon, ScaleMode.ScaleToFit);
        else
            GUI.Label(iconRect, "🍞", new GUIStyle { fontSize = 28, alignment = TextAnchor.MiddleCenter });
        
        float textX = iconRect.xMax + 8f;
        Rect textRect = new Rect(textX, card.y, card.xMax - textX - 10f, card.height);
        GUI.Label(textRect, attr.title, s_textStyle);
    }
    
    private static void DrawToast(Rect r, Color color, float topRadius, float bottomRadius)
    {
        topRadius    = Mathf.Min(topRadius,    r.width * 0.5f, r.height * 0.5f);
        bottomRadius = Mathf.Min(bottomRadius, r.width * 0.5f, r.height * 0.5f);

        //rough toast shape 
        EditorGUI.DrawRect(new Rect(r.x + bottomRadius, r.y + topRadius,
            r.width - bottomRadius * 2f, r.height - topRadius), color);
        EditorGUI.DrawRect(new Rect(r.x + topRadius, r.y,
            r.width - topRadius * 2f, topRadius), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y + topRadius,
            topRadius, r.height - topRadius), color);
        EditorGUI.DrawRect(new Rect(r.xMax - topRadius, r.y + topRadius,
            topRadius, r.height - topRadius), color);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - bottomRadius,
            bottomRadius, bottomRadius), color);
        EditorGUI.DrawRect(new Rect(r.xMax - bottomRadius, r.yMax - bottomRadius,
            bottomRadius, bottomRadius), color);
    }
    
    private static void DrawToastOutlineOnly(Rect r, Color color, float topRadius, float bottomRadius, float thickness)
    {
        topRadius    = Mathf.Max(0, Mathf.Min(topRadius,    r.width * 0.5f, r.height * 0.5f));
        bottomRadius = Mathf.Max(0, Mathf.Min(bottomRadius, r.width * 0.5f, r.height * 0.5f));
        
        EditorGUI.DrawRect(new Rect(r.x + topRadius, r.y, r.width - topRadius * 2f, thickness), color);
        EditorGUI.DrawRect(new Rect(r.x + bottomRadius, r.yMax - thickness,
            r.width - bottomRadius * 2f, thickness), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y + topRadius, thickness, r.height - topRadius - bottomRadius), color);
        EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y + topRadius, thickness,
            r.height - topRadius - bottomRadius), color);
    }

    private static void EnsureAssets()
    {
        if (s_icon == null)
            s_icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

        if (s_textStyle == null)
        {
            Font customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/EditorTools/InspectorGUIS/fonts/CN.ttf");

            s_textStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 20,
                font      = customFont != null ? customFont : EditorStyles.boldLabel.font,
                alignment = TextAnchor.MiddleLeft,
                wordWrap  = false,
                clipping  = TextClipping.Clip,
                normal    = { textColor = TextColor },
                hover     = { textColor = TextColor },
                active    = { textColor = TextColor },
                focused   = { textColor = TextColor },
                onNormal  = { textColor = TextColor },
                onHover   = { textColor = TextColor },
                onActive  = { textColor = TextColor },
                onFocused = { textColor = TextColor },
            };
        }
    }
}
#endif
    
}