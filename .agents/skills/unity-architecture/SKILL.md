---
name: unity-architecture
description: Blood & Beans의 코드 배치 규칙. 새 클래스를 어느 어셈블리·어느 폴더에 둘지, 서버 권위를 어디에 둘지, 싱글턴·RPC·NetworkVariable을 언제 쓸지 판단할 때 사용한다. C# 파일을 새로 만들거나, 클래스를 옮기거나, 게임 규칙·네트워크 동기화·UI 표현이 얽힌 코드를 손댈 때 먼저 읽는다.
allowed-tools:
  - Bash
  - Read
  - Grep
  - Glob
---

# Blood & Beans 아키텍처

이 문서는 **관행이 아니라 이 저장소에서 실제로 강제되고 있는 구조**다. 어셈블리 경계는
`.asmdef`가 컴파일러로 막고 있어서, 어기면 빌드가 깨진다.

저장소 전체 규칙은 루트 `CLAUDE.md`에 있고, 충돌하면 `CLAUDE.md`가 우선한다.

## 1. 세 어셈블리 — 의존은 한 방향뿐이다

```
BB.Client  ──▶  BB.Game  ──▶  BB.Rules
(표현·입력·UI)   (권위·복제)     (순수 규칙)
```

| 어셈블리 | 위치 | 참조하는 것 | 참조 못 하는 것 |
|---|---|---|---|
| `BB.Rules` | `Scripts/Rules/` | **아무것도 없다.** `noEngineReferences: true` | `UnityEngine` 전체, Netcode, 나머지 전부 |
| `BB.Game` | `Scripts/` (Client 제외) | `BB.Rules`, Netcode, UniTask | `BB.Client` |
| `BB.Client` | `Scripts/Client/` | `BB.Game`, `BB.Rules`, Netcode, Input System, Cinemachine, TMP | — |

**역방향 참조는 존재하지 않는다.** `BB.Game`은 어떤 UI도 알지 못하고, `BB.Rules`는
`Vector3`조차 쓸 수 없다. 이것이 `CLAUDE.md`의 DIP 항목이 실제로 구현된 형태다.

### BB.Rules가 엔진을 못 쓴다는 것의 의미

`Mathf.Min`을 쓸 수 없다. `System.Math.Min`을 쓴다. 실제 코드에 그 이유가 주석으로 남아 있다:

```csharp
// Rules/Rent.cs
/// Mathf 대신 System.Math를 쓰는 이유는 BB.Rules가 UnityEngine을 참조하지 않기 때문이다.
public static int Due(int day) =>
    Table[System.Math.Min(System.Math.Max(day, 1), Table.Length) - 1];
```

불편해 보이면 그것이 목적이다. **엔진을 못 쓰기 때문에 규칙이 EditMode 테스트에서 씬 없이
그대로 돈다.** `BB.Rules`에 `using UnityEngine`을 추가하고 싶어지면, 그 코드는 `BB.Rules`에
있을 코드가 아니다.

## 2. 새 클래스를 어디에 둘 것인가

위에서부터 답이 "예"인 첫 줄에서 멈춘다.

| 질문 | 답이 예이면 | 폴더 |
|---|---|---|
| 씬·프리팹·`GameObject` 없이 값만 계산하는가? | `BB.Rules` | `Rules/` |
| 한 판의 진행·좌석·부팅 순서를 정하는가? | `BB.Game` | `Core/` |
| 전송 계층·로비·세션인가? | `BB.Game` | `Net/` |
| 밤 페이즈에서만 존재하는 상호작용인가? | `BB.Game` | `Night/` |
| 낮 페이즈 카페 설비·손님인가? | `BB.Game` | `Day/` |
| 판매·정산·점수인가? | `BB.Game` | `Economy/` |
| 플레이어 이동·소유·상호작용 주체인가? | `BB.Game` | `Player/` |
| uGUI 계층을 소유하는가? | `BB.Client` | `Client/UI/` |
| 카메라·연출·입력 라우팅·셰이더 제어인가? | `BB.Client` | `Client/` |

**어느 줄에도 안 걸리면 새 폴더를 만들지 말고 물어본다.** 폴더가 늘어나는 것은 계층이
하나 더 생겼다는 뜻이고, 그 판단은 사람이 한다.

