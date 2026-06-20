using System;
using System.Collections.Generic;
using System.Text;

namespace fluid_general.Utils
{
    public static class KanaUtils
    {
        private static readonly Dictionary<char, char> HalfToFullMap = new Dictionary<char, char>
        {
            {'ｧ', 'ァ'}, {'ｨ', 'ィ'}, {'ｩ', 'ゥ'}, {'ｪ', 'ェ'}, {'ｫ', 'ォ'},
            {'ｬ', 'ャ'}, {'ｭ', 'ュ'}, {'ｮ', 'ョ'}, {'ｯ', 'ッ'}, {'ｰ', 'ー'},
            {'ｱ', 'ア'}, {'ｲ', 'イ'}, {'ｳ', 'ウ'}, {'ｴ', 'エ'}, {'ｵ', 'オ'},
            {'ｶ', 'カ'}, {'ｷ', 'キ'}, {'ｸ', 'ク'}, {'ｹ', 'ケ'}, {'ｺ', 'コ'},
            {'ｻ', 'サ'}, {'ｼ', 'シ'}, {'ｽ', 'ス'}, {'ｾ', 'セ'}, {'ｿ', 'ソ'},
            {'ﾀ', 'タ'}, {'ﾁ', 'チ'}, {'ﾂ', 'ツ'}, {'ﾃ', 'テ'}, {'ﾄ', 'ト'},
            {'ﾅ', 'ナ'}, {'ﾆ', 'ニ'}, {'ﾇ', 'ヌ'}, {'ﾈ', 'ネ'}, {'ﾉ', 'ノ'},
            {'ﾊ', 'ハ'}, {'ﾋ', 'ヒ'}, {'ﾌ', 'フ'}, {'ﾍ', 'ヘ'}, {'ﾎ', 'ホ'},
            {'ﾏ', 'マ'}, {'ﾐ', 'ミ'}, {'ﾑ', 'ム'}, {'ﾒ', 'メ'}, {'ﾓ', 'モ'},
            {'ﾔ', 'ヤ'}, {'ﾕ', 'ユ'}, {'ﾖ', 'ヨ'},
            {'ﾗ', 'ラ'}, {'ﾘ', 'リ'}, {'ﾙ', 'ル'}, {'ﾚ', 'レ'}, {'ﾛ', 'ロ'},
            {'ﾜ', 'ワ'}, {'ｦ', 'ヲ'}, {'ﾝ', 'ン'},
            // Punctuation
            {'｡', '。'}, {'｢', '「'}, {'｣', '」'}, {'､', '、'}, {'･', '・'}
        };

        private static readonly Dictionary<char, char> HalfToFullDakutenMap = new Dictionary<char, char>
        {
            {'ｳ', 'ヴ'},
            {'ｶ', 'ガ'}, {'ｷ', 'ギ'}, {'ｸ', 'グ'}, {'ｹ', 'ゲ'}, {'ｺ', 'ゴ'},
            {'ｻ', 'ザ'}, {'ｼ', 'ジ'}, {'ｽ', 'ズ'}, {'ｾ', 'ゼ'}, {'ｿ', 'ゾ'},
            {'ﾀ', 'ダ'}, {'ﾁ', 'ヂ'}, {'ﾂ', 'ヅ'}, {'ﾃ', 'デ'}, {'ﾄ', 'ド'},
            {'ﾊ', 'バ'}, {'ﾋ', 'ビ'}, {'ﾌ', 'ブ'}, {'ﾍ', 'ベ'}, {'ﾎ', 'ボ'}
        };

        private static readonly Dictionary<char, char> HalfToFullHandakutenMap = new Dictionary<char, char>
        {
            {'ﾊ', 'パ'}, {'ﾋ', 'ピ'}, {'ﾌ', 'プ'}, {'ﾍ', 'ペ'}, {'ﾎ', 'ポ'}
        };

        public static string ConvertHalfToFullKatakana(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (i + 1 < input.Length)
                {
                    char next = input[i + 1];
                    if (next == 'ﾞ' || next == '\uFF9E')
                    {
                        if (HalfToFullDakutenMap.TryGetValue(c, out char dakutenChar))
                        {
                            sb.Append(dakutenChar);
                            i++; // Skip voicing mark
                            continue;
                        }
                    }
                    else if (next == 'ﾟ' || next == '\uFF9F')
                    {
                        if (HalfToFullHandakutenMap.TryGetValue(c, out char handakutenChar))
                        {
                            sb.Append(handakutenChar);
                            i++; // Skip semi-voicing mark
                            continue;
                        }
                    }
                }
                if (HalfToFullMap.TryGetValue(c, out char fullChar))
                {
                    sb.Append(fullChar);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string KatakanaToHiragana(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= 0x30A1 && c <= 0x30F6)
                {
                    sb.Append((char)(c - 0x60));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string NormalizeToHiragana(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string fullKatakana = ConvertHalfToFullKatakana(input);
            string hiragana = KatakanaToHiragana(fullKatakana);
            return hiragana.ToLowerInvariant();
        }
    }
}
