using UnityEngine;

namespace BrokenSovereign.Town
{
    /// <summary>NPC 상점 종류 지정 (선택). 없으면 NPC 이름(Merchant·Blacksmith·Healer)으로 정함 (ShopCatalog.TypeOf)</summary>
    public class NpcShop : MonoBehaviour
    {
        [SerializeField] private ShopType _type = ShopType.Goods;

        public ShopType Type => _type;
    }
}