## 3. 서버 권위 — `Server` 접미사가 계약이다

서버에서만 돌아야 하는 메서드는 **이름이 `Server`로 끝난다.** 저장소에 70여 종이 있다.

```csharp
public void EndPhaseNowServer()                           // GamePhase
public void ApplyTeamVisibilityServer(ulong id, int team) // MatchDirector
public void ShowToTeamServer(NetworkObject o, int team)   // MatchDirector
```

접미사는 장식이 아니라 **호출부에서 권한 실수를 눈으로 잡는 장치**다. 규칙 셋:

1. **`Server`로 끝나는 메서드는 서버에서만 부른다.** 대부분 `IsServer`로 가드하지만,
   서버 경로에서만 도달하는 한 줄짜리 대입은 가드 없이 두기도 한다 (저장소에 3건).
   **가드를 생략하려면 도달 경로가 서버 하나뿐임이 코드로 보여야 한다.** 애매하면 가드한다.
2. **`Server`로 안 끝나는 public 메서드는 서버 상태를 바꾸지 않는다.** 읽기이거나 표현이다.
3. **클라이언트 코드에서 `Server` 메서드를 직접 부르지 않는다.** RPC를 거친다.

## 4. 복제 — RPC냐 NetworkVariable이냐

**NGO 2.x 통합 `[Rpc]` 속성만 쓴다.** 레거시 `[ServerRpc]`·`[ClientRpc]`는 이 저장소에
한 건도 없다. 새로 만들지 않는다.

저장소에서 실제로 쓰는 다섯 형태가 전부다:

```csharp
[Rpc(SendTo.Server)]                                              // 16건 — 클라 → 서버 요청
[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)] //  4건 — 소유자만
[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]        // 5건
[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]  // 1건
[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)] // 5건 — 팀 격리
```

### 발신자 검증은 속성이 아니라 본문에서 한다

**`[Rpc(SendTo.Server)]`에는 기본 권한 제한이 없다 — 어떤 클라이언트든 부를 수 있다.**
저장소의 16건이 의도적으로 그렇게 두고, **RPC 본문 첫 줄에서 발신자를 검증한다.**

```csharp
/// `[Rpc(SendTo.Server)]`는 어떤 클라이언트든 호출할 수 있으므로, 팀 검사는 호출부
/// Update뿐 아니라 반드시 여기에도 있어야 한다.
[Rpc(SendTo.Server)]
public void StopRpc(RpcParams p = default)
{
    if (!IsDay || !active.Value || TeamId < 0) return;
    if (PlayerTeam.Of(p.Receive.SenderClientId) != TeamId) return;   // ← 이 줄이 핵심
    Finish(Judge(Needle));
}
```

**서버로 가는 RPC를 새로 만들 때 둘 중 하나를 반드시 한다:**

- `InvokePermission = RpcInvokePermission.Owner`로 소유자만 부르게 막거나,
- `RpcParams p = default`를 받아 **`p.Receive.SenderClientId`로 팀·소유·상태를 검증**한다.

둘 다 없는 `SendTo.Server` RPC는 **아무 클라이언트나 남의 팀 상태를 조작할 수 있는
구멍이다.** 호출부(`Update` 등)의 검사는 클라이언트에 있으므로 신뢰 근거가 되지 않는다.

### 어느 쪽을 고르는가

| 성격 | 수단 | 예 |
|---|---|---|
| 늦게 들어온 클라이언트도 현재 값을 알아야 한다 | `NetworkVariable` | 페이즈, 일차, 종료 시각 |
| 한 번 일어난 사건이다 | `[Rpc]` | 상자 열기, 판매 확정 |
| 매 프레임 변한다 | NetworkTransform / 예측 | 플레이어 위치 |
| 팀에게만 보여야 한다 | `SendTo.SpecifiedInParams` + 가시성 | 팀 재고, 안개 |

`GamePhase`가 표준 사례다 — `phase`·`day`·`endsAt`·`finished` 넷을 `NetworkVariable`로
두고, 남은 시간은 **서버 시각에서 계산**한다. 타이머를 복제하지 않는다.

