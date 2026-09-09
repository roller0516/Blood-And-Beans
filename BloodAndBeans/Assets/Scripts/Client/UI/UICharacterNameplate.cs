using TMPro;
using UnityEngine;

/// 무대 위 캐릭터 한 명의 이름표. 트리는 `UICharacterNameplate.prefab`에 있고
/// <see cref="UICharacterSelectScreen"/>이 자리 수만큼 이어 둔다.
///
/// 이름표는 자기가 몇 번째 자리인지 모른다. 화면이 자리를 잡아 주고 값을 넘긴다.
public sealed class UICharacterNameplate : MonoBehaviour
{
    [SerializeField] RectTransform root;
    [SerializeField] TMP_Text nameLabel;

    [Tooltip("준비 표시. 준비를 누른 사람에게만 뜬다 (기획서 10.1). 방장은 준비 개념이 없어 부르는 쪽이 걸러 준다.")]
    [SerializeField] GameObject readyMark;

    /// 내 이름표. 참조 이미지의 「YOU」 자리다.
    [SerializeField] TMP_Text selfCaption;

    /// 팀은 **이름 글자 색**으로 알린다. 점을 따로 두면 준비 점과 모양이 같아 무엇이
    /// 무엇인지 갈리지 않는다 — 준비는 글자, 팀은 색으로 축을 나눈다.
    public void Render(string playerName, Color teamColor, bool ready, bool isSelf)
    {
        if (nameLabel != null)
        {
            nameLabel.text = playerName ?? "—";
            nameLabel.color = teamColor;
        }
        if (readyMark != null) readyMark.SetActive(ready);
        if (selfCaption != null) selfCaption.gameObject.SetActive(isSelf);
    }

    /// 화면 좌표를 캔버스 좌표로 옮긴 값. 무대 카메라가 준 자리를 화면이 넘긴다.
    public void PlaceAt(Vector2 canvasPoint)
    {
        if (root != null) root.anchoredPosition = canvasPoint;
    }

    public void SetVisible(bool value)
    {
        if (gameObject.activeSelf != value) gameObject.SetActive(value);
    }
}
