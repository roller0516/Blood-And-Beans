using NUnit.Framework;

public class DayV5Tests
{
    [Test]
    public void BuffRenewalExpiresWithoutStackingOrAffectingOtherTeams()
    {
        var team = new TeamBuffs();
        var other = new TeamBuffs();
        Assert.IsTrue(team.Apply(TeamBuff.Move, 1));
        Assert.AreEqual(3, team.Remaining(TeamBuff.Move, 1));
        Assert.AreEqual(1, team.Remaining(TeamBuff.Move, 3));
        Assert.AreEqual(0, team.Remaining(TeamBuff.Move, 4));
        team.Apply(TeamBuff.Move, 2);
        team.Apply(TeamBuff.Move, 2);
        Assert.AreEqual(3, team.Remaining(TeamBuff.Move, 2));
        Assert.AreEqual(0, other.Remaining(TeamBuff.Move, 2));
        Assert.IsFalse(team.Apply((TeamBuff)99, 1));
        Assert.IsFalse(team.Apply(TeamBuff.Move, 0));
    }

    [Test]
    public void EmptyDishRemainsVisibleAndKeepsItsType()
    {
        var dish = CarryView.Of(HeldItem.Dish(true, true));
        Assert.IsFalse(dish.Empty);
        Assert.IsTrue(dish.HasDish);
        Assert.IsTrue(dish.DishIsPlate);
        Assert.IsTrue(dish.Dirty);
        Assert.IsFalse(dish.Equals(CarryView.Nothing));
    }

    /// 기획서 9.1.1 짝 · 9.1.3 쿨타임 · 9.1.2 삼키기 0.9초.
    [Test]
    public void DaySkillsFollowNightPairAndCooldownTable()
    {
        Assert.AreEqual(DaySkill.Ignite, DaySkills.Of(NightSkill.WillOWisp));
        Assert.AreEqual(DaySkill.Glide, DaySkills.Of(NightSkill.Echo));
        Assert.AreEqual(DaySkill.Refine, DaySkills.Of(NightSkill.Appraise));
        Assert.AreEqual(DaySkill.Shortcut, DaySkills.Of(NightSkill.Track));
        Assert.AreEqual(DaySkill.Swallow, DaySkills.Of(NightSkill.Illusion));
        Assert.AreEqual(DaySkill.None, DaySkills.Of(NightSkill.None));

        Assert.AreEqual(14f, DaySkills.CooldownOf(DaySkill.Shortcut));
        Assert.AreEqual(15f, DaySkills.CooldownOf(DaySkill.Ignite));
        Assert.AreEqual(18f, DaySkills.CooldownOf(DaySkill.Refine));
        Assert.AreEqual(22f, DaySkills.CooldownOf(DaySkill.Glide));
        Assert.AreEqual(22f, DaySkills.CooldownOf(DaySkill.Swallow));
        Assert.AreEqual(0.9f, DayBalance.WashSeconds * (1f - DaySkills.SwallowProgress), 0.0001f);

        // 9.1.1: 5종이고 스킬이 겹치지 않아야 팀 내 중복 픽 금지가 스킬 중복 금지가 된다.
        Assert.AreEqual(5, CharacterCatalog.All.Length);
        var nights = new System.Collections.Generic.HashSet<NightSkill>();
        var ids = new System.Collections.Generic.HashSet<CharacterId>();
        foreach (var c in CharacterCatalog.All)
        {
            Assert.IsTrue(nights.Add(c.Night), c.Name);
            Assert.IsTrue(ids.Add(c.Id), c.Name);
            Assert.AreEqual(DaySkills.Of(c.Night), c.Day);
            Assert.AreEqual(DaySkills.NameOf(c.Day), c.DayName);
        }
    }

}

public class DayV5RuntimeTests
{
    [UnityEngine.TestTools.UnitySetUp]
    public System.Collections.IEnumerator EnterPlay()
    {
        yield return new UnityEngine.TestTools.EnterPlayMode();
    }