## 5. 싱글턴 — 셋으로 닫혀 있다

`Core/Singletons/`에 `Singleton`·`MonoSingleton`·`PersistentMonoSingleton` 세 기반이 있고,
**실제 사용처는 정확히 셋뿐이다.**

| 클래스 | 기반 | 수명 | 왜 싱글턴인가 |
|---|---|---|---|
| `GameManager` | `PersistentMonoSingleton` | 앱 전체 | 부팅 순서의 주인. 씬을 넘어 산다 |
| `MatchDirector` | `MonoSingleton` | 한 판 | 판의 권위. 씬과 함께 죽는다 |
| `UIManager` | `MonoSingleton` | 씬 | 화면 스택의 주인 |

**넷째를 만들지 않는다.** `CLAUDE.md`가 새 싱글턴을 금지한다. 위 셋은 "전역이라 편해서"가
아니라 **수명이 씬보다 길거나 판 전체에 하나뿐인 것이 물리적으로 참인** 경우다.

### GameManager의 자가 부팅

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void CreateIfMissing()
```

`Assets/Resources/`의 프리팹을 스스로 붙인다. **어느 씬에서 재생을 눌러도 성립한다.**
에디터에서 전투 씬만 열고 재생해도 접속과 좌석 배정이 돈다. 이 장치를 우회하는 코드
(`playModeStartScene` 의존 등)를 넣지 않는다 — 주석에 그 사고 이력이 적혀 있다.

### MatchDirector의 Bind/Unbind

```csharp
public static void Bind(System.Action<MatchDirector> onReady)
public static void Unbind(System.Action<MatchDirector> onReady)
```

`Awake` 순서에 기대지 않고 준비된 뒤 콜백을 받는다. **씬 오브젝트가 `MatchDirector`를
`Awake`에서 직접 찾지 않는다.** 짝을 반드시 맞춘다 — `OnEnable`/`OnDisable`.

## 6. 규칙 클래스의 모양

`BB.Rules`의 클래스는 두 부분으로 나뉜다.

```csharp
public class Rent
{
    // ponytail: 기획서 3.2의 하드코딩 표. 일차가 데이터가 되면 DT_Rent로 옮긴다.
    static readonly int[] Table = { 60, 100, 160, 250, 380, 560, 800 };

    public static int Due(int day) => ...   // 순수 함수 — 상태 없음
    public int Debt { get; private set; }   // 팀당 인스턴스 상태
}
```

- **static 부분**은 기획서의 표다. 순수 함수로 노출한다.
- **instance 부분**은 팀당 하나씩 서버가 들고 있는 상태다.
- **모든 수치에 기획서 절 번호를 주석으로 단다** (`기획서 3.2`). 근거 없는 수치는 두지 않는다.
- 아직 데이터로 못 뺀 표는 `ponytail:` 주석으로 천장과 이관처를 남긴다.

## 7. 값을 어디에 둘 것인가

`CLAUDE.md` 「절대 원칙」이 하드코딩을 금지한다. **금지의 뜻은 "상수를 쓰지 말라"가 아니라
"값의 출처와 변경 주체가 코드에서 읽혀야 한다"이다.** 이 저장소는 세 자리를 쓴다.

| 값의 성격 | 자리 | 누가 바꾸나 | 실제 예 |
|---|---|---|---|
| 기획서에 확정된 규칙 수치 | `BB.Rules`의 `static readonly` 표 | 기획서가 바뀔 때 프로그래머 | `Rent.Table`, `LoadBands.Speed`, `MenuDef.All` |
| 연출·타이밍·감각값 | `[SerializeField]` (342개) | 에디터에서 기획·아트가 직접 | `cookSeconds`, `spawnSeconds`, 카메라·색 |
| 여러 자리가 공유하는 표현 설정 | ScriptableObject | 에디터에서 한 번, 전부 공유 | `UIThemeConfig`, `ItemVisualConfig` (**SO는 이 둘뿐이다**) |

### 판단 순서

1. **기획서에 절 번호가 있는 수치인가?** → `BB.Rules`의 `static readonly` 표.
   주석에 절 번호를 단다: `// 기획서 3.2`
