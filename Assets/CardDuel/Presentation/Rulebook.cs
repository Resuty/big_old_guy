namespace CardDuel.Presentation
{
    public static class Rulebook
    {
        public static string Text(Language language)
        {
            if(language==Language.Japanese)return @"遊び方
手札をクリックして選び、「出す」で確定。「ヒント」は候補を選ぶだけです。出せない時は「パス」。効果カードを左長押しすると説明が出ます。右下のLOGでカードの移動と効果を確認できます。Escで設定を開閉します。

勝敗とドロー
各8枚、HP3で開始。じゃんけんの勝者が先手で、最初のドローはありません。以降は通常1枚引きます。ドロー段階の「はい」はHPを1払い、無料分に加えて4枚引きます。「いいえ」は無料分だけです。HP0でも続行し、HPが0未満になると負けます。手札を出し切るだけでは勝ちません。

組み合わせ
シングル、ペア、トリプル、トリプル＋1枚、トリプル＋ペア、ストレート（5枚以上）、連続ペア（3組以上）、4枚組＋2枚、4枚組＋2ペア、ボム、ロケット。飛行機はありません。4枚組＋2枚の追加2枚はペアでも可。4枚組＋2ペアは異なる2ペアが必要です。

出せる条件
数字は3 < 4 < … < K < A < 2 < 小ジョーカー < 大ジョーカー。ストレートと連続ペアは3〜Aのみ。通常は同じ形・同じ枚数で、中心となる数字が厳密に大きい必要があります。同じ10で10には勝てません。追加カードの数字とスートは比較しません。ボムは通常の形に勝ち、より大きいボムが優先。ロケット（大小ジョーカー）は全てに勝ちます。

パスと次のターン
相手に応じる時だけパスでき、HPを1失います。直前の組み合わせが消え、ダメージを受けた側が1枚引いて自由に出します。次に前の勝者が3枚引き、次に受けた側が3枚引き、その後は通常1枚に戻ります。途中で再びダメージが出たら、この流れを新しく始めます。

出した枚数で回復
累計10枚出すごとにHPを1回復（最大3）。余りは持ち越し、最大HPでも10枚分は消費します。ハート横の数字は次の回復までの枚数です。戻したカードや除外でも出した枚数は減りません。

カード効果
黄色は出す前、青は出した後、緑は捨てた後。各段階で1枚まで。複数候補は選択し、使わなくても構いません。候補が0〜1枚なら自動で進みますが、コピー先などの指定は必要です。数字コピーは出す前に指定し、変更後の組み合わせで判定します。例：5667の5を6にすれば6667。印刷された数字は変わりません。手札戻しは各プレイヤー合計3回まで。詳細は「カード効果」タブへ。

山札・捨て札・除外
捨てた後の効果は、今回実際に捨てたカードだけが候補。除外されたカードは他の山に入らず、その対戦では戻りません。山札が切れると捨て札を戻してシャッフル。どちらかの手札が2枚以下になると、捨て札を山札に戻して混ぜ、両者が交互に最大4枚ずつ補充します。この補充は両者が2枚を超えるまで再発動しません。

設定
歯車から言語・解像度・BGM・効果音・AIの強さを変更できます。設定は自動保存。かんたんAIは合法候補からランダムに選び、標準AIは低い数字やボムの温存を優先します。AIは隠れた相手の手札や山札の順序を見ることはできません。";
            if(language==Language.English)return @"Controls
Click hand cards to select them, then Play. Hint only selects a suggested play. Use Pass when you cannot respond. Hold the left mouse button on an effect card to read it. Open LOG at bottom right to review plays, effects and card movements. Esc opens or closes settings.

Winning and drawing
Start with 8 cards and 3 HP each. The RPS winner leads and skips their first draw. Later turns normally draw 1. Yes in the draw phase pays 1 HP to draw 4 extra cards in addition to the free draw; No takes only the free draw. You are still alive at 0 HP and lose below 0. Emptying your hand alone does not win.

Combinations
Single, Pair, Triple, Triple + single, Triple + pair, Straight (5+ cards), Consecutive pairs (3+ pairs), Four + two singles, Four + two pairs, Bomb and Rocket. No airplanes. The two extras in Four + two singles may form a pair. Four + two pairs requires two distinct pairs.

Beating a play
Ranks: 3 < 4 < ... < K < A < 2 < Small Joker < Big Joker. Straights and consecutive pairs use only 3 through A. Normally match both type and card count, with a strictly higher main rank. A 10 cannot beat another 10. Kickers and suits do not decide the winner. A Bomb beats ordinary types; a higher Bomb beats a lower one. Rocket (both Jokers) beats everything.

Passing and damage
Pass is available only when responding, and costs 1 HP. It clears the previous play. The damaged player then draws 1 and freely leads. Next, the previous winner draws 3; then the damaged player draws 3; normal draws of 1 resume afterward. New damage restarts this sequence.

Healing by playing
Every 10 cards played restores 1 HP, capped at 3. Remainders carry over. Reaching the threshold at full HP still consumes it. The number beside your hearts counts down to the next heal. Returned or exiled cards do not undo your played-card count.

Card effects
Yellow: before play. Blue: after play. Green: after discard. At most one card's effect per stage. Choose when there are several candidates, or skip. Zero/one candidate advances automatically; effects needing targets still ask for them. Set Copy rank before playing: the resulting ranks determine both the combination and comparison. Example: copy 5 to 6 in 5667 to play 6667. Printed ranks never change. Each player can return a card to hand at most 3 times per match. See the Card effects tab for all effects.

Deck, discard and exile
After-discard candidates are only cards actually discarded by this action. Exiled cards leave the match and enter no other pile. When the deck runs out, recycle and shuffle discard. If either player ends an action with 2 or fewer hand cards, merge discard into the deck and shuffle; both players alternately draw up to 4 each. This refill cannot trigger again until both hands exceed 2.

Settings
The gear opens language, window size, music, sound and AI settings. Changes save automatically. Easy AI chooses randomly among legal candidates. Normal favors lower ranks and saving Bombs. AI never sees hidden opponent card identities or deck order.";
            return @"操作
点击手牌选取，再点出牌。提示只会选中推荐牌，仍需点出牌确认；无法回应可点要不起。左键长按效果牌查看说明，右下角LOG查看出牌、效果与牌的去向。Esc开关设置。

胜负与抽牌
双方各8张、HP3，猜拳胜者先手，首次跳过抽牌。之后常规免费抽1张。抽牌阶段点「是」支付1HP，在免费抽牌之外额外抽4张；点「否」只抽免费张数。HP0仍存活，HP小于0才落败；出完手牌不会直接获胜。

合法牌型
单张、对子、三张、三带一、三带一对、顺子（至少5张）、连对（至少3对）、四带二单、四带两对、炸弹、王炸。不包含飞机。四带二单的两张可为一对；四带两对必须为两种不同点数的对子。

压牌规则
点数依次为3 < 4 < … < K < A < 2 < 小王 < 大王。顺子与连对只允许3至A。普通牌必须牌型相同、张数相同，主点数严格更大；10不能压10，附带牌和花色不参与大小比较。炸弹压普通牌，更大的炸弹压小炸弹；王炸为大小王，压过所有其他牌。

要不起与受伤后回合
仅回应上一手时能要不起，扣1HP并清空上一手。受伤方随后抽1张并自由出牌；再轮到此前胜者抽3张、出牌；再轮到受伤方抽3张、出牌；之后恢复常规抽1。途中再次发生伤害，则重新建立这一顺序。

出牌回血
每累计打出10张回复1HP，上限3；余数保留，满血时达到门槛仍消耗进度，不存储回血。爱心旁数字为距下次回血还需打出的张数。回手和除外不撤回出牌计数。

牌效阶段
黄色为出牌前、蓝色为出牌后、绿色为进弃牌后。每个阶段最多发动一张，可跳过。候选为0或1张时自动推进，需要指定目标的效果仍要选择。复制点数必须在对抗前指定，用更改后的点数判断牌型与能否压过；如5667中5复制6，按6667三带一处理。原始牌面不变。每位玩家每局回手总共最多3次。全部牌效见「牌效」页。

牌库、弃牌与除外
进弃牌后仅本次实际弃掉的牌可作为效果候选。除外牌移出本局，不进任何其他牌堆，也不会回收。牌库抽空时回收弃牌洗切。一方在完整行动后手牌不超过2张，合并弃牌洗牌后双方交替各补最多4张；双方手牌都恢复到2张以上才可再次触发。

设置
齿轮中可切换三语、窗口分辨率、音乐和音效音量、AI强度，自动保存。简单AI随机选择合法候选；标准AI优先较小点数并保留炸弹。AI只能读取受限观察，不能查看对手隐藏牌或牌库顺序。";
        }
    }
}
