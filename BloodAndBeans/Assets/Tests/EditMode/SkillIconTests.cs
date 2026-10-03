using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// 낮·밤 매핑과 실제 HUD 배선을 함께 확인한다.
public class SkillIconTests
{
    [TestCase(Phase.Day, DaySkill.Ignite, NightSkill.WillOWisp, "igniteIcon")]
    [TestCase(Phase.Day, DaySkill.Glide, NightSkill.Echo, "glideIcon")]
    [TestCase(Phase.Night, DaySkill.Ignite, NightSkill.WillOWisp, "wispIcon")]
    [TestCase(Phase.Night, DaySkill.Glide, NightSkill.Echo, "echoIcon")]
    [TestCase(Phase.Transition, DaySkill.Glide, NightSkill.Echo, null)]
    [TestCase(Phase.Day, DaySkill.None, NightSkill.None, null)]
    [TestCase(Phase.Night, DaySkill.Refine, NightSkill.Appraise, null)]
    public void 스킬과_페이즈에_맞는_그림이_이어져_있다(Phase phase, DaySkill day, NightSkill night, string field)
    {
        var guids = AssetDatabase.FindAssets("UIMatchHudScreen t:Prefab");
        Assert.AreEqual(1, guids.Length);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        var slot = root.GetComponentInChildren<UIDaySkillSlot>(true);
        Assert.IsNotNull(slot);
        var data = new SerializedObject(slot);
        var part = data.FindProperty("slot").objectReferenceValue as UISkillSlot;
        Assert.IsNotNull(part);
        var partData = new SerializedObject(part);
        var icon = partData.FindProperty("icon").objectReferenceValue as Component;
        Assert.IsNotNull(icon);
        var image = new SerializedObject(icon);
        Assert.IsFalse(image.FindProperty("m_RaycastTarget").boolValue);
        Assert.IsTrue(image.FindProperty("m_PreserveAspect").boolValue);
        var count = partData.FindProperty("count").objectReferenceValue as Component;
        Assert.IsNotNull(count);
        Assert.Less(icon.transform.GetSiblingIndex(), count.transform.GetSiblingIndex());

        var expected = field == null ? null : data.FindProperty(field).objectReferenceValue as Sprite;
        if (field != null) Assert.IsNotNull(expected, field);
        var method = typeof(UIDaySkillSlot).GetMethod("IconFor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.AreSame(expected, method.Invoke(slot, new object[] { phase, day, night }));
    }
}
