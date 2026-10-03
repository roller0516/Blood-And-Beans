
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

/// DF-01·DF-02: 판매 사건을 말풍선 파열과 매출 흡수로 그린다.
public sealed class UISaleFeedback : MonoBehaviour
{
    [SerializeField] RectTransform flightLayer;
    [SerializeField] RectTransform revenueTarget;
    [SerializeField] UnityEngine.UI.Image bubblePrefab;
    [SerializeField] UnityEngine.UI.Image coinPrefab;
    [SerializeField] UnityEngine.UI.Image sparklePrefab;
    [SerializeField] UnityEngine.UI.Image shardPrefab;
    [SerializeField] Vector3 orderOffset = new(0f, 3.63f, 0f);
    [SerializeField] Vector2 bubbleSize = new(150f, 130f);
    [SerializeField] Color gold = new(1f, .79f, .24f, 1f);
    [SerializeField] int goodCoins = 7;
    [SerializeField] int perfectCoins = 12;
    [SerializeField] int missCoins = 5;
    [SerializeField] int burntCoins = 2;
    [SerializeField] float coinSize = 28f;
    [SerializeField] float perfectCoinScale = 1.3f;
    [SerializeField] float burstSeconds = .24f;
    [SerializeField] float popSeconds = .19f;
    [SerializeField] float scatterHoldSeconds = .1f;
    [SerializeField] float flightSeconds = .48f;
    [SerializeField] float coinStagger = .045f;
    [SerializeField] Vector2 scatter = new(145f, 115f);
    [SerializeField] Vector2 flightArc = new(-100f, 90f);
    [SerializeField] float revenuePunch = .1f;
    [SerializeField] float revenuePunchSeconds = .2f;
    readonly HashSet<Tween> playing = new();
    Tween revenueTween;
    Vector3 revenueScale;
    Camera worldCamera;
    Canvas canvas;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        worldCamera = Camera.main;
        if (revenueTarget != null) revenueScale = revenueTarget.localScale;
    }
    void OnEnable() => SaleRegister.SaleShown += OnSale;
    void OnDisable()
    {
        SaleRegister.SaleShown -= OnSale;
        foreach (var tween in new List<Tween>(playing)) tween.Kill();
        playing.Clear();
        revenueTween?.Kill();
        if (revenueTarget != null) revenueTarget.localScale = revenueScale;
    }

    void OnSale(ulong customerId, Vector3 position, int amount, Gauge gauge, bool complete, int team)
    {
        var director = MatchDirector.Instance;
        if (amount <= 0 || team != PlayerTeam.Local() || director == null || director.Phase.Current != Phase.Day) return;
        var cafe = director.CafeOf(team);
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (cafe == null || cafe.Floor == null || player == null ||
            !UICustomerOrder.InsideFloor(cafe.Floor.bounds, player.transform.position)) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        var screen = worldCamera.WorldToScreenPoint(position + orderOffset);
        var bubbleDepth = screen.z;
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) return;
        var size = bubbleSize;
        var manager = NetworkManager.Singleton;
        if (manager.SpawnManager.SpawnedObjects.TryGetValue(customerId, out var customer))
        {
            var order = customer.GetComponentInChildren<UICustomerOrder>(true);
            if (order != null && order.TrySaleAnchor(complete, out var anchor, out var pixels))
            {
                screen = anchor;
                size = new Vector2(pixels.x / flightLayer.lossyScale.x, pixels.y / flightLayer.lossyScale.y);
            }
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(flightLayer, screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var origin)) return;
        Play(origin, gauge, complete, size);
        if (complete && gauge == Gauge.Perfect)
            EffectManager.Play(EffectId.SalePerfect, worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, bubbleDepth)));
    }

    public void Play(Vector2 origin, Gauge gauge, bool complete, Vector2 size)
    {
        if (revenueTarget == null || !revenueTarget.gameObject.activeInHierarchy) return;
        if (complete) Burst(origin, size);
        var count = Mathf.Max(1, gauge == Gauge.Perfect ? perfectCoins :
            gauge == Gauge.Burnt ? burntCoins : gauge == Gauge.Miss ? missCoins : goodCoins);
        var remaining = count;
        for (var i = 0; i < count; i++) Fly(origin, gauge == Gauge.Perfect, () => { if (--remaining == 0) PunchRevenue(); });
    }

    UnityEngine.UI.Image Spawn(UnityEngine.UI.Image prefab, Vector2 point)
    {
        var image = Instantiate(prefab, flightLayer);
        image.gameObject.SetActive(true);
        image.rectTransform.anchoredPosition = point;
        image.rectTransform.localScale = Vector3.one;
        return image;
    }

    void Burst(Vector2 origin, Vector2 size)
    {
        var flash = Spawn(bubblePrefab, origin);
        flash.rectTransform.sizeDelta = size;
        flash.color = gold;
        Track(DOTween.Sequence()
            .Append(flash.rectTransform.DOScale(1.12f, burstSeconds).SetEase(Ease.OutCubic))
            .Join(flash.DOFade(0f, burstSeconds).SetEase(Ease.InQuad)), flash);
        var count = Mathf.Max(1, goodCoins);
        for (var i = 0; i < count; i++)
        {
            var angle = (i + .5f) / count * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var shard = Spawn(shardPrefab, origin + Vector2.Scale(direction, size * .28f));
            shard.color = gold;
            Track(DOTween.Sequence()
                .Append(shard.rectTransform.DOAnchorPos(origin + Vector2.Scale(direction, size * .7f), burstSeconds * 1.5f).SetEase(Ease.OutCubic))
                .Join(shard.rectTransform.DORotate(new Vector3(0, 0, angle * Mathf.Rad2Deg), burstSeconds * 1.5f))
                .Join(shard.DOFade(0f, burstSeconds * 1.5f)), shard);
            var star = Spawn(sparklePrefab, origin + Vector2.Scale(direction, size * .45f));
            star.color = gold;
            star.rectTransform.localScale = Vector3.zero;
            Track(DOTween.Sequence()
                .Append(star.rectTransform.DOScale(1f, burstSeconds * .4f).SetEase(Ease.OutBack))
                .Append(star.rectTransform.DOScale(0f, burstSeconds * 1.1f).SetEase(Ease.InCubic)), star);
        }
    }

    Vector2 Destination() => (Vector2)flightLayer.InverseTransformPoint(revenueTarget.TransformPoint(revenueTarget.rect.center));

    void Fly(Vector2 origin, bool perfect, System.Action arrived)
    {
        var coin = Spawn(coinPrefab, origin);
        var rect = coin.rectTransform;
        rect.sizeDelta = Vector2.one * coinSize * (perfect ? perfectCoinScale : 1f);
        rect.localScale = Vector3.one * .25f;
        var pop = origin + Vector2.Scale(Random.insideUnitCircle, scatter);
        var control = (pop + Destination()) * .5f + flightArc + Vector2.Scale(Random.insideUnitCircle, scatter * .5f);
        var move = DOVirtual.Float(0f, 1f, flightSeconds, t =>
        {
            var end = Destination();
            rect.anchoredPosition = (1f - t) * (1f - t) * pop + 2f * (1f - t) * t * control + t * t * end;
        }).SetEase(Ease.InQuad);
        var sequence = DOTween.Sequence()
            .SetDelay(Random.Range(0f, coinStagger))
            .Append(rect.DOAnchorPos(pop, popSeconds).SetEase(Ease.OutCubic))
            .Join(rect.DOScale(1f, popSeconds).SetEase(Ease.OutBack))
            .AppendInterval(scatterHoldSeconds)
            .Append(move)
            .Join(rect.DOScale(.18f, flightSeconds).SetEase(Ease.InCubic))
            .Join(rect.DORotate(new Vector3(0, 0, perfect ? 180f : -120f), flightSeconds, RotateMode.FastBeyond360))
            .AppendCallback(() => arrived());
        Track(sequence, coin);
    }

    void PunchRevenue()
    {
        revenueTween?.Kill();
        revenueTarget.localScale = revenueScale;
        revenueTween = revenueTarget.DOPunchScale(Vector3.one * revenuePunch, revenuePunchSeconds, 1, .5f)
            .SetUpdate(true).SetLink(gameObject);
    }

    void Track(Sequence sequence, UnityEngine.UI.Image image)
    {
        playing.Add(sequence);
        sequence.SetTarget(image).SetUpdate(true).SetLink(gameObject).OnKill(() =>
        {
            playing.Remove(sequence);
            if (image != null) Destroy(image.gameObject);
        });
    }
}


