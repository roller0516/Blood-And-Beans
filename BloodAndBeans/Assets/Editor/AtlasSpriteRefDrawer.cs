using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.U2D;

/// <see cref="AtlasSpriteRef"/>의 Inspector. 스프라이트를 끌어다 놓으면 그 스프라이트를 담은 아틀라스를 찾아
/// GUID와 이름만 저장한다. 오브젝트 참조는 저장하지 않는다.
[CustomPropertyDrawer(typeof(AtlasSpriteRef))]
public sealed class AtlasSpriteRefDrawer : PropertyDrawer
{
    const string AtlasGuidField = "atlasGuid";
    const string SpriteNameField = "spriteName";

    // 배열 칸들이 드로어 인스턴스 하나를 같이 쓰므로, 다시 그릴 때마다 에셋을 찾지 않게 키별로 들고 있는다.
    // 에셋이 바뀌면 결과가 낡으니 비운다.
    static readonly System.Collections.Generic.Dictionary<string, (Sprite sprite, string problem)> Cache = new();
    [InitializeOnLoadMethod] static void ClearOnProjectChange() => EditorApplication.projectChanged += Cache.Clear;

    Sprite cachedSprite;
    string cachedProblem;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        Resolve(property);
        var line = EditorGUIUtility.singleLineHeight;
        return cachedProblem == null ? line : line * 2f + EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var guid = property.FindPropertyRelative(AtlasGuidField);
        var spriteName = property.FindPropertyRelative(SpriteNameField);
        Resolve(property);

        EditorGUI.BeginProperty(position, label, property);
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.BeginChangeCheck();
        var picked = (Sprite)EditorGUI.ObjectField(line, label, cachedSprite, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck()) Assign(guid, spriteName, picked);

        if (cachedProblem != null)
        {
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.HelpBox(EditorGUI.IndentedRect(line), cachedProblem, MessageType.Error);
        }
        EditorGUI.EndProperty();
    }

    static void Assign(SerializedProperty guid, SerializedProperty spriteName, Sprite sprite)
    {
        if (sprite == null)
        {
            guid.stringValue = string.Empty;
            spriteName.stringValue = string.Empty;
            return;
        }

        var atlas = FindAtlasOf(sprite, out var atlasPath);
        if (atlas == null)
        {
            CDebug.LogError($"{nameof(AtlasSpriteRef)}: '{sprite.name}'을 담은 아틀라스가 없다. 스프라이트를 아틀라스 폴더에 넣는다.", sprite);
            return;
        }

        guid.stringValue = AssetDatabase.AssetPathToGUID(atlasPath);
        spriteName.stringValue = sprite.name;
    }

    static SpriteAtlas FindAtlasOf(Sprite sprite, out string atlasPath)
    {
        foreach (var id in AssetDatabase.FindAssets("t:" + nameof(SpriteAtlas)))
        {
            atlasPath = AssetDatabase.GUIDToAssetPath(id);
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas != null && atlas.CanBindTo(sprite)) return atlas;
        }
        atlasPath = null;
        return null;
    }

    // 저장된 GUID·이름이 가리키는 원본 스프라이트와 문제를 푼다. 값이 바뀔 때만 다시 찾는다.
    void Resolve(SerializedProperty property)
    {
        var guid = property.FindPropertyRelative(AtlasGuidField).stringValue;
        var spriteName = property.FindPropertyRelative(SpriteNameField).stringValue;
        var key = guid + "[" + spriteName + "]";
        if (!Cache.TryGetValue(key, out var hit))
        {
            Find(guid, spriteName);
            hit = (cachedSprite, cachedProblem);
            Cache[key] = hit;
        }
        (cachedSprite, cachedProblem) = hit;
    }

    void Find(string guid, string spriteName)
    {
        cachedSprite = null;
        cachedProblem = null;
        if (string.IsNullOrEmpty(guid)) return;

        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(guid));
        if (atlas == null)
        {
            cachedProblem = $"아틀라스({guid})를 찾을 수 없다. 지워졌거나 옮겨졌다.";
            return;
        }

        // SpriteAtlas.GetSprite는 복제본을 만든다. 원본 스프라이트를 이름으로 찾아 그 아틀라스에 묶이는지 본다.
        foreach (var id in AssetDatabase.FindAssets($"t:{nameof(Sprite)} {spriteName}"))
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(id)))
            {
                if (asset is Sprite sprite && sprite.name == spriteName && atlas.CanBindTo(sprite))
                {
                    cachedSprite = sprite;
                    break;
                }
            }
            if (cachedSprite != null) break;
        }

        if (cachedSprite == null)
            cachedProblem = $"'{atlas.name}' 아틀라스에 '{spriteName}' 스프라이트가 없다. 이름이 바뀌었거나 다른 아틀라스로 옮겨졌다.";
        else if (AddressableAssetSettingsDefaultObject.Settings?.FindAssetEntry(guid) == null)
            cachedProblem = $"'{atlas.name}' 아틀라스가 Addressables 그룹에 없다. 런타임에 불러오지 못한다.";
    }
}
