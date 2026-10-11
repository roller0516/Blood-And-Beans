using UnityEditor.AddressableAssets.Settings;

/// 빌더들이 어드레서블 엔트리를 넣는 그룹 이름. 그룹 창에서 이름을 바꾸면 여기도 바꾼다.
static class AddressableGroups
{
    public const string Items = "Items";
    public const string Characters = "Characters";
    public const string Vfx = "VFX";

    // 주소 접두사. 그룹 이름과 달리 단수다.
    public const string ItemAddress = "Item/";
    public const string CharacterAddress = "Character/";
    public const string VfxAddress = "VFX/";

    /// 그룹이 없으면 기본 그룹으로 떨어진다 — 등록이 통째로 빠지면 런타임 로드가 "unknown key"로 실패한다.
    public static AddressableAssetGroup Find(AddressableAssetSettings settings, string name) =>
        settings.FindGroup(name) ?? settings.DefaultGroup;
}