    [UnityEngine.TestTools.UnityTearDown]
    public System.Collections.IEnumerator ExitPlay()
    {
        if (UnityEngine.Application.isPlaying)
        {
            if (Unity.Netcode.NetworkManager.Singleton != null) Unity.Netcode.NetworkManager.Singleton.Shutdown();
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }
    }

    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator CafeDishesBloodBeansAndGaugeAuthority()
    {
        var wait = WaitFor(() => GameManager.Instance != null && GameManager.Instance.IsReady && Unity.Netcode.NetworkManager.Singleton != null); while (wait.MoveNext()) yield return wait.Current;
        var manager = Unity.Netcode.NetworkManager.Singleton;
        try
        {
            Assert.IsTrue(GameManager.Seating.SetTeamCountCheat(2));
            Assert.IsTrue(manager.StartHost());
            wait = WaitFor(() => MatchDirector.Instance != null && MatchDirector.Instance.CafeOf(0) != null && manager.LocalClient.PlayerObject != null); while (wait.MoveNext()) yield return wait.Current;
            var director = MatchDirector.Instance;
            var player = manager.LocalClient.PlayerObject.gameObject;
            var team = player.GetComponent<PlayerTeam>().Team;
            var cafe = director.CafeOf(team);
            var carry = player.GetComponent<PlayerCarry>();
            director.Phase.EndPhaseNowServer();
            wait = WaitFor(() => director.Phase.Current == Phase.Day); while (wait.MoveNext()) yield return wait.Current;
            var racks = cafe.GetComponentsInChildren<DishRack>();
            Assert.AreEqual(2, racks.Length, "카페에 잔·접시 수령대가 있어야 한다");
            var cup = System.Array.Find(racks, r => r.name == "CupRack");
            PlayerTeleport.ToServer(player, cup.transform.position);
            var cupsBefore = cafe.Dishes.CleanCups;
            cup.TakeRpc();
            Assert.IsTrue(carry.Held.HasDish);
            Assert.IsFalse(carry.Held.DishIsPlate);
            Assert.AreEqual(cupsBefore - 1, cafe.Dishes.CleanCups);
            cup.TakeRpc();
            Assert.AreEqual(cupsBefore - 1, cafe.Dishes.CleanCups, "한 손에 식기를 중복 지급하면 안 된다");
            var blood = System.Array.Find(cafe.GetComponentsInChildren<IngredientShelf>(), s => s.SlotItem(0) == Ingredient.BloodBean);
            cafe.Stock.DepositServer(Ingredient.BloodBean);
            PlayerTeleport.ToServer(player, blood.transform.position);
            blood.TakeRpc((int)Ingredient.BloodBean);
            Assert.AreEqual(Ingredient.BloodBean, carry.Held.Ingredient, "빈 잔에 블러드 빈을 먼저 담는다");
            Assert.AreEqual(0, cafe.Stock.CountOf(Ingredient.BloodBean));
            var facilities = UnityEngine.Object.FindObjectsByType<SharedFacility>();
            foreach (var f in facilities)
                foreach (var renderer in f.GetComponentsInChildren<UnityEngine.Renderer>(true))
                    Assert.AreEqual(f.gameObject.layer, renderer.gameObject.layer, "공용 설비 모델에 팀 전용 레이어가 남으면 안 된다");
            var machine = System.Array.Find(facilities, f => f.Kind == FacilityKind.Coffee);
            Assert.IsNotNull(machine);
            PlayerTeleport.ToServer(player, machine.transform.position);
            machine.UseRpc();
            Assert.IsTrue(machine.Busy);
            Assert.IsTrue(carry.Reserved);
            wait = WaitFor(() => CompletionGauge.LocalTarget() != null); while (wait.MoveNext()) yield return wait.Current;
            CompletionGauge.LocalTarget().StopRpc();
            Assert.IsTrue(carry.Held.IsProduct);
            Assert.AreEqual(Ingredient.BloodBean, carry.Held.Recipe[0]);
            Assert.IsFalse(machine.Busy);
            Assert.IsFalse(carry.Reserved);

            wait = WaitFor(() => cafe.Queue.Waiting.Count >= 2); while (wait.MoveNext()) yield return wait.Current;
            var front = cafe.Queue.Waiting[0];
            var recipient = cafe.Queue.Waiting[1];
            front.SetupServer(team, Race.Ghost, MenuTag.Cold, MenuTag.None, 1, 1);
            recipient.SetupServer(team, Race.Werewolf, MenuTag.Hot, MenuTag.None, 1, 3);
            front.AddPatienceServer(-20);
            recipient.AddPatienceServer(-20);
            var frontBefore = front.Patience;
            var recipientBefore = recipient.Patience;
            var serving = carry.Held;
            serving.GaugeMultiplier = CompletionGauge.MultiplierOf(Judgement.Perfect);
            Assert.IsTrue(cafe.Queue.TryServeServer(serving));
            Assert.AreEqual(frontBefore, front.Patience, 0.001f, "무관한 맨 앞 손님은 회복하지 않는다");
            Assert.Greater(recipient.Patience, recipientBefore, "실제로 서빙받은 손님만 회복한다");
            cafe.Dishes.SoilServer();
            carry.ClearServer();
            var beans = System.Array.Find(facilities, f => f.Kind == FacilityKind.Beans);
            PlayerTeleport.ToServer(player, beans.transform.position);
            beans.UseRpc();
            Assert.IsTrue(carry.Empty, "광장에서는 식기를 원격 지급하지 않는다");
            var enemyRack = director.CafeOf(1 - team).GetComponentInChildren<DishRack>();
            PlayerTeleport.ToServer(player, enemyRack.transform.position);
            enemyRack.TakeRpc();
            Assert.IsTrue(carry.Empty, "다른 팀 식기를 가져갈 수 없다");

            var first = cafe.Gauges[0];
            var second = cafe.Gauges[1];
            first.BeginServer();
            yield return null;
            second.BeginServer();
            second.StopRpc();
            Assert.IsTrue(first.Active);
            Assert.IsTrue(second.Active, "점유하지 않았고 멀리 떨어진 게이지 RPC는 거부한다");
            first.StopRpc();
            Assert.IsTrue(first.Active, "점유하지 않았고 멀리 떨어진 게이지는 정지할 수 없다");
            Assert.IsTrue(second.Active);
            first.CancelServer();
            second.CancelServer();
            Assert.IsTrue(cafe.Dishes.ClaimServer());
            var input = HeldItem.Of(Ingredient.Bean); input.HasDish = true;
            carry.SetServer(input);
            PlayerTeleport.ToServer(player, machine.transform.position);
            machine.UseRpc();
            wait = WaitFor(() => CompletionGauge.LocalTarget() != null); while (wait.MoveNext()) yield return wait.Current;
            var owned = CompletionGauge.LocalTarget();
            Assert.AreEqual(manager.LocalClientId, owned.Station.OperatorId);
            var field = typeof(CompletionGauge).GetField("startedAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ((Unity.Netcode.NetworkVariable<double>)field.GetValue(owned)).Value = manager.ServerTime.Time - 11;
            var result = Judgement.Good;
            owned.OnResult += value => result = value;
            owned.StopRpc();
            Assert.IsFalse(machine.Busy);
            Assert.IsTrue(carry.Held.Burnt);
            Assert.AreEqual(Judgement.Burnt, result, "Update 전 도착한 만료 입력도 탄 판정이다");
        }
        finally { if (manager != null) manager.Shutdown(); }
        yield return new UnityEngine.TestTools.ExitPlayMode();
    }

    static System.Collections.IEnumerator WaitFor(System.Func<bool> ready)
    {
        var deadline = UnityEngine.Time.realtimeSinceStartup + 30f;
        while (!ready() && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(ready(), "호스트 또는 낮 플레이 준비 시간 초과");
    }
}