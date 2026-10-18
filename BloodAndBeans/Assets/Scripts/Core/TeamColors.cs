using System.Collections.Generic;
using UnityEngine;

/// 팀을 색으로 알아보게 한다. 밤 숲에서는 상대 팀과 같은 화면에 서므로
/// 누가 아군인지가 장식이 아니라 게임플레이 정보다.
///
/// 팔레트 순서는 팀 번호이고, 카메라 컬링 레이어(`TeamVision.LayerPrefix` + n)와 같은 순서다.
public static class TeamColors
{
    static readonly Color[] Palette =
    {
        new(0.85f, 0.25f, 0.25f),   // 팀 0 — 붉은색
        new(0.25f, 0.50f, 0.90f),   // 팀 1 — 푸른색
        new(0.35f, 0.75f, 0.35f),   // 팀 2 — 녹색
        new(0.90f, 0.70f, 0.20f),   // 팀 3 — 노란색
    };

    /// URP Lit·Unlit의 색 프로퍼티. 표준 셰이더의 `_Color`가 아니다.
    public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// `BloodAndBean/Style/BipedSoft`의 팀 영역 색. 흰색이면 물들지 않는다.
    public static readonly int TeamColorId = Shader.PropertyToID("_TeamColor");

    /// 팔레트를 넘는 팀에는 색을 주지 않는다. 조용히 팀 0으로 접으면 두 팀이 같은 색이 되어
    /// 구분하려고 넣은 장치가 오히려 사람을 속인다.
    static bool TryGet(int team, out Color color)
    {
        if (team < 0 || team >= Palette.Length)
        {
            color = Color.white;
            return false;
        }
        color = Palette[team];
        return true;
    }

    /// 계층 전체에 팀 색을 입힌다. 원본 머티리얼마다 팀 색 사본을 하나 만들어 같은 원본을 쓰는
    /// 렌더러끼리 공유한다. MaterialPropertyBlock을 쓰면 카페 타일 수백 개가 SRP Batcher에서
    /// 빠져 하나씩 그려진다. 만든 사본은 `owned`에 담기며, 파괴는 부른 쪽 책임이다.
    ///
    /// `strength`가 0이면 원래 색 그대로, 1이면 팀 색 그대로다. 곱이 아니라 보간인 이유는
    /// 키트 모델의 대비를 남기기 위해서다 — 곱하면 wood와 metal이 같은 색으로 뭉개진다.
    /// 투명도는 팀 색이 아니라 원래 머티리얼 것을 쓴다. 유리를 불투명하게 만들면 안 된다.
    public static void Tint(GameObject root, int team, float strength, List<Material> owned)
    {
        if (root == null || !TryGet(team, out var color)) return;

        var copies = new Dictionary<Material, Material>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || !source.HasProperty(BaseColorId)) continue;

                if (!copies.TryGetValue(source, out var copy))
                {
                    var baseColor = source.GetColor(BaseColorId);
                    var tinted = Color.Lerp(baseColor, color, strength);
                    tinted.a = baseColor.a;

                    copy = new Material(source) { name = $"{source.name} (team {team})" };
                    copy.SetColor(BaseColorId, tinted);
                    copies.Add(source, copy);
                    owned.Add(copy);
                }
                materials[i] = copy;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    /// 팀 색. 팔레트 밖이면 흰색이다. 잠깐 다른 색으로 물들였다가 되돌릴 때 목표값이 된다.
    public static Color Of(int team) => TryGet(team, out var color) ? color : Color.white;

    /// 팀과 무관한 임의의 색으로 물들인다. 피격 번쩍임처럼 한순간만 다른 색이 필요할 때
    /// 쓴다 — 되돌리는 책임은 부르는 쪽에 있다.
    public static void TintWith(GameObject root, Color color, float strength)
    {
        if (root == null) return;

        var block = new MaterialPropertyBlock();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            // 키트 모델은 색 영역마다 머티리얼이 따로다. 렌더러 하나로 뭉뚱그리면 서브메시
            // 색이 전부 첫 머티리얼 색으로 덮여 모델이 단색이 된다.
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || !material.HasProperty(BaseColorId)) continue;

                var baseColor = material.GetColor(BaseColorId);
                var tinted = Color.Lerp(baseColor, color, strength);
                tinted.a = baseColor.a;

                block.Clear();
                block.SetColor(BaseColorId, tinted);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }
}
