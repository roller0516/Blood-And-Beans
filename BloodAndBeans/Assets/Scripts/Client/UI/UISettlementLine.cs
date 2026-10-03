using TMPro;
using UnityEngine;

/// 하루 정산 카드의 「이름 · 값」 한 줄. 이름은 프리팹 인스턴스마다 적어 두고 값만 받는다.
public sealed class UISettlementLine : MonoBehaviour
{
    [SerializeField] TMP_Text value;

    public void Render(string text, Color color)
    {
        value.text = text;
        value.color = color;
    }
}
