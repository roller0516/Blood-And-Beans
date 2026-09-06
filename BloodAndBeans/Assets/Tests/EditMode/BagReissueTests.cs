using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

/// 기획서 6.7~6.8: 실제 호스트의 묻기·재지급·정산 경로를 함께 검증한다.
public class BagReissueTests
{
    [UnitySetUp]
    public IEnumerator StartPlayMode()
    {
        yield return new EnterPlayMode();
    }

    [UnityTest]
    public IEnumerator ReturningReissuesAnEmptyBagWithoutLosingBurialAccounting()
    {
        var wait = Until(() => GameManager.Instance != null && GameManager.Instance.IsReady &&
            NetworkManager.Singleton != null);
        while (wait.MoveNext()) yield return wait.Current;
        var manager = NetworkManager.Singleton;
        Assert.IsFalse(manager.IsListening);
        Assert.IsTrue(GameManager.Seating.SetTeamCountCheat(2));
        Assert.IsTrue(manager.StartHost());
        wait = Until(() => MatchDirector.Instance != null &&
            MatchDirector.Instance.ZoneOf(0) != null && MatchDirector.Instance.ZoneOf(1) != null &&
            manager.LocalClient.PlayerObject != null);
        while (wait.MoveNext()) yield return wait.Current;

        var player = manager.LocalClient.PlayerObject.gameObject;
        var inv = player.GetComponent<PlayerInventory>();
        var director = MatchDirector.Instance;
        var phase = director.Phase;
        var team = player.GetComponent<PlayerTeam>().Team;
        var home = director.ZoneOf(team);
        var away = director.ZoneOf(1 - team).Center.Value;
        Assert.IsTrue(inv.HasBag);

        PlayerTeleport.ToServer(player, away);
        Assert.IsTrue(inv.AddServer(Ingredient.Milk, 3));
        inv.BuryRpc();
        inv.ReissueBagServer();
        Assert.IsFalse(inv.HasBag, "적 귀환 구역은 재지급하지 않는다");
        Assert.IsFalse(inv.AddServer(Ingredient.Ice));
        Assert.AreEqual(3, inv.BuriedLossCount);

        PlayerTeleport.ToServer(player, home.Center.Value);
        yield return null;
        yield return null;
        Assert.IsTrue(inv.HasBag, "Update에서 귀환을 감지해 재지급한다");
        Assert.AreEqual(0, inv.Count);
        Assert.AreEqual(0f, inv.Carried);
        Assert.AreEqual(3, inv.BuriedLossCount, "빈 가방이 이전 수확을 되살리지 않는다");

        inv.enabled = false; // 마감 경계를 같은 프레임에서 순서대로 검사한다.
        Assert.IsTrue(inv.AddServer(Ingredient.Berry, 2));
        inv.ReissueBagServer();
        Assert.AreEqual(2, inv.Count, "이미 멘 가방은 비우지 않는다");
        var bagsBefore = UnityEngine.Object.FindObjectsByType<BuriedBag>();
        inv.BuryRpc();
        inv.ReissueBagServer();
        Assert.IsFalse(inv.HasBag, "구역 안에서 묻자마자 재지급하면 원본 회수가 막힌다");
        Assert.AreEqual(5, inv.BuriedLossCount);
        var latest = Array.Find(UnityEngine.Object.FindObjectsByType<BuriedBag>(),
            bag => Array.IndexOf(bagsBefore, bag) < 0);
        Assert.IsNotNull(latest);
        typeof(BuriedBag).GetMethod("RetrieveServer", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(latest, new object[] { manager.LocalClientId });
        Assert.IsTrue(inv.HasBag);
        Assert.AreEqual(2, inv.Count);
        Assert.AreEqual(3, inv.BuriedLossCount, "회수한 가방의 수량만 기록에서 빠진다");

        PlayerTeleport.ToServer(player, away);
        inv.BuryRpc();
        PlayerTeleport.ToServer(player, home.Center.Value);
        phase.EndPhaseNowServer();
        inv.ReissueBagServer();
        Assert.IsFalse(inv.HasBag, "마감 시각 이후 재지급은 금지한다");
        wait = Until(() => phase.Current == Phase.Day);
        while (wait.MoveNext()) yield return wait.Current;
        inv.ReissueBagServer();
        Assert.IsFalse(inv.HasBag, "낮에는 지급하지 않는다");
        Assert.AreEqual(5, home.LostCount);
        Assert.AreEqual(ReturnOutcome.BagLost, home.Outcome);

        phase.SkipToNextDayServer();
        wait = Until(() => phase.Day == 2 && phase.Current == Phase.Night);
        while (wait.MoveNext()) yield return wait.Current;
        Assert.IsTrue(inv.HasBag);
        Assert.AreEqual(0, inv.BuriedLossCount);
        Assert.AreEqual(0, inv.Count);

        PlayerTeleport.ToServer(player, away);
        inv.AddServer(Ingredient.Milk, 3);
        inv.BuryRpc();
        PlayerTeleport.ToServer(player, home.Center.Value);
        inv.ReissueBagServer();
        Assert.IsTrue(inv.HasBag);
        inv.AddServer(Ingredient.Ice, 2);
        phase.EndPhaseNowServer();
        wait = Until(() => phase.Current == Phase.Day);
        while (wait.MoveNext()) yield return wait.Current;
        Assert.AreEqual(ReturnOutcome.Returned, home.Outcome);
        Assert.AreEqual(2, home.KeptCount);
        Assert.AreEqual(3, home.LostCount, "새 가방 귀환에도 미회수 원본의 손실을 표시한다");
    }

    [UnityTearDown]
    public IEnumerator StopHost()
    {
        if (Application.isPlaying)
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return new ExitPlayMode();
        }
    }

    static IEnumerator Until(Func<bool> ready)
    {
        var deadline = Time.realtimeSinceStartup + 30f;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(ready(), "호스트/페이즈 준비 시간이 초과됐다");
    }
}
