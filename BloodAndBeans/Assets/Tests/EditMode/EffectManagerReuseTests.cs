using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// `EffectManager.StopAfterAsync`가 재생 세대를 확인하는지 검증한다 (Client/EffectManager.cs).
///
/// 예전에는 취소 토큰이 매니저 파괴에만 걸려서, 지속 연출을 조기에 끊고 같은 파티클
/// 인스턴스가 풀에서 다시 나가 새로 재생돼도 옛 타이머가 그대로 남아 있다가 새 재생을
/// 멈춰 버릴 수 있었다. 진짜 싱글턴을 그대로 쓰고, 세대 검사가 걸린 private 메서드만
/// 리플렉션으로 부른다 — Awake와 Addressables 로딩을 다시 태우면 실제 싱글턴과 충돌한다.
public class EffectManagerReuseTests
{
    [UnitySetUp]
    public IEnumerator StartPlayMode()
    {
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator StopPlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator StaleTimer_DoesNotStopEffectTakenForANewGeneration()
    {
        var wait = Until(() => EffectManager.Instance != null);
        while (wait.MoveNext()) yield return wait.Current;
        var manager = EffectManager.Instance;

        var generationField = typeof(EffectManager).GetField("generation", BindingFlags.NonPublic | BindingFlags.Instance);
        var generation = (Dictionary<ParticleSystem, int>)generationField.GetValue(manager);
        var stopAfterAsync = typeof(EffectManager).GetMethod("StopAfterAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        var effectGO = new GameObject("EffectManagerReuseTests.Effect");
        var effect = effectGO.AddComponent<ParticleSystem>();
        effect.Play(true);

        // 1세대: 0.05초 뒤 끊기는 타이머를 건다.
        generation[effect] = 1;
        stopAfterAsync.Invoke(manager, new object[] { effect, 0.05f, 1 });

        // 타이머가 끝나기 전에 같은 인스턴스가 새 재생(2세대)으로 넘어갔다고 가정한다
        // (조기 반환 후 풀에서 다시 나간 상황).
        generation[effect] = 2;

        yield return new WaitForSecondsRealtime(0.15f);

        Assert.IsTrue(effect.isEmitting,
            "1세대 타이머가 세대를 확인하지 않고 2세대 재생을 멈췄다 (EffectManager.StopAfterAsync).");

        generation.Remove(effect);
        UnityEngine.Object.DestroyImmediate(effectGO);
    }

    static IEnumerator Until(Func<bool> ready)
    {
        var deadline = Time.realtimeSinceStartup + 10f;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(ready(), "EffectManager 싱글턴 준비 시간이 초과됐다");
    }
}
