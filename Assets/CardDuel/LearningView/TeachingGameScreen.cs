using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace CardDuel.Learning
{
    // uGUI presents the game. Only GameEngine may change cards, HP or turn state.
    public sealed partial class TeachingGameScreen : MonoBehaviour
    {
        private GameBootstrap bootstrap;
        private HandView hand;
        private GameEngine game;
        private Font font;
        private bool ownsFont;
        private bool executing;
        private float nextAi;
        private string dialog = "";
        private string feedback = "";
        private int page;
        private int effectSource = -1;
        private bool putTop = true;
        private readonly HashSet<int> targets = new HashSet<int>();
        private readonly List<int> top = new List<int>();
        private readonly List<int> bottom = new List<int>();
        private IReadOnlyList<PlayCommand> playOptions = Array.Empty<PlayCommand>();

        private RectTransform root;
        private RectTransform lastCards;
        private RectTransform modalContent;
        private GameObject actionBar;
        private GameObject modalShade;
        private Text statusText, hpText, opponentText, pileText, lastText;
        private Text feedbackText, detailText, modalTitle, previousCaption;
        private Button playButton, passButton, hintButton, clearButton, logButton;
        private Image discardImage;

        private void Start()
        {
            bootstrap = GetComponent<GameBootstrap>();
            hand = FindAnyObjectByType<HandView>();
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (bootstrap == null || hand == null || canvas == null)
            {
                Debug.LogError("教学游戏界面缺少 GameBootstrap、HandView 或 Canvas。", this);
                enabled = false;
                return;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 900);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Yu Gothic", "Arial" }, 20);
                ownsFont = true;
            }

            InitializeSettingsAudio();
            BuildUi(canvas.transform);
            hand.BadgeText = Badge;
            hand.CardClicked += OnCardClicked;
            hand.SelectionChanged += RefreshControls;
            hand.DetailsShown += ShowDetails;
            hand.DetailsHidden += HideDetails;
            Attach(bootstrap.Engine);
            Refresh();
        }

        private void OnDestroy()
        {
            if (game != null) game.Changed -= OnGameChanged;
            if (hand != null)
            {
                hand.CardClicked -= OnCardClicked;
                hand.SelectionChanged -= RefreshControls;
                hand.DetailsShown -= ShowDetails;
                hand.DetailsHidden -= HideDetails;
            }
            if (ownsFont && font != null) Destroy(font);
        }

        private void Attach(GameEngine engine)
        {
            if (game != null) game.Changed -= OnGameChanged;
            game = engine;
            game.Changed += OnGameChanged;
            nextAi = Time.unscaledTime + AiDelay;
        }

        private void OnGameChanged()
        {
            if (!executing) Refresh();
        }

        private void Update()
        {
            TickSettingsSave();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ToggleSettings();
            if (game == null || dialog != "" || game.RevealedToHuman.Count > 0)
                return;
            if (game.State.Phase == TurnPhase.Rps || game.State.Phase == TurnPhase.GameOver)
                return;

            if (game.State.Actor == 1)
            {
                if (Time.unscaledTime >= nextAi)
                    Act(() => game.StepAi(1, difficulty));
                return;
            }

            if (game.State.Phase == TurnPhase.Draw && !game.CanBuy(0))
            {
                Act(() => game.ChooseDraw(0, false));
            }
            else if (game.State.Phase == TurnPhase.Post || game.State.Phase == TurnPhase.Discard)
            {
                EffectChoice automatic = game.AutomaticEffect(0);
                if (automatic != null)
                    Act(() => game.ResolveEffect(0, automatic));
                else
                {
                    dialog = "effects";
                    page = 0;
                    RefreshModal();
                }
            }
        }

        private void Act(Func<bool> command)
        {
            TurnPhase oldPhase = game.State.Phase;
            int eventStart = game.State.BattleLog.Count;
            int hp0 = game.State.Hp(0), hp1 = game.State.Hp(1);
            executing = true;
            bool success;
            try { success = command(); }
            finally { executing = false; }
            if (!success)
            {
                feedback = game.Error;
            }
            else
            {
                PlayActionAudio(oldPhase, eventStart, hp0, hp1);
                feedback = "";
                dialog = "";
                nextAi = Time.unscaledTime + AiDelay;
                if (game.State.Actor != 0 || game.State.Phase != TurnPhase.Select)
                    hand.ResetSelection();
            }
            Refresh();
        }

        private void Restart() => RestartWithSeed(Environment.TickCount);

        private void OnCardClicked() => PlaySound("click");

        private void ShowDetails(CardInstance card)
        {
            detailText.text = L(card.Face) + "  #" + card.Id + "  " +
                L(EffectCatalog.Description(card.Effect));
        }
        private void HideDetails() => detailText.text = "";

        private void BuildUi(Transform canvas)
        {
            root = Rect("Teaching UI", canvas, new Vector2(0, 0), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            root.offsetMin = root.offsetMax = Vector2.zero;

            RectTransform topPanel = Panel("Top HUD", root, new Vector2(0.5f, 1),
                new Vector2(0, -52), new Vector2(1510, 78), new Color(0.10f, 0.26f, 0.19f, 0.92f));
            hpText = Label("Player HP", topPanel, new Vector2(-565, 0), new Vector2(345, 58), 24, TextAnchor.MiddleLeft);
            statusText = Label("Turn State", topPanel, Vector2.zero, new Vector2(650, 58), 25, TextAnchor.MiddleCenter);
            opponentText = Label("Opponent", topPanel, new Vector2(555, 0), new Vector2(365, 58), 24, TextAnchor.MiddleRight);

            RectTransform deckPanel = Panel("Deck", root, new Vector2(0, 0.5f),
                new Vector2(110, 105), new Vector2(190, 230), new Color(0.11f, 0.30f, 0.21f, 0.93f));
            Picture("Card Back", deckPanel, new Vector2(0, 15), new Vector2(94, 132),
                Resources.Load<Sprite>("Art/Cards/cardBack_blue1"));
            pileText = Label("Deck Count", deckPanel, new Vector2(0, -84), new Vector2(175, 37), 22, TextAnchor.MiddleCenter);
            RectTransform discardPanel = Panel("Discard", root, new Vector2(0, 0.5f),
                new Vector2(110, -145), new Vector2(190, 230), new Color(0.11f, 0.30f, 0.21f, 0.93f));
            discardImage = Picture("Top Discard", discardPanel, new Vector2(0, 15), new Vector2(94, 132), null);
            lastText = Label("Discard Count", discardPanel, new Vector2(0, -84), new Vector2(175, 37), 22, TextAnchor.MiddleCenter);

            RectTransform playPanel = Panel("Previous Play", root, new Vector2(0.5f, 0.5f),
                new Vector2(60, 94), new Vector2(840, 215), new Color(0.11f, 0.30f, 0.21f, 0.89f));
            previousCaption = Label("Previous Caption", playPanel, new Vector2(0, 82),
                new Vector2(800, 38), 23, TextAnchor.MiddleCenter);
            lastCards = Rect("Previous Cards", playPanel, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -15), new Vector2(810, 150));

            RectTransform action = Panel("Actions", root, new Vector2(0.5f, 0),
                new Vector2(0, 280), new Vector2(900, 68), new Color(0.10f, 0.26f, 0.19f, 0.93f));
            actionBar = action.gameObject;
            playButton = UiButton("出牌", action, new Vector2(-320, 0), new Vector2(145, 52),
                "Green", ShowPlayOptions);
            passButton = UiButton("要不起", action, new Vector2(-155, 0), new Vector2(145, 52),
                "Red", () => Act(() => game.Pass(0)));
            hintButton = UiButton("提示", action, new Vector2(10, 0),
                new Vector2(145, 52), "Blue", Hint);
            clearButton = UiButton("清除选择", action, new Vector2(175, 0),
                new Vector2(145, 52), "Grey", hand.ResetSelection);
            logButton = UiButton("日志", action, new Vector2(340, 0),
                new Vector2(145, 52), "Yellow", () => Open("log"));

            feedbackText = Label("Feedback", root, new Vector2(0, 0), new Vector2(900, 34), 22, TextAnchor.MiddleCenter);
            feedbackText.rectTransform.anchorMin = feedbackText.rectTransform.anchorMax = new Vector2(0.5f, 0);
            feedbackText.rectTransform.anchoredPosition = new Vector2(0, 330);
            detailText = Label("Card Details", root, new Vector2(0, 0), new Vector2(1200, 54), 19, TextAnchor.MiddleCenter);
            detailText.rectTransform.anchorMin = detailText.rectTransform.anchorMax = new Vector2(0.5f, 0);
            detailText.rectTransform.anchoredPosition = new Vector2(0, 360);

            RectTransform shade = Panel("Modal Shade", root, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.68f));
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            modalShade = shade.gameObject;
            RectTransform modal = Panel("Modal Panel", shade, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1120, 660), Color.white,
                "Art/UI/Extra/input_rectangle");
            modalTitle = Label("Modal Title", modal, new Vector2(0, 275), new Vector2(1030, 60),
                30, TextAnchor.MiddleCenter, new Color(0.12f, 0.24f, 0.18f));
            modalContent = Rect("Modal Content", modal, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(1030, 490));

            Button gear = UiButton("⚙", root, Vector2.zero, new Vector2(66, 58),
                "Yellow", ToggleSettings);
            RectTransform gearRect = gear.GetComponent<RectTransform>();
            gearRect.anchorMin = gearRect.anchorMax = new Vector2(1, 1);
            gearRect.anchoredPosition = new Vector2(-70, -120);
        }

        private void Refresh()
        {
            if (game == null || root == null) return;
            hand.Render();
            GameState state = game.State;
            string phase = state.Phase switch
            {
                TurnPhase.Rps => "猜拳",
                TurnPhase.Draw => "抽牌",
                TurnPhase.Select => "选牌",
                TurnPhase.Post => "出牌后效果",
                TurnPhase.Discard => "弃牌后效果",
                _ => "游戏结束"
            };
            previousCaption.text = L("上一手");
            playButton.GetComponentInChildren<Text>().text = L("出牌");
            passButton.GetComponentInChildren<Text>().text = L("要不起");
            hintButton.GetComponentInChildren<Text>().text = L("提示");
            clearButton.GetComponentInChildren<Text>().text = L("清除选择");
            logButton.GetComponentInChildren<Text>().text = L("日志");
            statusText.text = F("第 {0} 回合 · 第 {1} 墩 · {2} · {3}",
                "ターン {0} · トリック {1} · {2} · {3}",
                "Turn {0} · Trick {1} · {2} · {3}",
                state.Turn, state.Trick, L(state.Actor == 0 ? "你" : "对手"), L(phase));
            hpText.text = F("你  ♥ {0}    手牌 {1}", "あなた  ♥ {0}    手札 {1}",
                "You  ♥ {0}    Hand {1}", state.Hp(0), state.Hand(0).Count);
            opponentText.text = F("对手  ♥ {0}    手牌 {1}", "相手  ♥ {0}    手札 {1}",
                "Opponent  ♥ {0}    Hand {1}", state.Hp(1), state.Hand(1).Count);
            pileText.text = F("抽牌堆  {0}", "山札  {0}", "Deck  {0}", state.DrawPile.Count);
            lastText.text = F("弃牌堆  {0}", "捨て札  {0}", "Discard  {0}", state.DiscardPile.Count);
            discardImage.sprite = state.DiscardPile.Count == 0 ? null :
                hand.SpriteFor(state.Cards[state.DiscardPile[state.DiscardPile.Count - 1]]);
            discardImage.enabled = discardImage.sprite != null;
            BattleLogEntry lastEntry = state.BattleLog.LastOrDefault();
            feedbackText.text = feedback != "" ? L(feedback) :
                lastEntry == null ? "" : LogLine(lastEntry).Replace("\n", " / ");
            RefreshPrevious();
            RefreshControls();
            RefreshModal();
        }

        private void RefreshPrevious()
        {
            Clear(lastCards);
            PlaySnapshot previous = game.State.LastPlay;
            if (previous == null)
            {
                Label("No Previous Play", lastCards, Vector2.zero, new Vector2(770, 65), 22,
                    TextAnchor.MiddleCenter).text =
                    F("当前没有需要压制的牌；先手不能 Pass",
                      "直前の手はありません。先手はパスできません。",
                      "No previous play to beat. The leader cannot pass.");
                return;
            }
            int visible = Math.Min(previous.CardIds.Count, 8);
            float step = 92;
            for (int i = 0; i < visible; i++)
            {
                int id = previous.CardIds[i];
                Sprite sprite = hand.SpriteFor(game.State.Cards[id]);
                Picture("Played Card #" + id, lastCards,
                    new Vector2((i - (visible - 1) / 2f) * step, 2), new Vector2(88, 120), sprite);
            }
            string caption = L(previous.Pattern.ToString());
            if (previous.CardIds.Count > visible)
                caption += F("（仅展示前8张）", "（先頭8枚のみ表示）",
                    " (first 8 shown)");
            Label("Pattern", lastCards, new Vector2(0, -69), new Vector2(810, 30), 19,
                TextAnchor.MiddleCenter).text = caption;
        }

        private void RefreshControls()
        {
            if (game == null || actionBar == null) return;
            bool selecting = game.State.Actor == 0 && game.State.Phase == TurnPhase.Select;
            actionBar.SetActive(selecting);
            if (!selecting) return;
            playButton.interactable = hand.SelectedIds.Length > 0;
            passButton.interactable = game.State.LastPlay != null && game.State.LastPlay.Actor != 0;
        }

        private void ShowPlayOptions()
        {
            int[] selected = hand.SelectedIds;
            playOptions = game.PlayOptions(0, selected);
            if (playOptions.Count == 0)
            {
                feedback = game.PreviewSelection(0, selected).Reason;
                Refresh();
                return;
            }
            if (playOptions.Count == 1)
            {
                PlayCommand only = playOptions[0];
                Act(() => game.Commit(0, only));
                return;
            }
            Open("play");
        }

        private void Hint()
        {
            PlayCommand hint = AiController.LegalPlays(game.Observe(0), 384).FirstOrDefault();
            if (hint == null)
            {
                feedback = "没有可出的牌，可以考虑 Pass";
                Refresh();
                return;
            }
            hand.SetSelection(hint.Cards);
            feedback = "已选中一组可出牌；点击出牌确认具体效果";
            Refresh();
        }

        private void Open(string name)
        {
            dialog = name;
            page = 0;
            RefreshModal();
        }

        private void SelectEffect(CardInstance card)
        {
            effectSource = card.Id;
            targets.Clear(); top.Clear(); bottom.Clear();
            putTop = true;
            page = 0;
            switch (card.Effect)
            {
                case EffectId.SwapTwoInHand: dialog = "swap"; break;
                case EffectId.PeekOpponentTwo: dialog = "peek"; break;
                case EffectId.ReorderTopFive: dialog = "order"; break;
                default:
                    Act(() => game.ResolveEffect(0, new EffectChoice { Source = card.Id }));
                    return;
            }
            RefreshModal();
        }

        private void ConfirmTargetEffect()
        {
            var choice = new EffectChoice
            {
                Source = effectSource,
                Targets = targets.ToArray(),
                Top = top.ToArray(),
                Bottom = bottom.ToArray()
            };
            Act(() => game.ResolveEffect(0, choice));
        }

        private void RefreshModal()
        {
            if (modalShade == null || game == null) return;
            string mode = dialog == "settings" || dialog == "rulebook" || dialog == "log"
                ? dialog : game.RevealedToHuman.Count > 0 ? "peekResult" :
                game.State.Phase == TurnPhase.GameOver ? "gameOver" :
                game.State.Phase == TurnPhase.Rps ? "rps" :
                game.State.Phase == TurnPhase.Draw && game.State.Actor == 0 && game.CanBuy(0) ? "draw" : dialog;
            modalShade.SetActive(mode != "");
            if (mode == "") return;
            Clear(modalContent);

            switch (mode)
            {
                case "rps": DrawRps(); break;
                case "draw": DrawPurchase(); break;
                case "play": DrawPlayOptions(); break;
                case "effects": DrawEffectChoices(); break;
                case "swap": DrawTargets(false); break;
                case "peek": DrawTargets(true); break;
                case "order": DrawReorder(); break;
                case "peekResult": DrawPeekResult(); break;
                case "log": DrawLog(); break;
                case "gameOver": DrawGameOver(); break;
                case "settings": DrawSettings(); break;
                case "rulebook": DrawRulebook(); break;
            }
        }

        private void DrawRps()
        {
            modalTitle.text = L("猜拳决定先手");
            string[] gestures = { "石头", "剪刀", "布" };
            for (int i = 0; i < 3; i++)
            {
                int choice = i;
                UiButton(gestures[i], modalContent, new Vector2((i - 1) * 250, 35),
                    new Vector2(210, 95), "Green", () => Act(() => game.Guess(choice)));
            }
            Label("RPS Note", modalContent, new Vector2(0, -95), new Vector2(900, 72), 23,
                TextAnchor.MiddleCenter, Color.black).text =
                L("平局重猜；胜者先手，第一次行动免费抽 0 张。");
        }

        private void DrawPurchase()
        {
            modalTitle.text = L("抽牌阶段");
            Label("Draw Prompt", modalContent, new Vector2(0, 95), new Vector2(900, 90), 25,
                TextAnchor.MiddleCenter, Color.black).text =
                F("本回合先免费抽 {0} 张。是否花 {1} 点生命再抽 {2} 张？",
                  "このターンの無料ドローは{0}枚。HPを{1}払って追加で{2}枚引きますか？",
                  "Draw {0} free cards this turn. Pay {1} HP for {2} extra cards?",
                  game.State.FreeDraw, game.Config.ExtraDrawCost, game.Config.ExtraDrawCount);
            UiButton("购买", modalContent, new Vector2(-150, -50), new Vector2(240, 72),
                "Green", () => Act(() => game.ChooseDraw(0, true)));
            UiButton("不购买", modalContent, new Vector2(150, -50), new Vector2(240, 72),
                "Grey", () => Act(() => game.ChooseDraw(0, false)));
        }

        private void DrawPlayOptions()
        {
            modalTitle.text = L("确认出牌方式");
            int start = page * 7;
            for (int i = start; i < Math.Min(start + 7, playOptions.Count); i++)
            {
                PlayCommand command = playOptions[i];
                PreviewResult preview = game.Preview(0, command);
                string caption = L(preview.Selected.ToString()) +
                    (command.PreSource < 0 ? " · " + L("不发动牌前效果") :
                    " · " + L(EffectCatalog.Name(game.State.Cards[command.PreSource].Effect)) +
                    " #" + command.PreSource +
                    (command.CopyTarget >= 0 ? " → " +
                        L(game.State.Cards[command.CopyTarget].Face) : ""));
                UiButton(caption, modalContent, new Vector2(0, 188 - (i - start) * 57),
                    new Vector2(930, 51), "Blue", () => Act(() => game.Commit(0, command)));
            }
            Pager(playOptions.Count, 7);
            UiButton("返回选牌", modalContent, new Vector2(0, -212), new Vector2(190, 42),
                "Grey", () => { dialog = ""; RefreshModal(); });
        }

        private void DrawEffectChoices()
        {
            modalTitle.text = L("选择本阶段发动的效果");
            var candidates = game.UsableEffects(0);
            int start = page * 7;
            for (int i = start; i < Math.Min(start + 7, candidates.Count); i++)
            {
                CardInstance card = candidates[i];
                string caption = L(card.Face) + "  #" + card.Id +
                    " · " + L(EffectCatalog.Name(card.Effect));
                UiButton(caption, modalContent, new Vector2(0, 188 - (i - start) * 57),
                    new Vector2(930, 51), "Green", () => SelectEffect(card));
            }
            Pager(candidates.Count, 7);
            UiButton("跳过本阶段效果", modalContent, new Vector2(0, -212), new Vector2(260, 42),
                "Grey", () => Act(() => game.ResolveEffect(0, new EffectChoice())));
        }

        private void DrawTargets(bool opponent)
        {
            modalTitle.text = L(opponent ? "选择对手两张背面牌" :
                "选择自己的两张手牌换入牌库");
            int[] items = opponent ? Enumerable.Range(0, game.State.Hand(1).Count).ToArray() :
                game.State.Hand(0).ToArray();
            int start = page * 24;
            for (int i = start; i < Math.Min(start + 24, items.Length); i++)
            {
                int value = items[i];
                int local = i - start;
                Vector2 pos = new Vector2((local % 8 - 3.5f) * 117,
                    145 - local / 8 * 145);
                Sprite sprite = opponent ? Resources.Load<Sprite>("Art/Cards/cardBack_blue1") :
                    hand.SpriteFor(game.State.Cards[value]);
                RectTransform cell = UiButton("", modalContent, pos, new Vector2(106, 138),
                    targets.Contains(value) ? "Yellow" : "Grey", () =>
                    {
                        if (!targets.Add(value)) targets.Remove(value);
                        else if (targets.Count > 2) targets.Remove(value);
                        RefreshModal();
                    }).GetComponent<RectTransform>();
                Picture("Card", cell, new Vector2(0, 7), new Vector2(78, 105), sprite);
                Label("Card Number", cell, new Vector2(0, -54), new Vector2(94, 22), 15,
                    TextAnchor.MiddleCenter, Color.black).text = opponent ?
                    F("位置 {0}", "位置 {0}", "Slot {0}", value + 1) :
                    L(game.State.Cards[value].Face) + " #" + value;
            }
            Pager(items.Length, 24);
            UiButton(F("确认 {0}/2", "確定 {0}/2", "Confirm {0}/2",
                targets.Count), modalContent, new Vector2(190, -215),
                new Vector2(190, 43), "Green", ConfirmTargetEffect).interactable = targets.Count == 2;
            UiButton("返回效果列表", modalContent, new Vector2(-190, -215),
                new Vector2(190, 43), "Grey", () => Open("effects"));
        }

        private void DrawReorder()
        {
            modalTitle.text = L("将牌顶五张依次放到牌顶或牌底");
            var cards = game.ReorderPreview(0, effectSource);
            UiButton(putTop ? "● 放牌顶" : "放牌顶", modalContent, new Vector2(-125, 182),
                new Vector2(210, 42), "Green", () => { putTop = true; RefreshModal(); });
            UiButton(putTop ? "放牌底" : "● 放牌底", modalContent, new Vector2(125, 182),
                new Vector2(210, 42), "Blue", () => { putTop = false; RefreshModal(); });
            for (int i = 0; i < cards.Count; i++)
            {
                CardInstance card = cards[i];
                bool assigned = top.Contains(card.Id) || bottom.Contains(card.Id);
                RectTransform cell = UiButton("", modalContent,
                    new Vector2((i - (cards.Count - 1) / 2f) * 185, 45), new Vector2(160, 185),
                    assigned ? "Yellow" : "Grey", () =>
                    {
                        if (assigned) { top.Remove(card.Id); bottom.Remove(card.Id); }
                        else (putTop ? top : bottom).Add(card.Id);
                        RefreshModal();
                    }).GetComponent<RectTransform>();
                Picture("Top Card", cell, new Vector2(0, 12), new Vector2(115, 150),
                    hand.SpriteFor(card));
                Label("Assignment", cell, new Vector2(0, -74), new Vector2(150, 28), 17,
                    TextAnchor.MiddleCenter, Color.black).text =
                    top.Contains(card.Id) ?
                        F("牌顶 {0}", "上 {0}", "Top {0}", top.IndexOf(card.Id) + 1) :
                    bottom.Contains(card.Id) ?
                        F("牌底 {0}", "下 {0}", "Bottom {0}", bottom.IndexOf(card.Id) + 1) :
                        L("未分配");
            }
            Label("Top Order", modalContent, new Vector2(0, -120), new Vector2(1000, 35), 18,
                TextAnchor.MiddleCenter, Color.black).text =
                F("牌顶（先抽 → 后抽）：{0}", "山札の上（先 → 後）：{0}",
                  "Top (draw first → last): {0}",
                  string.Join(" → ", top.Select(id => L(game.State.Cards[id].Face))));
            Label("Bottom Order", modalContent, new Vector2(0, -157), new Vector2(1000, 35), 18,
                TextAnchor.MiddleCenter, Color.black).text =
                F("牌底（靠前 → 最底）：{0}", "山札の下（上 → 下）：{0}",
                  "Bottom (near → far): {0}",
                  string.Join(" → ", bottom.Select(id => L(game.State.Cards[id].Face))));
            UiButton("返回效果列表", modalContent, new Vector2(-190, -218),
                new Vector2(190, 43), "Grey", () => Open("effects"));
            UiButton("确认排序", modalContent, new Vector2(190, -218),
                new Vector2(190, 43), "Green", ConfirmTargetEffect).interactable =
                top.Count + bottom.Count == cards.Count;
        }

        private void DrawPeekResult()
        {
            modalTitle.text = L("查看到的两张对手手牌");
            for (int i = 0; i < game.RevealedToHuman.Count; i++)
            {
                CardInstance card = game.RevealedToHuman[i];
                Picture("Revealed Card", modalContent, new Vector2((i - 0.5f) * 180, 25),
                    new Vector2(145, 195), hand.SpriteFor(card));
            }
            UiButton("记住了，继续", modalContent, new Vector2(0, -185),
                new Vector2(260, 55), "Green", () =>
                {
                    game.DismissPeek();
                    Refresh();
                });
        }

        private void DrawLog()
        {
            modalTitle.text = L("对战日志");
            var entries = game.State.BattleLog;
            int start = page * 9;
            for (int i = start; i < Math.Min(start + 9, entries.Count); i++)
            {
                Label("Log " + i, modalContent, new Vector2(0, 192 - (i - start) * 43),
                    new Vector2(975, 40), 17, TextAnchor.MiddleLeft, Color.black).text =
                    LogLine(entries[i]).Replace("\n", " / ");
            }
            Pager(entries.Count, 9);
            UiButton("返回牌桌", modalContent, new Vector2(0, -213),
                new Vector2(180, 42), "Grey", () => { dialog = ""; RefreshModal(); });
        }

        private void DrawGameOver()
        {
            modalTitle.text = L(game.State.Winner == 0 ? "你赢了！" : "对手赢了");
            Label("Result", modalContent, new Vector2(0, 90), new Vector2(900, 75), 24,
                TextAnchor.MiddleCenter, Color.black).text =
                F("共进行 {0} 回合。你可以打开新局继续练习。",
                  "全{0}ターン。新しい対戦を始められます。",
                  "{0} turns played. You can start a new match.",
                  game.State.Turn);
            UiButton("再来一局", modalContent, new Vector2(0, -55),
                new Vector2(245, 70), "Green", Restart);
        }

        private void Pager(int itemCount, int pageSize)
        {
            int pages = Math.Max(1, (itemCount + pageSize - 1) / pageSize);
            if (pages <= 1) return;
            UiButton("上一页", modalContent, new Vector2(-405, -213),
                new Vector2(130, 42), "Grey", () => { page = Math.Max(0, page - 1); RefreshModal(); })
                .interactable = page > 0;
            Label("Page", modalContent, new Vector2(0, -213), new Vector2(140, 42), 18,
                TextAnchor.MiddleCenter, Color.black).text = (page + 1) + " / " + pages;
            UiButton("下一页", modalContent, new Vector2(405, -213),
                new Vector2(130, 42), "Grey", () => { page = Math.Min(pages - 1, page + 1); RefreshModal(); })
                .interactable = page + 1 < pages;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform Panel(string name, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size, Color color, string spritePath = null)
        {
            RectTransform rect = Rect(name, parent, anchor, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = spritePath == null ? null : Resources.Load<Sprite>(spritePath);
            image.color = color;
            return rect;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size,
            int fontSize, TextAnchor alignment, Color? color = null)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private Image Picture(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        private Button UiButton(string caption, Transform parent, Vector2 position,
            Vector2 size, string color, Action click)
        {
            RectTransform rect = Rect(caption == "" ? "Card Button" : caption, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("Art/UI/" + color + "/button_rectangle_flat");
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                PlaySound("click");
                click();
            });
            if (caption != "")
                Label("Caption", rect, Vector2.zero, size - new Vector2(12, 8), 20,
                    TextAnchor.MiddleCenter, new Color(0.10f, 0.20f, 0.16f));
            if (caption != "")
                rect.GetComponentInChildren<Text>().text = L(caption);
            return button;
        }

        private static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
