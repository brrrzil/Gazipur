using UnityEngine;
using Zenject;

public class TraderObject : InteractObject
{
    [Inject] MarketManager _market;
    public override void Intearct()
    {
        _market.StartSellTrade();
    }
}
