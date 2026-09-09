using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// 가방 아이콘의 두 얼굴이 프리팹에 이어져 있는지 본다.
///
/// 규칙 자체(`bagIconImage.sprite` 교체)는 눈에 보이는 한 줄이라 굳이 재지 않는다. 실제로
/// 조용히 깨지는 것은 배선이다 — 프리팹을 다시 저장하다 스프라이트 참조가 빠져도 화면에는
/// 아무 오류가 없고 가방이 그냥 안 바뀔 뿐이다 (이 테스트를 쓰게 만든 사고가 그것이다).
public class MatchHudBagIconTests
{
    [Test]
    public void 열린_가방과_닫힌_가방이_둘_다_이어져_있다()
    {
        // 경로로 찾지 않는다. UI 프리팹은 폴더째 옮겨 다닌다.
        var guids = AssetDatabase.FindAssets("UIMatchHudScreen t:Prefab");
        Assert.AreEqual(1, guids.Length, "UIMatchHudScreen 프리팹이 하나여야 한다");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        var hud = prefab.GetComponentInChildren<UIMatchHudScreen>(true);
        Assert.IsNotNull(hud, "프리팹에 UIMatchHudScreen이 없다");

        var serialized = new SerializedObject(hud);
        var closed = serialized.FindProperty("bagClosedSprite").objectReferenceValue as Sprite;
        var open = serialized.FindProperty("bagOpenSprite").objectReferenceValue as Sprite;

        Assert.IsNotNull(closed, "닫힌 가방 스프라이트가 비었다");
        Assert.IsNotNull(open, "열린 가방 스프라이트가 비었다");
        Assert.AreNotSame(closed, open, "두 칸에 같은 그림이 들어가면 아이콘이 바뀌지 않는다");

        // 0이면 어떤 적재량도 구간에 들지 못해 늘 닫힌 채로 남는다.
        Assert.Greater(serialized.FindProperty("bagOpenUntilRatio").floatValue, 0f);
    }
}
