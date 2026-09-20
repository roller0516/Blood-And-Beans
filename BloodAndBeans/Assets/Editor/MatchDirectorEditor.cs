using UnityEditor;
using UnityEngine;

/// `MatchDirector` Inspector에 숲 굽기 버튼을 붙인다. 메뉴(`Blood & Beans`)와 같은
/// 일을 하며, 씨앗을 보면서 그 자리에서 눌러 볼 수 있게 한 것뿐이다.
[CustomEditor(typeof(MatchDirector))]
public class MatchDirectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("숲 굽기", EditorStyles.boldLabel);

        // 굽기는 편집 시점 전용이다. 플레이 중에 눌러도 씬에 남지 않는다.
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("이 씨앗으로 굽기")) Run(ForestMapBuilder.Build);
                if (GUILayout.Button("씨앗 새로 뽑아 굽기")) Run(ForestMapBuilder.Reroll);
            }

            if (GUILayout.Button("씨앗 훑기 (씬은 그대로)")) Run(ForestMapBuilder.VerifySeeds);
        }

        EditorGUILayout.HelpBox(
            "씨앗이나 밀도를 바꾸면 나무와 상자 위치가 함께 바뀐다. 도구는 씬을 저장하지 않으니 "
            + "마음에 들면 Ctrl+S로 저장하고, 아니면 저장하지 않고 씬을 다시 열면 된다.",
            MessageType.Info);
    }

    /// Inspector를 그리는 도중에 씬을 갈아엎고 저장하면 GUI 상태가 꼬인다. 한 프레임 미룬다.
    static void Run(EditorApplication.CallbackFunction action) => EditorApplication.delayCall += action;
}
