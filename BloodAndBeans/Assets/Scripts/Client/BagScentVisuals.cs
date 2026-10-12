using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// 추적을 받은 로컬 플레이어에서 가방까지 붉은 냄새 길을 그린다.
public class BagScentVisuals : MonoBehaviour
{
    [SerializeField] BuriedBag bag;
    [SerializeField] LineRenderer route;
    [SerializeField, Min(0.01f)] float routeRefreshSeconds = 0.15f;
    [SerializeField, Min(0f)] float routeHeight = 0.12f;
    [SerializeField, Min(0.01f)] float navSampleDistance = 2f;

    // 필드 초기화로 만들면 네이티브 객체가 비어 null 참조로 터진다 (네트워크 스폰 인스턴스). 처음 쓸 때 만든다.
    NavMeshPath path;
    MaterialPropertyBlock routeProperties;
    float routeLength;
    static readonly int RouteLength = Shader.PropertyToID("_RouteLength");

    Transform viewer;
    ParticleSystem scent;
    bool showing;
    int showVersion;
    float until;

    void OnEnable()
    {
        if (bag == null || route == null)
        {
            CDebug.LogError("가방 냄새 표시의 참조가 비었다.", this);
            enabled = false;
            return;
        }
        route.positionCount = 0;
        route.enabled = false;
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
        var nextRouteUpdate = 0f;
        while (Time.time < until)
        {
            if (viewer == null) break;
            if (Time.time >= nextRouteUpdate)
            {
                DrawRoute(viewer.position, transform.position);
                nextRouteUpdate = Time.time + Mathf.Max(0.01f, routeRefreshSeconds);
            }
            else FollowRouteStart(viewer.position);
            if (scent != null)
            {
                var toward = viewer.position - scent.transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > 0.0001f)
                    scent.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
            }
            // 이동이 끝난 뒤 시작점과 입자 방향을 갱신하고, 경로 탐색만 제한된 주기로 실행한다.
            var cancelled = await UniTask.NextFrame(PlayerLoopTiming.LastPreLateUpdate, ct).SuppressCancellationThrow();
            if (cancelled || version != showVersion) return;
        }
        showing = false;
        Clear();
    }

    void Clear()
    {
        if (route != null)
        {
            route.positionCount = 0;
            route.enabled = false;
        }
        until = 0f;
        viewer = null;
        if (scent == null) return;
        // 가방이 회수·소각되어 사라져도 풀 인스턴스를 함께 파괴하지 않는다.
        scent.transform.SetParent(EffectManager.Instance != null ? EffectManager.Instance.transform : null, true);
        scent.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        scent = null;
    }

    void FollowRouteStart(Vector3 from)
    {
        if (route == null || !route.enabled || route.positionCount < 2) return;
        if (!NavMesh.SamplePosition(from, out var start, navSampleDistance, NavMesh.AllAreas)) return;

        var at = start.position + Vector3.up * routeHeight;
        var previous = route.GetPosition(0);
        var next = route.GetPosition(1);
        routeLength += Vector3.Distance(at, next) - Vector3.Distance(previous, next);
        route.SetPosition(0, at);
        routeProperties.SetFloat(RouteLength, routeLength);
        route.SetPropertyBlock(routeProperties);
    }

    /// 에디터 미리보기에서도 같은 경로를 사용한다. 게임 상태나 플레이어 위치는 바꾸지 않는다.
    public void DrawRoute(Vector3 from, Vector3 to)
    {
        if (route == null) return;
        route.enabled = false;
        route.positionCount = 0;
        path ??= new NavMeshPath();
        routeProperties ??= new MaterialPropertyBlock();
        if (!NavMesh.SamplePosition(from, out var start, navSampleDistance, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(to, out var end, navSampleDistance, NavMesh.AllAreas)
            || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
            || path.status != NavMeshPathStatus.PathComplete) return;

        var corners = path.corners;
        if (corners.Length < 2) return;
        routeLength = 0f;
        for (var i = 0; i < corners.Length; i++)
        {
            if (i > 0) routeLength += Vector3.Distance(corners[i - 1], corners[i]);
        }
        for (var i = 0; i < corners.Length; i++) corners[i] += Vector3.up * routeHeight;
        route.positionCount = corners.Length;
        route.SetPositions(corners);
        routeProperties.SetFloat(RouteLength, routeLength);
        route.SetPropertyBlock(routeProperties);
        route.enabled = true;
    }
}
