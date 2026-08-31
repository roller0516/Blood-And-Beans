using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// UI 기본 디자인 원본(<see cref="UIThemeConfig"/>)을 만지는 자리.
///
/// 색과 규격을 화면마다 박아 두면 톤을 한 번 바꿀 때 손댈 곳이 흩어진다. 애셋 하나를
/// 원본으로 두고 여기서 만진다. 값을 바꾸면 즉시 애셋에 기록되고(`Undo` 대상), 재생 중이면
/// 씬의 <see cref="UIFontScale"/>에 배율을 다시 먹여 눈으로 바로 확인할 수 있다.
///
/// 애셋은 `Resources`에 있어야 런타임이 찾는다(<see cref="UIThemeConfig.AssetName"/>).
/// 없으면 이 탭이 열릴 때 그 경로에 자동으로 만든다 — 기본값이 코드 기본값과 같아
/// 만들어도 화면이 달라지지 않으므로, 사용자가 버튼을 누를 이유가 없다.
public class UIThemeGroup : DevConsoleGroup
{
    public override string Tab => "화면";
    public override string Title => "UI 테마";

    const string ResourcesDir = "Assets/Resources";
    static string AssetPath => $"{ResourcesDir}/{UIThemeConfig.AssetName}.asset";

    UIThemeConfig config;
    SerializedObject serialized;

    VisualElement fields;
    Label status;

    /// 만들기를 한 번 시도했는가. 실패해도 0.1초마다 다시 만들려 들면 안 된다.
    bool triedCreate;

    protected override void Build(VisualElement group)
    {
        var hint = new Label("값은 바꾸는 즉시 애셋에 저장된다. 재생 중이면 화면에 바로 반영된다.");
        hint.AddToClassList("hint");
        group.Add(hint);

        status = Row(group, "애셋", "-");

        Btn(ButtonRow(group), "애셋 열기", Select);

        // 애셋을 찾은 뒤에 채운다. 없을 수도 있으므로 상자만 먼저 둔다.
        fields = new VisualElement();
        group.Add(fields);
    }

    public override void Refresh(in DevConsoleState state)
    {
        // 애셋은 에디터에서 지웠다 만들 수 있다. 놓치면 계속 빈 화면이 되므로 없을 때만 다시 찾는다.
        if (config == null)
        {
            // 없으면 만든다. 버튼을 누르게 하면 "안 눌러서 값이 저장되지 않는" 상태가
            // 생기는데, 이 애셋은 기본값이 코드 기본값과 같아 있어도 화면이 달라지지 않는다.
            // 즉 만들어서 잃는 것이 없다.
            config = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(AssetPath);
            if (config == null && !triedCreate)
            {
                triedCreate = true;
                config = Create();
            }

            serialized = null;
            fields.Clear();
            if (config != null) BuildFields();
        }

        status.text = config != null ? AssetPath : "만들지 못했다 — Console을 본다";

        // 외부(Inspector)에서 바뀐 값을 창에도 반영한다.
        serialized?.Update();
    }

    void BuildFields()
    {
        serialized = new SerializedObject(config);

        // 애셋의 필드 순서와 [Header]를 그대로 따라간다. 여기서 목록을 다시 적으면
        // 애셋에 항목을 더할 때마다 두 곳을 고쳐야 한다.
        var property = serialized.GetIterator();
        var enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script") continue;

            var field = new PropertyField(property.Copy());
            field.Bind(serialized);
            fields.Add(field);
        }

        // 재생 중에는 글자 배율을 즉시 먹인다. 값을 넣어 보고 되돌리는 일이 잦다.
        fields.TrackSerializedObjectValue(serialized, _ => ApplyLive());
    }

    /// 재생 중인 씬의 글자 배율만 다시 칠한다. 색은 화면을 다시 만들 때 반영된다 —
    /// 이미 그려진 이미지를 훑어 되돌리려면 원본 색을 따로 들고 있어야 하고,
    /// 그만한 값어치가 없다.
    void ApplyLive()
    {
        if (!Application.isPlaying || config == null) return;

        UITheme.UseConfig(config);
        var scalers = Object.FindObjectsByType<UIFontScale>(FindObjectsInactive.Include);
        foreach (var scaler in scalers)
            if (scaler != null) scaler.Apply(config.FontScale);
    }

    /// 기본값 그대로인 애셋을 `Resources`에 만든다. 만들지 못하면 null을 돌려주고,
    /// 그때는 UITheme이 코드 기본값으로 계속 그린다 — 화면이 멈추지는 않는다.
    static UIThemeConfig Create()
    {
        Directory.CreateDirectory(ResourcesDir);

        var created = ScriptableObject.CreateInstance<UIThemeConfig>();
        AssetDatabase.CreateAsset(created, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var loaded = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(AssetPath);
        if (loaded == null)
        {
            CDebug.LogError($"{AssetPath}를 만들지 못했다. 코드 기본값으로 그린다.");
            return null;
        }

        UITheme.UseConfig(loaded);
        CDebug.Log($"{AssetPath}를 만들었다. 개발 콘솔 '화면' 탭에서 값을 만진다.");
        return loaded;
    }

    void Select()
    {
        if (config == null) return;
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }
}
