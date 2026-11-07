using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardDuel.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CardDuel.Learning
{
    public sealed partial class TeachingGameScreen
    {
        // A separate key keeps this teaching project independent of the original game.
        private const string SettingsKey = "CardDuel.Teaching.Settings.v1";
        private static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900),
            new Vector2Int(1600, 960), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440)
        };
        private static readonly string[] SoundNames =
            { "click", "draw", "play", "shuffle", "return", "effect", "damage", "heal", "exile" };

        private UserSettings settings = new UserSettings();
        private AudioSource musicSource;
        private AudioSource soundSource;
        private AudioClip[] sounds;
        private bool settingsDirty;
        private float settingsSaveAt;
        private int settingsTab;
        private string previousDialog = "";
        private int helpTab;
        private Difficulty difficulty = Difficulty.Normal;
        private bool fast;

        private Language CurrentLanguage => (Language)settings.Language;
        private float AiDelay => fast ? 0.12f : 0.5f;

        // The original catalogue covers engine text. These are the new uGUI-only captions.
        private static readonly Dictionary<string, string[]> UiWords =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["猜拳决定先手"] = new[] { "じゃんけんで先手を決める", "Rock, paper, scissors" },
                ["平局重猜；胜者先手，第一次行动免费抽 0 张。"] = new[] { "あいこは再挑戦。勝者が先手で、最初の無料ドローは0枚。", "Ties replay. The winner leads and skips the first free draw." },
                ["抽牌阶段"] = new[] { "ドロー段階", "Draw phase" },
                ["选牌"] = new[] { "カード選択", "Select cards" },
                ["抽牌"] = new[] { "ドロー", "Draw" },
                ["出牌后效果"] = new[] { "出した後の効果", "After-play effect" },
                ["弃牌后效果"] = new[] { "捨てた後の効果", "After-discard effect" },
                ["游戏结束"] = new[] { "対戦終了", "Game over" },
                ["购买"] = new[] { "購入する", "Buy" },
                ["不购买"] = new[] { "購入しない", "Do not buy" },
                ["确认出牌方式"] = new[] { "出し方を選ぶ", "Choose how to play" },
                ["不发动牌前效果"] = new[] { "出す前の効果を使わない", "No before-play effect" },
                ["返回选牌"] = new[] { "カード選択に戻る", "Back to selection" },
                ["选择本阶段发动的效果"] = new[] { "この段階の効果を選ぶ", "Choose an effect for this stage" },
                ["跳过本阶段效果"] = new[] { "この段階の効果を使わない", "Skip this stage's effect" },
                ["选择对手两张背面牌"] = new[] { "相手の裏向きカードを2枚選ぶ", "Choose two face-down opponent cards" },
                ["选择自己的两张手牌换入牌库"] = new[] { "山札に戻す手札を2枚選ぶ", "Choose two hand cards to exchange" },
                ["返回效果列表"] = new[] { "効果一覧に戻る", "Back to effects" },
                ["将牌顶五张依次放到牌顶或牌底"] = new[] { "上から5枚を山札の上か下に並べる", "Assign up to five cards to the deck top or bottom" },
                ["● 放牌顶"] = new[] { "● 山札の上へ", "● To top" },
                ["放牌顶"] = new[] { "山札の上へ", "To top" },
                ["● 放牌底"] = new[] { "● 山札の下へ", "● To bottom" },
                ["放牌底"] = new[] { "山札の下へ", "To bottom" },
                ["未分配"] = new[] { "未配置", "Unassigned" },
                ["查看到的两张对手手牌"] = new[] { "確認した相手の手札2枚", "Two revealed opponent cards" },
                ["返回牌桌"] = new[] { "対戦に戻る", "Back to game" },
                ["对手赢了"] = new[] { "相手の勝ち", "Opponent wins" },
                ["上一页"] = new[] { "前へ", "Previous" },
                ["下一页"] = new[] { "次へ", "Next" },
                ["清除选择"] = new[] { "選択解除", "Clear selection" },
                ["日志"] = new[] { "履歴", "Log" },
                ["上一手"] = new[] { "直前の手", "Previous play" },
                ["没有可出的牌，可以考虑 Pass"] = new[] { "出せるカードがありません。パスを検討してください。", "No legal play found. Consider passing." },
                ["已选中一组可出牌；点击出牌确认具体效果"] = new[] { "出せる組み合わせを選択しました。「出す」で効果を確認してください。", "A legal play is selected. Press Play to choose its effect." },
                ["规则手册"] = new[] { "ルールブック", "Rulebook" },
                ["新开一局"] = new[] { "新しい対戦", "New match" },
                ["确认"] = new[] { "確定", "Confirm" },
                ["牌桌"] = new[] { "対戦", "Game" }
            };

        private string L(string source)
        {
            if (string.IsNullOrEmpty(source) || CurrentLanguage == Language.Chinese)
                return source;
            string known = Localization.Text(source, CurrentLanguage);
            if (known != source)
                return known;
            return UiWords.TryGetValue(source, out string[] words)
                ? words[settings.Language - 1] : source;
        }

        private string F(string chinese, string japanese, string english, params object[] values)
        {
            string format = CurrentLanguage == Language.Japanese ? japanese :
                CurrentLanguage == Language.English ? english : chinese;
            return string.Format(CultureInfo.InvariantCulture, format, values);
        }

        private string Badge(EffectId effect)
        {
            if (CurrentLanguage == Language.Chinese) return EffectCatalog.Badge(effect);
            string[] labels = CurrentLanguage == Language.Japanese
                ? new[] { "—", "写", "捨1", "下げ", "除外", "引2", "戻す", "混ぜ", "見2", "交換", "並替" }
                : new[] { "—", "COPY", "DROP", "LOW", "EXILE", "DRAW", "BACK", "MIX", "PEEK", "SWAP", "ORDER" };
            return labels[(int)effect];
        }

        private string LogLine(BattleLogEntry entry)
        {
            string actor = entry.Actor < 0 ? L("系统") :
                entry.Actor == 0 ? L("你") : L("对手");
            return "[T" + entry.Turn + "] " + actor + ": " + L(entry.Text);
        }

        private void InitializeSettingsAudio()
        {
            try
            {
                settings = JsonUtility.FromJson<UserSettings>(
                    PlayerPrefs.GetString(SettingsKey, "")) ?? new UserSettings();
            }
            catch (ArgumentException)
            {
                settings = new UserSettings();
            }
            settings.Sanitize();
            difficulty = (Difficulty)settings.Difficulty;
            fast = settings.Fast;
            if (!Application.isEditor) ApplyWindowSize(settings.Resolution);

            if (FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0;
            musicSource.clip = Resources.Load<AudioClip>("Audio/bgm");
            musicSource.volume = settings.Music;
            soundSource = gameObject.AddComponent<AudioSource>();
            soundSource.playOnAwake = false;
            soundSource.spatialBlend = 0;
            soundSource.volume = settings.Sound;
            sounds = SoundNames.Select(name =>
                Resources.Load<AudioClip>("Audio/" + name)).ToArray();
            if (musicSource.clip != null) musicSource.Play();
            else Debug.LogError("Missing teaching BGM: Audio/bgm", this);
            if (sounds.Any(clip => clip == null))
                Debug.LogError("Missing teaching sound effect", this);
        }

        private void PlaySound(string name)
        {
            int index = Array.IndexOf(SoundNames, name);
            if (soundSource == null || index < 0 || sounds[index] == null ||
                settings.Sound <= 0) return;
            soundSource.PlayOneShot(sounds[index]);
        }

        private void PlayActionAudio(TurnPhase oldPhase, int eventStart, int hp0, int hp1)
        {
            var entries = game.State.BattleLog.Skip(eventStart).ToArray();
            var effect = entries.FirstOrDefault(entry =>
                entry.Kind == BattleEventKind.Effect);
            if (game.State.Phase == TurnPhase.GameOver)
                PlaySound("heal");
            else if (effect != null)
            {
                switch (effect.Effect)
                {
                    case EffectId.DrawTwo: PlaySound("draw"); break;
                    case EffectId.ReturnThisCard: PlaySound("return"); break;
                    case EffectId.ExileJoker: PlaySound("exile"); break;
                    case EffectId.ReshuffleDiscard:
                    case EffectId.SwapTwoInHand:
                    case EffectId.ReorderTopFive: PlaySound("shuffle"); break;
                    default: PlaySound("effect"); break;
                }
            }
            else if (oldPhase == TurnPhase.Select && game.State.Phase == TurnPhase.Post)
                PlaySound("play");
            else if (entries.Any(entry => entry.Kind == BattleEventKind.Recycle))
                PlaySound("shuffle");
            else if (entries.Any(entry => entry.Kind == BattleEventKind.Draw))
                PlaySound("draw");

            if (game.State.Hp(0) < hp0 || game.State.Hp(1) < hp1)
                PlaySound("damage");
            else if (game.State.Hp(0) > hp0 || game.State.Hp(1) > hp1)
                PlaySound("heal");
        }

        private void SaveSettings()
        {
            settings.Difficulty = (int)difficulty;
            settings.Fast = fast;
            settings.Sanitize();
            musicSource.volume = settings.Music;
            soundSource.volume = settings.Sound;
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(settings));
            settingsDirty = true;
            settingsSaveAt = Time.unscaledTime + 0.5f;
        }

        private void FlushSettings()
        {
            if (!settingsDirty) return;
            PlayerPrefs.Save();
            settingsDirty = false;
        }

        private void TickSettingsSave()
        {
            if (settingsDirty && Time.unscaledTime >= settingsSaveAt)
                FlushSettings();
        }

        private void OnApplicationQuit() => FlushSettings();
        private void OnApplicationPause(bool paused)
        {
            if (paused) FlushSettings();
        }

        private void ApplyWindowSize(int index)
        {
            settings.Resolution = Mathf.Clamp(index, 0, WindowSizes.Length - 1);
            Vector2Int size = WindowSizes[settings.Resolution];
            int width = Screen.currentResolution.width;
            int height = Screen.currentResolution.height;
            while (settings.Resolution > 0 &&
                   (size.x > width || size.y > height - 64))
                size = WindowSizes[--settings.Resolution];
            if (!Application.isEditor)
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }

        private void ChangeLanguage(int index)
        {
            settings.Language = index;
            settings.Sanitize();
            SaveSettings();
            Refresh();
        }

        private void ToggleSettings()
        {
            if (dialog == "settings")
            {
                dialog = previousDialog;
                previousDialog = "";
            }
            else
            {
                previousDialog = dialog;
                dialog = "settings";
                settingsTab = 0;
            }
            page = 0;
            RefreshModal();
        }

        private void RestartWithSeed(int seed)
        {
            dialog = feedback = "";
            page = 0;
            targets.Clear();
            top.Clear();
            bottom.Clear();
            bootstrap.Restart(seed);
            Attach(bootstrap.Engine);
            hand.ResetSelection();
            Refresh();
        }

        private void DrawSettings()
        {
            modalTitle.text = F("设置 · v{0}", "設定 · v{0}", "Settings · v{0}", Application.version);
            UiButton("常规", modalContent, new Vector2(-330, 204), new Vector2(180, 43),
                settingsTab == 0 ? "Green" : "Grey", () =>
                { settingsTab = 0; RefreshModal(); });
            UiButton("对局", modalContent, new Vector2(-130, 204), new Vector2(180, 43),
                settingsTab == 1 ? "Green" : "Grey", () =>
                { settingsTab = 1; RefreshModal(); });
            if (settingsTab == 0) DrawGeneralSettings();
            else DrawMatchSettings();
            UiButton("回到牌桌", modalContent, new Vector2(380, -215),
                new Vector2(210, 42), "Green", () =>
                { dialog = previousDialog; previousDialog = ""; RefreshModal(); });
        }

        private void DrawGeneralSettings()
        {
            Label("Language", modalContent, new Vector2(-370, 125),
                new Vector2(220, 40), 23, TextAnchor.MiddleLeft, Color.black).text = L("语言");
            string[] languages = { "中文", "日本語", "English" };
            for (int i = 0; i < languages.Length; i++)
            {
                int index = i;
                UiButton(languages[i], modalContent, new Vector2(-145 + i * 220, 125),
                    new Vector2(200, 44),
                    settings.Language == i ? "Green" : "Grey",
                    () => ChangeLanguage(index));
            }

            Label("Resolution", modalContent, new Vector2(-370, 50),
                new Vector2(220, 40), 22, TextAnchor.MiddleLeft, Color.black).text =
                L("窗口分辨率");
            var available = Enumerable.Range(0, WindowSizes.Length)
                .Where(i => i == 0 || (WindowSizes[i].x <= Screen.currentResolution.width &&
                    WindowSizes[i].y <= Screen.currentResolution.height - 64)).ToArray();
            for (int j = 0; j < available.Length; j++)
            {
                int index = available[j];
                Vector2Int size = WindowSizes[index];
                float x = -145 + j * (660f / Math.Max(1, available.Length));
                UiButton(size.x + "×" + size.y, modalContent,
                    new Vector2(x, 50), new Vector2(640f / available.Length, 42),
                    settings.Resolution == index ? "Green" : "Grey", () =>
                    { ApplyWindowSize(index); SaveSettings(); RefreshModal(); });
            }

            Label("Music", modalContent, new Vector2(-370, -34),
                new Vector2(220, 40), 22, TextAnchor.MiddleLeft, Color.black).text =
                L("音乐音量");
            VolumeSlider(new Vector2(80, -34), settings.Music, true);
            Label("Sound", modalContent, new Vector2(-370, -113),
                new Vector2(220, 40), 22, TextAnchor.MiddleLeft, Color.black).text =
                L("音效音量");
            VolumeSlider(new Vector2(80, -113), settings.Sound, false);
            Label("License", modalContent, new Vector2(-110, -180),
                new Vector2(760, 32), 17, TextAnchor.MiddleLeft, Color.black).text =
                "Kenney · CC0   /   Noto Sans CJK · SIL OFL";
        }

        private void VolumeSlider(Vector2 position, float value, bool music)
        {
            RectTransform rect = Panel(music ? "Music slider" : "Sound slider",
                modalContent, new Vector2(0.5f, 0.5f), position,
                new Vector2(570, 34), new Color(0.78f, 0.83f, 0.77f));
            RectTransform fill = Panel("Fill", rect, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.29f, 0.62f, 0.40f));
            // Slider changes the fill's right anchor as its value changes.
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(0, 8);
            fill.offsetMax = new Vector2(0, -8);
            RectTransform handle = Panel("Handle", rect, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(22, 42), new Color(0.98f, 0.83f, 0.27f));
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            Text percent = Label("Percent", modalContent,
                position + new Vector2(365, 0), new Vector2(85, 42), 22,
                TextAnchor.MiddleCenter, Color.black);
            percent.text = Mathf.RoundToInt(value * 100) + "%";
            slider.onValueChanged.AddListener(next =>
            {
                if (music) settings.Music = next;
                else settings.Sound = next;
                percent.text = Mathf.RoundToInt(next * 100) + "%";
                SaveSettings();
            });
        }

        private void DrawMatchSettings()
        {
            Label("Difficulty", modalContent, new Vector2(-370, 120),
                new Vector2(220, 40), 23, TextAnchor.MiddleLeft, Color.black).text =
                L("AI强度");
            UiButton("简单", modalContent, new Vector2(-130, 120),
                new Vector2(210, 45), difficulty == Difficulty.Simple ? "Green" : "Grey",
                () => { difficulty = Difficulty.Simple; SaveSettings(); RefreshModal(); });
            UiButton("标准", modalContent, new Vector2(135, 120),
                new Vector2(210, 45), difficulty == Difficulty.Normal ? "Green" : "Grey",
                () => { difficulty = Difficulty.Normal; SaveSettings(); RefreshModal(); });

            Label("Speed", modalContent, new Vector2(-370, 40),
                new Vector2(220, 40), 22, TextAnchor.MiddleLeft, Color.black).text =
                F("AI速度", "AI速度", "AI speed");
            UiButton(fast ? "AI速度：快速" : "AI速度：正常",
                modalContent, new Vector2(40, 40), new Vector2(335, 43), "Blue",
                () => { fast = !fast; SaveSettings(); RefreshModal(); });

            UiButton("新开一局", modalContent, new Vector2(25, -45),
                new Vector2(300, 48), "Green", Restart);
            UiButton("规则手册", modalContent, new Vector2(25, -118),
                new Vector2(300, 43), "Grey", () => Open("rulebook"));
            UiButton("对战日志", modalContent, new Vector2(-180, -190),
                new Vector2(240, 42), "Grey", () => Open("log"));
            if (!Application.isEditor)
                UiButton("退出游戏", modalContent, new Vector2(90, -190),
                    new Vector2(240, 42), "Red", Application.Quit);
        }

        private void DrawRulebook()
        {
            modalTitle.text = L("规则手册");
            UiButton("规则", modalContent, new Vector2(-360, 200),
                new Vector2(170, 42), helpTab == 0 ? "Green" : "Grey",
                () => { helpTab = 0; RefreshModal(); });
            UiButton("牌效", modalContent, new Vector2(-170, 200),
                new Vector2(170, 42), helpTab == 1 ? "Green" : "Grey",
                () => { helpTab = 1; RefreshModal(); });
            string body = helpTab == 0 ? Rulebook.Text(CurrentLanguage) :
                string.Join("\n\n", Enum.GetValues(typeof(EffectId))
                    .Cast<EffectId>().Select(effect =>
                        L(EffectCatalog.Name(effect)) + "\n" +
                        L(EffectCatalog.Description(effect))));
            ScrollText(body, new Vector2(0, -10), new Vector2(960, 355));
            UiButton("返回设置", modalContent, new Vector2(0, -215),
                new Vector2(230, 42), "Green", () =>
                { previousDialog = ""; Open("settings"); });
        }

        private void ScrollText(string body, Vector2 position, Vector2 size)
        {
            RectTransform viewport = Panel("Text viewport", modalContent,
                new Vector2(0.5f, 0.5f), position, size,
                new Color(0.95f, 0.96f, 0.92f));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Text content", viewport,
                new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, size);
            content.pivot = new Vector2(0, 1);
            content.anchoredPosition = new Vector2(14, -10);
            Text text = Label("Body", content, Vector2.zero,
                new Vector2(size.x - 30, size.y), 20,
                TextAnchor.UpperLeft, Color.black);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = body;
            float height = Mathf.Max(size.y - 20, text.preferredHeight + 10);
            content.sizeDelta = new Vector2(size.x - 20, height);
            text.rectTransform.anchorMin = new Vector2(0, 1);
            text.rectTransform.anchorMax = new Vector2(0, 1);
            text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = new Vector2(size.x - 30, height);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
        }
    }
}