2. **에디터에서 만져 보며 정할 값인가?** → `[SerializeField]`. 프리팹이 값을 갖는다.
3. **여러 화면이 같은 값을 공유하는가?** → ScriptableObject.
4. **위 어디에도 안 걸리고 그냥 박게 되는가?** → 아래 「미결정 값」으로 간다.

**게임 수치용 ScriptableObject를 새로 만들지 않는다.** 현재 SO는 표현 설정 둘(`UIThemeConfig`·`ItemVisualConfig`)뿐이고,
수치는 `BB.Rules`의 표와 `[SerializeField]`가 나눠 갖는다. SO를 새로 세우려면 "왜 이 둘로는
안 되는지"를 먼저 설명한다 — 10절과 같은 승인 사항이다.

### 미결정 값 — `ponytail:` 주석이 계약이다

기획서가 아직 정하지 않은 수치는 **박되, 박았다는 사실을 남긴다.**

```csharp
void Reset() => cookSeconds = 4f;   // ponytail: 임시값. 기획서 14장에 시간 표가 없다
static readonly int[] Table = { 60, 100, 160, 250, 380, 560, 800 };
// ponytail: 기획서 3.2의 하드코딩 표. 일차가 데이터가 되면 DT_Rent로 옮긴다.
```

주석에 **반드시 둘을 적는다:**

- **왜 지금 박았는가** — "기획서 14장에 시간 표가 없다", "목업에서만 온 값이다"
- **언제 어디로 옮기는가** — "일차가 데이터가 되면 `DT_Rent`로", "아트 파이프라인이 생기면"

저장소에 36건이 있고, `ponytail-debt` 스킬로 한 번에 모아 볼 수 있다. **근거 없이 박힌 값은
이 주석이 없어서 영영 안 보인다** — 그것이 「절대 원칙」이 막으려는 것이다.

### 하지 말 것

- 같은 수치를 두 곳에 적기. 표가 진실의 원천이면 `[SerializeField]`로 중복하지 않는다.
- `0`·`1`·빈 컬렉션처럼 알고리즘 불변값까지 설정으로 빼기. 「절대 원칙」이 명시적으로 제외한다.
- 씬·태그·레이어·Input Action 이름을 문자열로 박기. 상수·해시·에셋 참조를 쓴다.
- `ponytail:` 없이 임시값 박기. 임시라는 표시가 없으면 그것은 임시가 아니라 그냥 하드코딩이다.

## 8. 클라이언트 계층

`Client/`는 **읽기만 한다.** 게임 상태를 바꾸려면 RPC를 거친다.

- **프레젠터** (`MatchHudPresenter`) — 복제 상태를 읽어 문자열·값을 만든다. `UI` 접두사를
  붙이지 않는다. uGUI 계층을 소유하지 않기 때문이다.
- **화면·팝업** (`UIMatchHudScreen`, `UIBoxLootPopup`) — `UI` 접두사 필수. `UIScreen`·
  `UIPopup`·`UIView`를 상속한다.
- **파츠** (`UIBoxLootSlot`, `UICharacterCard`) — 반복되는 조각. 자기 배선은 자기가 가진다.
- **연출** (`PlayerDissolve`, `DashVisuals`, `FogRenderer`) — 상태를 보고 그린다. 판단하지 않는다.

상세는 `CLAUDE.md`의 「명명 규칙」과 「에셋과 프로젝트 파일」에 있다.

## 9. 손대기 전에 확인하는 것

```bash
# 어셈블리 경계를 넘는지
cat BloodAndBeans/Assets/Scripts/*/BB.*.asmdef

# 이 메서드를 누가 부르는지 (권한 실수 확인)
grep -rn "MethodNameServer" BloodAndBeans/Assets/Scripts
```

씬·프리팹이 얽히면 `unity-cli` 스킬로 에디터에 물어본다. YAML을 직접 열지 않는다.

## 10. 이 구조를 바꾸려 할 때

어셈블리를 추가하거나, 참조 방향을 늘리거나, 싱글턴을 하나 더 만들거나, 폴더 계층을
늘리는 변경은 **사용자 승인 사항이다.** 컴파일이 통과한다는 것은 승인이 아니다.
왜 기존 경계로는 안 되는지를 먼저 설명한다.
