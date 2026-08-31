using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// 계산대. Day/가 서빙을 알리면 여기서 금액으로 바꿔 장부에 올린다.
/// Day/가 Economy/의 존재를 알 필요가 없도록 별도 파일로 둔다 (스크립트 구조.md 참고).
public class SaleRegister : NetworkBehaviour
{
    // 팀은 이 계산대가 속한 카페에서 가져온다. 그래야 둘이 어긋날 수 없다

    static readonly Ingredient[] NoPopular = new Ingredient[0];

    Scoreboard board;
    CustomerQueue queue;
    int team;

    public Ingredient[] Popular { get; set; } = NoPopular;
    public int LastSale { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // 전역에서 하나만 집으면 안 된다. 매출판이 카페마다 있던 시절 이 조회가 아무
        // 카페의 것이나 잡아서, 모든 팀의 판매가 한 팀 장부에만 쌓였다.
        var cafe = Cafe.Of(this);
        board = cafe != null ? cafe.Board : null;
        team = cafe != null ? cafe.TeamId : 0;
        queue = cafe != null ? cafe.Queue : null;
        if (queue != null) queue.Served += Book;
    }

    public override void OnNetworkDespawn()
    {
        if (queue != null) queue.Served -= Book;
    }

    void Book(ServeInfo info)
    {
        if (!IsServer) return;

        var recipe = info.Recipe ?? NoPopular;

        // 빵 베이스로 만든 것은 전부 디저트이고, 원두 등급을 완전히 무시한다 (5.6.2).
        var isDessert = recipe.Contains(Ingredient.BreadBase);
        var grade = recipe.Contains(Ingredient.BloodBean) ? BeanGrade.Blood : BeanGrade.Normal;

        var price = SalePrice.Calculate(
            info.BasePrice, GaugeOf(info), grade, isDessert, recipe, Popular);

        // 종족 가중치는 손님 고유의 배율이고 (5.5), 5.6.2 공식의 일부가 아니다.
        LastSale = Mathf.RoundToInt(price * Mathf.Max(0f, info.RacePriceWeight));
        board?.AddSale(team, LastSale);
    }

    /// ServeInfo는 배율을 들고 오지만 SalePrice가 원하는 것은 그 배율을 만든 판정이다.
    static Gauge GaugeOf(ServeInfo info) =>
        info.Burnt ? Gauge.Burnt :
        info.GaugeMultiplier >= 1.3f ? Gauge.Perfect :
        info.GaugeMultiplier >= 1.0f ? Gauge.Good :
                                       Gauge.Miss;
}
