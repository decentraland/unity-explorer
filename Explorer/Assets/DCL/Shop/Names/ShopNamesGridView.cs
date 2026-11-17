using System;

namespace DCL.Shop
{
    public class ShopNamesGridView : ShopGridViewBase<ShopNameCardView, ShopNameCardModel>
    {
        public event Action<ShopNameCardView>? CardClicked;
        public event Action<ShopNameCardView>? CardViewClicked;

        protected override void WireCard(ShopNameCardView card)
        {
            card.Clicked = c => CardClicked?.Invoke(c);
            card.ViewClicked = c => CardViewClicked?.Invoke(c);
        }
    }
}
