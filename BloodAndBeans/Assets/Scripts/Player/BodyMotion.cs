/// 캐릭터 몸짓 (18장). 사건을 확정한 쪽이 보내고, 표현 쪽(`CharacterModel`)이 클립을 고른다.
public enum BodyMotion : byte
{
    None,
    HoldStart,   // F 홀드 진입 — 세척·개봉·소각·회수 (DA-05)
    HoldEnd,     // F 홀드 이탈 — 완료·취소
    Collect,     // 묻힌 가방 회수 완료 (NA-06)
    Bury,        // 가방 묻기 (NA-06)
    Dump,        // 가방 뒤집어 쏟기 (DA-15)
    Denied,      // F가 먹지 않음 (DA-08). 서 있으면 전신, 움직이면 상체
    Perfect,     // 게이지 Perfect (DA-12)
    Miss,        // 게이지 Miss·탐 (DA-14)
    HandOver,    // 팀원에게 건넴 (DA-11)
    Receive,     // 팀원에게서 받음 (DA-11)
    Collapse,    // 주저앉음 — 무게 구간 변화·대시 불가·물약 (NA-10)
    Return,      // 귀환 (NA-11)
    Throw,       // 던지는 스킬 시전 (DA-37)
    Cast,        // 제자리 스킬 시전 (DA-38)
}
