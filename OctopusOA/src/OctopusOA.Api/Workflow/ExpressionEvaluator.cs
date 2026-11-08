using System.Globalization;
using System.Text.Json;

namespace OctopusOA.Api.Workflow;

/// <summary>
/// 轻量条件表达式求值器：比较(== != &lt; &lt;= &gt; &gt;=)、逻辑(&amp;&amp; || !)、括号、
/// 字段引用、数字/字符串/布尔字面量。字段来自流程表单与变量合并后的字典。
/// </summary>
public static class ExpressionEvaluator
{
    public static bool Evaluate(string? expression, IDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        var tokens = Tokenize(expression);
        var parser = new Parser(tokens, data);
        var result = parser.ParseOr();
        if (!parser.AtEnd)
            throw new ArgumentException($"表达式存在多余内容: '{expression}'");
        return result;
    }

    private enum TokenType { Number, String, Bool, Null, Identifier, And, Or, Not, Eq, Ne, Le, Ge, Lt, Gt, LParen, RParen, End }

    private readonly record struct Token(TokenType Type, string Text);

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                tokens.Add(new Token(TokenType.Number, s[start..i]));
                continue;
            }

            if (c == '"' || c == '\'')
            {
                char quote = c; i++;
                int start = i;
                while (i < s.Length && s[i] != quote) i++;
                var text = s[start..i];
                if (i < s.Length) i++;
                tokens.Add(new Token(TokenType.String, text));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                var word = s[start..i];
                tokens.Add(word switch
                {
                    "true" => new Token(TokenType.Bool, word),
                    "false" => new Token(TokenType.Bool, word),
                    "null" => new Token(TokenType.Null, word),
                    _ => new Token(TokenType.Identifier, word),
                });
                continue;
            }

            if (c == '&' && i + 1 < s.Length && s[i + 1] == '&') { tokens.Add(new Token(TokenType.And, "&&")); i += 2; continue; }
            if (c == '|' && i + 1 < s.Length && s[i + 1] == '|') { tokens.Add(new Token(TokenType.Or, "||")); i += 2; continue; }
            if (c == '!' && (i + 1 >= s.Length || s[i + 1] != '=')) { tokens.Add(new Token(TokenType.Not, "!")); i++; continue; }
            if (c == '=' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token(TokenType.Eq, "==")); i += 2; continue; }
            if (c == '!' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token(TokenType.Ne, "!=")); i += 2; continue; }
            if (c == '<' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token(TokenType.Le, "<=")); i += 2; continue; }
            if (c == '>' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token(TokenType.Ge, ">=")); i += 2; continue; }
            if (c == '<') { tokens.Add(new Token(TokenType.Lt, "<")); i++; continue; }
            if (c == '>') { tokens.Add(new Token(TokenType.Gt, ">")); i++; continue; }
            if (c == '(') { tokens.Add(new Token(TokenType.LParen, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new Token(TokenType.RParen, ")")); i++; continue; }

            throw new ArgumentException($"无法识别的字符: '{c}'");
        }
        tokens.Add(new Token(TokenType.End, ""));
        return tokens;
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private readonly IDictionary<string, object?> _data;
        private int _pos;

        public Parser(List<Token> tokens, IDictionary<string, object?> data)
        {
            _tokens = tokens;
            _data = data;
        }

        private Token Cur => _tokens[_pos];
        public bool AtEnd => Cur.Type == TokenType.End;

        public bool ParseOr()
        {
            bool v = ParseAnd();
            while (Cur.Type == TokenType.Or) { _pos++; bool r = ParseAnd(); v = v || r; }
            return v;
        }

        private bool ParseAnd()
        {
            bool v = ParseUnary();
            while (Cur.Type == TokenType.And) { _pos++; bool r = ParseUnary(); v = v && r; }
            return v;
        }

        private bool ParseUnary()
        {
            if (Cur.Type == TokenType.Not) { _pos++; return !ParseUnary(); }
            return ParsePrimary();
        }

        private bool ParsePrimary()
        {
            if (Cur.Type == TokenType.LParen)
            {
                _pos++;
                bool v = ParseOr();
                Expect(TokenType.RParen);
                return v;
            }
            return ParseComparison();
        }

        private bool ParseComparison()
        {
            object? left = ParseOperand();
            var op = Cur.Type;
            if (op is TokenType.Eq or TokenType.Ne or TokenType.Le or TokenType.Ge or TokenType.Lt or TokenType.Gt)
            {
                _pos++;
                object? right = ParseOperand();
                return Compare(op, left, right);
            }
            return Truthy(left);
        }

        private object? ParseOperand()
        {
            var t = Cur;
            if (t.Type == TokenType.Number) { _pos++; return decimal.Parse(t.Text, CultureInfo.InvariantCulture); }
            if (t.Type == TokenType.String) { _pos++; return t.Text; }
            if (t.Type == TokenType.Bool) { _pos++; return t.Text == "true"; }
            if (t.Type == TokenType.Null) { _pos++; return null; }
            if (t.Type == TokenType.Identifier) { _pos++; return _data.TryGetValue(t.Text, out var v) ? v : null; }
            throw new ArgumentException($"意外的 token: '{t.Text}'");
        }

        private void Expect(TokenType type)
        {
            if (Cur.Type != type) throw new ArgumentException($"期望 {type}");
            _pos++;
        }

        private static bool Truthy(object? v) => v switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrEmpty(s),
            _ => TryNumber(v, out var d) && d != 0,
        };

        private static bool Compare(TokenType op, object? l, object? r)
        {
            if (l == null || r == null)
                return op switch
                {
                    TokenType.Eq => l == null && r == null,
                    TokenType.Ne => !(l == null && r == null),
                    _ => false,
                };

            if (TryNumber(l, out var ln) && TryNumber(r, out var rn))
                return op switch
                {
                    TokenType.Eq => ln == rn,
                    TokenType.Ne => ln != rn,
                    TokenType.Le => ln <= rn,
                    TokenType.Ge => ln >= rn,
                    TokenType.Lt => ln < rn,
                    TokenType.Gt => ln > rn,
                    _ => false,
                };

            var ls = l is JsonElement le && le.ValueKind == JsonValueKind.String ? le.GetString() ?? "" : l.ToString() ?? "";
            var rs = r is JsonElement re && re.ValueKind == JsonValueKind.String ? re.GetString() ?? "" : r.ToString() ?? "";
            return op switch
            {
                TokenType.Eq => ls == rs,
                TokenType.Ne => ls != rs,
                TokenType.Le => string.CompareOrdinal(ls, rs) <= 0,
                TokenType.Ge => string.CompareOrdinal(ls, rs) >= 0,
                TokenType.Lt => string.CompareOrdinal(ls, rs) < 0,
                TokenType.Gt => string.CompareOrdinal(ls, rs) > 0,
                _ => false,
            };
        }

        private static bool TryNumber(object? v, out decimal d)
        {
            d = 0;
            if (v is null) return false;
            if (v is decimal dec) { d = dec; return true; }
            if (v is int i) { d = i; return true; }
            if (v is long lg) { d = lg; return true; }
            if (v is short sh) { d = sh; return true; }
            if (v is double db) { d = (decimal)db; return true; }
            if (v is float fl) { d = (decimal)fl; return true; }
            if (v is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number) { d = je.GetDecimal(); return true; }
                if (je.ValueKind == JsonValueKind.String && decimal.TryParse(je.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return true;
                return false;
            }
            return decimal.TryParse(v.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out d);
        }
    }
}
