using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ReturnMagicTests
{
    [Test]
    public void ReturnCircle_MatchesWorldRadius_AndTeleportAssetsAreWired()
    {
        var zone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/ReturnZone.prefab");
        var visual = zone.GetComponent<ReturnZoneVisuals>();
        Assert.That(visual, Is.Not.Null);
        var so = new SerializedObject(visual);
        var circle = (Transform)so.FindProperty("circle").objectReferenceValue;
        var radius = zone.GetComponent<ReturnZone>().Radius;
        Assert.That(circle.lossyScale.x, Is.EqualTo(radius).Within(0.001f));
        Assert.That(circle.lossyScale.z, Is.EqualTo(radius).Within(0.001f));
        Assert.That(zone.GetComponent<MeshRenderer>().enabled, Is.False);
        Assert.That(circle.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(4));
        Assert.That(AssetDatabase.GetAssetPath(circle.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_MainTex")),
            Is.EqualTo("Assets/Art/VFX/ReturnMagicCircle.png"), "바닥 문양 텍스처가 연결되지 않았다.");

        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Character/Prefabs/Player.prefab");
        var look = new SerializedObject(player.GetComponent<PlayerVisuals>());
        Assert.That(look.FindProperty("teleportAfterimageMaterial").objectReferenceValue, Is.Not.Null);
        foreach (var name in new[] { "TeleportDeparture", "TeleportArrival" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/VFX/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>())
                Assert.That(ps.main.loop, Is.False, name + "가 풀로 돌아가지 않는다.");
        }
    }
}
