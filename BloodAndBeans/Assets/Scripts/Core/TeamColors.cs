using UnityEngine;

/// 팀을 색으로 알아보게 한다. CustomerLook이 종족을 색으로 가르는 것과 같은 이유다 —
/// 밤 숲에서는 상대 팀과 같은 화면에 서므로 누가 아군인지가 장식이 아니라 게임플레이 정보다.
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
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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

    /// 계층 전체에 팀 색을 입힌다. 머티리얼 에셋을 복제하지 않고 MaterialPropertyBlock으로
    /// 인스턴스별 색만 덮으므로, 프리팹 하나를 모든 팀이 공유하는 지금 구조에서도
    /// 팀마다 다른 색이 나온다.
    ///
    /// `strength`가 0이면 원래 색 그대로, 1이면 팀 색 그대로다. 곱이 아니라 보간인 이유는
    /// 키트 모델의 대비를 남기기 위해서다 — 곱하면 wood와 metal이 같은 색으로 뭉개진다.
    /// 투명도는 팀 색이 아니라 원래 머티리얼 것을 쓴다. 유리를 불투명하게 만들면 안 된다.
    public static void Tint(GameObject root, int team, float strength)
    {
        if (root == null || !TryGet(team, out var color)) return;

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
