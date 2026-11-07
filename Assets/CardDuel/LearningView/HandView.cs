using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CardDuel.Learning
{
    public sealed class HandView : MonoBehaviour
    {
        private static readonly string[] SuitNames =
            { "Spades", "Hearts", "Clubs", "Diamonds" };

        private readonly HashSet<int> selectedIds = new HashSet<int>();
        private readonly List<CardVisual> views = new List<CardVisual>();
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private CardVisual cardPrefab;
        [SerializeField] private Sprite smallJoker;
        [SerializeField] private Sprite bigJoker;

        private RectTransform content;
        public Func<EffectId, string> BadgeText { get; set; }
        public event Action CardClicked;
        public event Action SelectionChanged;
        public event Action<CardInstance> DetailsShown;
        public event Action DetailsHidden;
        public int[] SelectedIds => selectedIds.ToArray();

        private void Awake()
        {
            var oldLayout = GetComponent<HorizontalLayoutGroup>();
            if (oldLayout != null)
                oldLayout.enabled = false;

            var viewport = (RectTransform)transform;
            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject("ScrollableCards", typeof(RectTransform));
            content = (RectTransform)contentObject.transform;
            content.SetParent(transform, false);
            content.anchorMin = content.anchorMax = new Vector2(0, 0.5f);
            content.pivot = new Vector2(0, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(1200, 190);

            var layout = contentObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 10;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var scroll = GetComponent<ScrollRect>() ?? gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
        }

        private void Start() => Render();

        public void ResetSelection()
        {
            selectedIds.Clear();
            Render();
            SelectionChanged?.Invoke();
        }

        public void SetSelection(IEnumerable<int> ids)
        {
            selectedIds.Clear();
            foreach (int id in ids)
                selectedIds.Add(id);
            Render();
            SelectionChanged?.Invoke();
        }

        public void Render()
        {
            if (bootstrap == null)
                bootstrap = FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.Engine == null || cardPrefab == null || content == null)
                return;

            GameState state = bootstrap.Engine.State;
            selectedIds.IntersectWith(state.Hand(0));
            int[] ids = state.Hand(0)
                .OrderBy(id => state.Cards[id].Rank)
                .ThenBy(id => state.Cards[id].Suit)
                .ToArray();

            content.sizeDelta = new Vector2(Mathf.Max(1200, ids.Length * 150 - 10), 190);
            for (int i = 0; i < ids.Length; i++)
            {
                if (i == views.Count)
                    views.Add(Instantiate(cardPrefab, content));

                int id = ids[i];
                CardInstance card = state.Cards[id];
                CardVisual view = views[i];
                view.gameObject.SetActive(true);
                view.CardId = id;
                view.Bind(SpriteFor(card),
                    card.Effect == EffectId.None ? "" :
                        (BadgeText != null ? BadgeText(card.Effect) : EffectCatalog.Badge(card.Effect)),
                    Color.white, "", selectedIds.Contains(id),
                    state.Actor == 0 && state.Phase == TurnPhase.Select,
                    () => ToggleSelection(id, view),
                    () => DetailsShown?.Invoke(card),
                    () => DetailsHidden?.Invoke());
            }

            for (int i = ids.Length; i < views.Count; i++)
                views[i].gameObject.SetActive(false);
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        public Sprite SpriteFor(CardInstance card)
        {
            if (card.Rank == 16)
                return smallJoker != null ? smallJoker : Resources.Load<Sprite>("Art/Cards/cardJokerSmall");
            if (card.Rank == 17)
                return bigJoker != null ? bigJoker : Resources.Load<Sprite>("Art/Cards/cardJoker");

            string path = "Art/Cards/card" + SuitNames[card.Suit] + CardInstance.RankName(card.Rank);
            if (!spriteCache.TryGetValue(path, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                spriteCache[path] = sprite;
            }
            return sprite;
        }

        private void ToggleSelection(int id, CardVisual view)
        {
            if (!selectedIds.Add(id))
                selectedIds.Remove(id);
            view.selectionOutline.enabled = selectedIds.Contains(id);
            CardClicked?.Invoke();
            SelectionChanged?.Invoke();
        }
    }
}