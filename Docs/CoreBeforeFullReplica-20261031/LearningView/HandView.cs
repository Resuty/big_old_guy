using System.Linq;
using UnityEngine;
using System.Collections.Generic;
namespace CardDuel.Learning
{
    public sealed class HandView : MonoBehaviour
    {
        private static readonly string[] SuitNames =
            { "Spades", "Hearts", "Clubs", "Diamonds" };
        private readonly HashSet<int> selectedIds = new HashSet<int>();

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private CardVisual cardPrefab;
        [SerializeField] private Sprite smallJoker;
        [SerializeField] private Sprite bigJoker;

        private void Start()
        {
            Render();
        }

        public void Render()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            GameState state = bootstrap.Engine.State;
            selectedIds.IntersectWith(state.Hand(0));
            foreach (int id in state.Hand(0)
                .OrderBy(id => state.Cards[id].Rank)
                .ThenBy(id => state.Cards[id].Suit))
            {
                CardInstance card = state.Cards[id];
                Sprite sprite = FindSprite(card);

                if (sprite == null)
                {
                    Debug.LogError("找不到牌面：" + card.Face, this);
                    continue;
                }

                CardVisual view = Instantiate(cardPrefab, transform);
                view.CardId = id;

                string badge = card.Effect == EffectId.None
                    ? ""
                    : EffectCatalog.Badge(card.Effect);

                view.Bind(sprite, badge, Color.white, "",
                    selectedIds.Contains(id), true,
                    () => ToggleSelection(id, view), null, null);
            }
        }

        private Sprite FindSprite(CardInstance card)
        {
            if (card.Rank == 16)
                return smallJoker;

            if (card.Rank == 17)
                return bigJoker;

            string path = "Art/Cards/card"
                + SuitNames[card.Suit]
                + CardInstance.RankName(card.Rank);

            return Resources.Load<Sprite>(path);
        }

        private void ToggleSelection(int id, CardVisual view)
        {
            if (selectedIds.Contains(id))
                selectedIds.Remove(id);
            else
                selectedIds.Add(id);

            view.selectionOutline.enabled = selectedIds.Contains(id);
        }
    }
}