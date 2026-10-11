using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// 가방의 소유자 전용 추적 RPC를 받은 화면에서만 냄새와 스텐실 표식을 켠다.
public class BagScentVisuals : MonoBehaviour
{
    public const uint RenderingLayerBit = 1u << 10;
    [SerializeField] BuriedBag bag;
    [SerializeField] Renderer[] body = System.Array.Empty<Renderer>();

    Transform viewer;
    ParticleSystem scent;
    bool showing;
    int showVersion;
    float until;

    void OnEnable()
    {
        if (bag == null || body.Length == 0)
        {
            CDebug.LogError("가방 냄새 표시의 참조가 비었다.", this);
            enabled = false;
            return;
        }
        bag.TrackRevealed += Reveal;
    }

    void OnDisable()
    {
        if (bag != null) bag.TrackRevealed -= Reveal;
        showVersion++;
        showing = false;
        Clear();
    }

    void Reveal(float seconds)
    {
        if (seconds <= 0f) return;
        var local = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient : null;
        viewer = local?.PlayerObject != null ? local.PlayerObject.transform : null;
        if (viewer == null)
        {
            CDebug.LogError("추적 표시를 받을 로컬 플레이어가 없다.", this);
            return;
        }
        until = Mathf.Max(until, Time.time + seconds);
        Mark(true);
        if (scent == null) scent = EffectManager.PlayAttached(EffectId.TrackScent, transform, 0f);
        if (!showing)
        {
            showing = true;
            ShowAsync(showVersion).Forget();
        }
    }

    async UniTaskVoid ShowAsync(int version)
    {
        var ct = this.GetCancellationTokenOnDestroy();
        while (Time.time < until)
        {
            if (viewer == null) break;
            if (scent != null)
            {
                var toward = viewer.position - scent.transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > 0.0001f)
                    scent.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
            }
            // 입자 방출 방향은 플레이어가 움직인 다음 프레임마다 따라간다.
            var cancelled = await UniTask.NextFrame(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
            if (cancelled || version != showVersion) return;
        }
        showing = false;
        Clear();
    }

    void Clear()
    {
        Mark(false);
        until = 0f;
        viewer = null;
        if (scent == null) return;
        // 가방이 회수·소각되어 사라져도 풀 인스턴스를 함께 파괴하지 않는다.
        scent.transform.SetParent(EffectManager.Instance != null ? EffectManager.Instance.transform : null, true);
        scent.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        scent = null;
    }

    void Mark(bool on)
    {
        foreach (var r in body)
        {
            if (r == null) continue;
            r.renderingLayerMask = on ? r.renderingLayerMask | RenderingLayerBit : r.renderingLayerMask & ~RenderingLayerBit;
        }
    }
}
