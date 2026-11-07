using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CardDuel.Presentation
{
    public enum Language { Chinese, Japanese, English }

    // Canonical engine logs remain unchanged for seed replays. Only their displayed text is localized.
    public static class Localization
    {
        sealed class Row
        {
            public readonly string Zh, Ja, En;
            public readonly Regex Pattern;
            public Row(string zh,string ja,string en)
            {
                Zh=zh;Ja=ja;En=en;
                if(zh.Contains("{0}"))
                    Pattern=new Regex("^"+Regex.Replace(Regex.Escape(zh),@"\\\{\d+}","(.*?)")+"$",RegexOptions.Singleline|RegexOptions.CultureInvariant);
            }
        }
        static readonly Row[] Rows={
            new Row("设置","設定","Settings"),new Row("常规","通常","General"),new Row("对局","対戦","Match"),
            new Row("语言","言語","Language"),new Row("窗口分辨率","ウィンドウ解像度","Window size"),
            new Row("音乐音量","BGM音量","Music volume"),new Row("音效音量","効果音音量","Sound volume"),
            new Row("AI强度","AIの強さ","AI difficulty"),new Row("简单","かんたん","Easy"),new Row("标准","標準","Normal"),
            new Row("随机种子","シード値","Seed"),new Row("以此种子重开","このシードで再開","Restart with seed"),new Row("新种子重开","新しいシードで再開","New match"),
            new Row("AI速度：快速","AI速度：速い","AI speed: fast"),new Row("AI速度：正常","AI速度：通常","AI speed: normal"),
            new Row("回到牌桌","対戦に戻る","Back to game"),new Row("对战日志","対戦履歴","Battle history"),new Row("退出游戏","ゲーム終了","Quit game"),
            new Row("返回设置","設定に戻る","Back to settings"),new Row("设置与对战日志","設定と対戦履歴","Settings and history"),
            new Row("玩法与效果图例","ルールとカード効果","Rules and card effects"),new Row("规则手册","ルールブック","Rulebook"),
            new Row("规则","ルール","Rules"),new Row("牌效","カード効果","Card effects"),
            new Row("左键长按效果牌查看说明","効果カードを左長押しで確認","Hold left mouse on a card to read its effect"),
            new Row("弃牌堆","捨て札","Discard"),new Row("抽牌堆","山札","Deck"),
            new Row("消耗 1 点生命额外抽 4 张？","HPを1払って、追加で4枚引く？","Pay 1 HP to draw 4 extra cards?"),
            new Row("是","はい","Yes"),new Row("否","いいえ","No"),new Row("提示","ヒント","Hint"),new Row("出牌","出す","Play"),new Row("要不起","パス","Pass"),
            new Row("暂无提示","候補がありません","No hint available"),new Row("改点数","数字をコピー","Copy rank"),new Row("牌前效果","出す前の効果","Before playing"),
            new Row("选择两张","2枚選ぶ","Select two cards"),new Row("点击卡牌，分配至牌顶或牌底","カードを選び、山札の上か下に置く","Click cards to assign them to the top or bottom"),
            new Row("换两张手牌","手札を2枚交換","Swap two cards"),new Row("看对手两张牌","相手の手札を2枚見る","Peek at two cards"),new Row("牌顶与牌底排序","山札の上下を並べ替える","Reorder top and bottom"),
            new Row("放到牌顶","山札の上へ","To top"),new Row("● 放到牌顶","● 山札の上へ","● To top"),new Row("放到牌底","山札の下へ","To bottom"),new Row("● 放到牌底","● 山札の下へ","● To bottom"),
            new Row("清空顺序","選び直す","Clear order"),new Row("确认排序","順序を確定","Confirm order"),new Row("取消，返回候选","戻って選び直す","Cancel"),
            new Row("先设定效果，再回到牌桌确认出牌","効果を設定してから、卓上で出す","Choose the effect, then confirm your play on the table"),
            new Row("不发动","使わない","Skip effect"),new Row("返回","戻る","Back"),new Row("选择一个效果","効果を1つ選ぶ","Choose one effect"),
            new Row("对手的两张牌","相手の手札2枚","Two opponent cards"),new Row("记住了，继续","確認して続ける","Continue"),
            new Row("猜拳","じゃんけん","Rock, paper, scissors"),new Row("石头","グー","Rock"),new Row("剪刀","チョキ","Scissors"),new Row("布","パー","Paper"),new Row("平局","あいこ","Tie"),
            new Row("你赢了！","あなたの勝ち！","You win!"),new Row("敌人赢了","相手の勝ち","Opponent wins"),new Row("再来一局","もう一度","Play again"),
            new Row("开局","開始","Opening"),new Row("你","あなた","You"),new Row("对手","相手","Opponent"),new Row("系统","システム","System"),
            new Row("小王","小J","SJ"),new Row("大王","大J","BJ"),
            new Row("白板","効果なし","No effect"),new Row("复制点数","数字コピー","Copy rank"),new Row("随机弃敌牌","相手の手札を捨てる","Random discard"),new Row("降低上手","直前の数字を下げる","Lower previous play"),
            new Row("王除外","ジョーカー除外","Exile Joker"),new Row("抽两张","2枚ドロー","Draw two"),new Row("本牌回手","このカードを手札へ","Return this card"),new Row("洗回弃牌","捨て札を山札へ","Recycle discard"),
            new Row("看敌两张","相手の手札を2枚見る","Peek at two"),new Row("换手牌两张","手札を2枚交換","Swap two"),new Row("牌顶排序","山札操作","Reorder deck"),
            new Row("无牌效","カード効果なし","No card effect"),
            new Row("PRE：本牌临时复制另一张所选牌的印刷点数","出す前：選んだ別のカードの元の数字を、この1回だけコピーする。","Before play: copy another selected card's printed rank for this play only."),
            new Row("PRE：合法出牌后，敌方随机弃1张；不触发其牌效","出す前：出せることを確認後、相手の手札をランダムに1枚捨てる。その効果は使わない。","Before play: after validation, discard one random opponent card without triggering its effect."),
            new Row("PRE：上一手每个逻辑点数降一级；含3或王不可用","出す前：直前の組み合わせの数字を全て1段階下げる。3かジョーカーを含む場合は使えない。","Before play: lower every rank in the previous play by one step. Cannot affect a 3 or Joker."),
            new Row("POST：仅该效果的王移出本局","出した後：この効果を持つジョーカーだけを、この対戦から除外する。","After play: remove only this effect's Joker from the match."),
            new Row("POST：抽最多2张，牌库不足先回收弃牌","出した後：最大2枚引く。山札が足りなければ捨て札を戻す。","After play: draw up to two cards; recycle discard if the deck runs out."),
            new Row("DISCARD：本牌回手；每位玩家全局合计最多3次","捨てた後：このカードを手札に戻す。各プレイヤー、1対戦で合計3回まで。","After discard: return this card to your hand. Each player may do this three times per match."),
            new Row("DISCARD：全部当前弃牌洗回牌库","捨てた後：現在の捨て札を全て山札に戻し、シャッフルする。","After discard: shuffle all current discard cards into the deck."),
            new Row("DISCARD：人类选2个敌方背面位置查看；AI不可用","捨てた後：相手の裏向きの手札を2枚選んで見る。人間専用で、AIは使えない。","After discard: choose two face-down opponent cards to inspect. Human only; AI cannot use this."),
            new Row("DISCARD：选2张手牌洗入牌库再抽2张","捨てた後：手札を2枚選んで山札に戻し、シャッフルしてから2枚引く。","After discard: shuffle two selected hand cards into the deck, then draw two."),
            new Row("DISCARD：看牌顶最多5张，按点击顺序分配顶/底；不额外回收弃牌","捨てた後：山札の上から最大5枚を見て、選んだ順に上か下へ置く。捨て札は戻さない。","After discard: inspect up to five top cards and assign them to top/bottom in click order. Does not recycle discard."),
            new Row("单张","シングル","Single"),new Row("对子","ペア","Pair"),new Row("三张","トリプル","Triple"),new Row("三带一","トリプル＋1枚","Triple + single"),new Row("三带一对","トリプル＋ペア","Triple + pair"),
            new Row("顺子","ストレート","Straight"),new Row("连对","連続ペア","Consecutive pairs"),new Row("四带二单","4枚組＋2枚","Four + two singles"),new Row("四带两对","4枚組＋2ペア","Four + two pairs"),new Row("炸弹","ボム","Bomb"),new Row("王炸","ロケット","Rocket"),
            new Row("初始发牌","初期配布","Initial deal"),new Row("伤害补偿","ダメージ後","After damage"),new Row("HP购买","HP支払い","HP purchase"),new Row("卡牌效果","カード効果","Card effect"),new Row("卡牌效果换牌","手札交換","Card swap"),
            new Row("弃牌效果","捨て札効果","Discard effect"),new Row("抽牌不足自动回收","山札切れ","Deck exhausted"),new Row("低手牌事件","手札補充","Low hand refill"),
            new Row("猜拳获胜，先手。","じゃんけんに勝ち、先手。","Won RPS; plays first."),new Row("出牌回复：生命已满","回復：HPは最大","Play recovery: HP already full"),new Row("查看对手 2 张手牌","相手の手札を2枚確認","Inspected two opponent cards"),
            new Row("低手牌补牌，洗切牌库","手札補充：山札をシャッフル","Low hand refill: shuffled deck"),new Row("获胜。","勝利。","Wins."),new Row("2 张牌","カード2枚","Two cards"),
            new Row("Seed需要32位整数。","シードは32ビット整数で入力してください。","Seed must be a 32-bit integer."),
            new Row("当前不能猜拳。","今はじゃんけんできません。","RPS is not available now."),new Row("仅非跳过的抽牌阶段、HP足够且本行动未购买时可用。","スキップ以外のドロー段階で、HPが足り、まだ購入していない時のみ使えます。","Buy once per draw phase, with enough HP and no skipped first draw."),
            new Row("不在你的抽牌阶段。","あなたのドロー段階ではありません。","It is not your draw phase."),new Row("生命不足或当前不能购买。","HP不足、または今は購入できません。","Not enough HP, or purchase unavailable."),new Row("不在你的选牌阶段。","あなたのカード選択段階ではありません。","It is not your selection phase."),
            new Row("请先选择手牌。","手札を選んでください。","Select cards first."),new Row("选牌含重复或不属于你的实体牌。","同じカード、または自分の手札以外が選ばれています。","Selected cards are duplicated or not in your hand."),new Row("PRE效果必须来自本次选牌。","出す前の効果は、今回選んだカードから選びます。","The before-play effect must come from the selected cards."),new Row("所选牌没有PRE效果。","このカードには出す前の効果がありません。","This card has no before-play effect."),
            new Row("复制目标必须是另一张已选牌。","コピー先は、選択中の別のカードにしてください。","Copy another selected card."),new Row("上一手含3/王、没有上一手或无法维持牌型，不能降低。","直前の組み合わせがないか、3・ジョーカーを含むか、形を保てないため下げられません。","Cannot lower: no previous play, contains 3/Joker, or invalid resulting shape."),new Row("对手没有可随机弃掉的手牌。","相手に捨てられる手札がありません。","Opponent has no card to discard."),
            new Row("牌型不成立","組み合わせが無効です","Invalid combination"),new Row("所选牌不是指定牌型","指定の組み合わせではありません","Not the selected combination type"),new Row("王炸不能被压过","ロケットには勝てません","A Rocket cannot be beaten"),new Row("牌型不同，不能压制","同じ組み合わせが必要です","Combination types must match"),new Row("长度不同，不能压制","枚数が一致していません","Card counts must match"),new Row("点数必须比上一手更大","直前より大きい数字が必要です","Main rank must be strictly higher"),
            new Row("只有回应上一手时可以Pass。","相手の組み合わせに応じる時のみパスできます。","Pass is only available when responding."),new Row("效果来源不在本阶段、本次候选中。","この段階では選べない効果です。","Effect is not among this stage's candidates."),new Row("只能除外王。","除外できるのはジョーカーだけです。","Only a Joker may be exiled."),new Row("本局回手次数已用完。","この対戦の手札戻し回数を使い切りました。","Return limit reached for this match."),
            new Row("仅人类玩家可以发动。","人間プレイヤー専用です。","Only the human player may use this."),new Row("对手不足2张。","相手の手札が2枚未満です。","Opponent has fewer than two cards."),new Row("本方手牌不足2张。","手札が2枚未満です。","You have fewer than two cards."),new Row("不在可选择牌效的阶段。","効果を選べる段階ではありません。","Not in an effect selection stage."),new Row("缺少效果选择。","効果を選んでください。","Missing effect selection."),
            new Row("换牌必须选择本方2张不同手牌。","自分の異なる手札を2枚選んでください。","Select two different cards from your hand."),new Row("请选择2个不同敌方背面位置。","相手の裏向きの手札を異なる位置から2枚選んでください。","Select two different face-down opponent slots."),new Row("排序必须完整且仅包含当前牌顶预览中的每张牌一次。","表示中のカードを、重複なく全て割り当ててください。","Assign every previewed card exactly once."),new Row("AI当前不能行动。","AIは今行動できません。","AI cannot act now."),
            new Row("设置 · v{0}","設定 · v{0}","Settings · v{0}"),new Row("对手 · {0}张","相手 · {0}枚","Opponent · {0} cards"),new Row("回合 {0}","ターン {0}","Turn {0}"),new Row("原 {0}","元 {0}","Was {0}"),
            new Row("牌顶（先抽 → 后抽）：{0}","山札の上（先 → 後）：{0}","Top (draw first → last): {0}"),new Row("牌底（靠前 → 最底）：{0}","山札の下（上 → 下）：{0}","Bottom (near → far): {0}"),
            new Row("确认（{0}/2）","確定（{0}/2）","Confirm ({0}/2)"),new Row("{0} 变为：","{0} を変更：","Change {0} to:"),
            new Row("打出 {0}\n{1}","{0} を出した\n{1}","Played {0}\n{1}"),new Row("{0} · {1}张 · {2}","{0} · {1}枚 · {2}","{0} · {1} cards · {2}"),
            new Row("{0} 发动【{1}】","{0}：{1} を発動","{0}: activated {1}"),new Row("{0} 本次视为 {1}","{0} → 今回は {1}","{0} counts as {1} this play"),new Row("降低上手：{0} → {1}","直前の数字：{0} → {1}","Previous ranks: {0} → {1}"),
            new Row("{0}：手牌 → 弃牌堆（被迫弃牌，不发动效果）","{0}：手札 → 捨て札（強制・効果なし）","{0}: hand → discard (forced; no effect)"),
            new Row("出牌回复 +{0} HP（{1}/{2}）","出した枚数で回復 +{0} HP（{1}/{2}）","Play recovery +{0} HP ({1}/{2})"),new Row("支付 {0} HP（{1}/{2}）","{0} HP支払い（{1}/{2}）","Paid {0} HP ({1}/{2})"),new Row("要不起，−{0} HP（{1}/{2}）","パス、−{0} HP（{1}/{2}）","Passed, −{0} HP ({1}/{2})"),
            new Row("{0} → 除外（移出本局）","{0} → 除外（対戦から取り除く）","{0} → exile (removed from match)"),new Row("{0}：弃牌堆 → 手牌","{0}：捨て札 → 手札","{0}: discard → hand"),new Row("{0}：手牌 → 牌库（洗切）","{0}：手札 → 山札（シャッフル）","{0}: hand → deck (shuffled)"),
            new Row("牌库 → 牌顶 {0} 张 / 牌底 {1} 张","山札 → 上に{0}枚 / 下に{1}枚","Deck → {0} top / {1} bottom"),new Row("{0} → 弃牌堆","{0} → 捨て札","{0} → discard"),
            new Row("牌库 → 手牌 {0} 张（{1}）","山札 → 手札 {0}枚（{1}）","Deck → hand: {0} cards ({1})"),new Row("{0}\n弃牌堆 → 牌库（{1}，洗切）","{0}\n捨て札 → 山札（{1}・シャッフル）","{0}\nDiscard → deck ({1}; shuffled)"),
            new Row("{0}\n弃牌堆 → 牌库；低手牌补牌，洗切牌库","{0}\n捨て札 → 山札・手札補充のためシャッフル","{0}\nDiscard → deck; low hand refill, shuffled")
        };
        static readonly Dictionary<string,Row> Exact=Rows.Where(r=>r.Pattern==null).ToDictionary(r=>r.Zh,StringComparer.Ordinal);
        static readonly Row[] Patterns=Rows.Where(r=>r.Pattern!=null).ToArray();
        public static IEnumerable<string> Sources => Rows.Select(r=>r.Zh);
        public static string Text(string source,Language language)
        {
            if(string.IsNullOrEmpty(source)||language==Language.Chinese)return source;
            if(Exact.TryGetValue(source,out var exact))return language==Language.Japanese?exact.Ja:exact.En;
            foreach(var row in Patterns)
            {
                Match match=row.Pattern.Match(source);if(!match.Success)continue;
                object[] values=match.Groups.Cast<Group>().Skip(1).Select(g=>(object)Text(g.Value,language)).ToArray();
                return string.Format(CultureInfo.InvariantCulture,language==Language.Japanese?row.Ja:row.En,values);
            }
            // Rank strings and public card lists have no language-dependent grammar.
            return source.Replace("小王",language==Language.Japanese?"小J":"SJ").Replace("大王",language==Language.Japanese?"大J":"BJ");
        }
        public static string EffectName(EffectId effect,Language language) => Text(EffectCatalog.Name(effect),language);
        public static string EffectDescription(EffectId effect,Language language) => Text(EffectCatalog.Description(effect),language);
    }
}
