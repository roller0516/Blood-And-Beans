using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// 낮·밤 매핑과 실제 HUD 배선을 함께 확인한다.
public class SkillIconTests
{
    [Test]
    public void 준비_VFX는_UIParticle이_렌더러_재질로_그린다()
    {
        var slots = AssetDatabase.FindAssets($"{nameof(UISkillSlot)} t:Prefab");
        Assert.AreEqual(1, slots.Length);
        var slot = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(slots[0])).GetComponent<UISkillSlot>();
        var effect = (ParticleSystem)new SerializedObject(slot).FindProperty("readyEffect").objectReferenceValue;
        Assert.IsNotNull(effect);
        // UIParticle은 같은 오브젝트나 부모에 있어야 이 파티클을 모은다. 렌더러 재질이 비면 아무것도 그리지 않는다.
        Assert.IsNotNull(effect.GetComponentInParent<Coffee.UIExtensions.UIParticle>(true));
        Assert.IsNotNull(effect.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        Assert.IsNotNull(effect.GetComponentInParent<Coffee.UIExtensions.UIParticle>(true).GetComponent<CanvasRenderer>());
    }

    [Test]
    public void 준비_완료는_쿨타임이_끝나는_순간에만_발생한다()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Art/UI/Prefabs/Parts/UISkillSlot.prefab");
        try
        {
            var slot = root.GetComponent<UISkillSlot>();
            Assert.IsFalse(slot.SetCooldown(.5f));
            Assert.IsTrue(slot.SetCooldown(0));
            Assert.IsFalse(slot.SetCooldown(0));
            Assert.IsFalse(slot.SetCooldown(.5f));
            slot.ResetCooldown();
            Assert.IsFalse(slot.SetCooldown(0));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

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
