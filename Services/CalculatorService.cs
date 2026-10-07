using System.Globalization;
using SpotlightNative.Models;

namespace SpotlightNative.Services;

public sealed class CalculatorService : ISearchProvider
{
    private static double _answer;
    private readonly string _angleMode;

    public CalculatorService(string angleMode = "deg") =>
        _angleMode = angleMode.Equals("rad", StringComparison.OrdinalIgnoreCase) ? "rad" : "deg";

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!Parser.TryEvaluate(query, _angleMode, _answer, out var value)) return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        _answer = value;
        var text = Format(value);
        return Task.FromResult<IReadOnlyList<SearchResult>>([new SearchResult
        {
            Title = $"= {text}",
            Subtitle = "Calculator",
            Kind = SearchResultKind.Calculator,
            ExecuteAsync = _ => { System.Windows.Clipboard.SetText(text); return Task.FromResult(true); }
        }]);
    }

    private static string Format(double value) =>
        value == 0 ? "0" : value is >= 1e12 or > 0 and < 1e-9 or < 0 and > -1e-9
            ? value.ToString("0.###########e+0", CultureInfo.InvariantCulture)
            : value.ToString("0.############", CultureInfo.InvariantCulture);

    private sealed class Parser
    {
        private readonly string _text;
        private readonly string _angleMode;
        private readonly double _answer;
        private int _position;

        private Parser(string text, string angleMode, double answer) =>
            (_text, _angleMode, _answer) = (text.Replace(",", ""), angleMode, answer);

        public static bool TryEvaluate(string text, string angleMode, double answer, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 120) return false;
            try
            {
                var parser = new Parser(text, angleMode, answer);
                result = parser.ParseExpression();
                parser.SkipWhitespace();
                return parser._position == parser._text.Length && double.IsFinite(result);
            }
            catch (Exception exception) when (exception is FormatException or DivideByZeroException or OverflowException or ArgumentException)
            {
                return false;
            }
        }

        private double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                if (Take('+')) value += ParseTerm();
                else if (Take('-')) value -= ParseTerm();
                else if (TakeWord("of")) value = ParseTerm() * value / 100;
                else return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParsePower();
            while (true)
            {
                if (Take('*')) value *= ParsePower();
                else if (Take('/')) value /= ParsePower();
                else if (Take('%'))
                {
                    if (TakeWord("of")) value = value * ParsePower() / 100;
                    else value %= ParsePower();
                }
                else if (StartsPrimary()) value *= ParsePower();
                else return value;
            }
        }

        private double ParsePower()
        {
            var value = ParseUnary();
            if (Take("**") || Take('^')) value = Math.Pow(value, ParsePower());
            return value;
        }

        private double ParseUnary()
        {
            if (Take('+')) return ParseUnary();
            if (Take('-')) return -ParseUnary();
            var value = ParsePrimary();
            while (Take('!')) value = Factorial(value);
            return value;
        }

        private double ParsePrimary()
        {
            if (Take('('))
            {
                var value = ParseExpression();
                Require(')');
                return value;
            }
            if (char.IsLetter(Current))
            {
                var name = ParseIdentifier().ToLowerInvariant();
                if (name is "pi") return Math.PI;
                if (name is "e") return Math.E;
                if (name is "tau") return Math.Tau;
                if (name is "ans") return _answer;
                if (!Take('(')) throw new FormatException();
                var arguments = new List<double> { ParseExpression() };
                while (Take(',')) arguments.Add(ParseExpression());
                Require(')');
                return Function(name, arguments);
            }
            var start = _position;
            while (char.IsDigit(Current) || Current == '.') _position++;
            if (start == _position) throw new FormatException();
            return double.Parse(_text[start.._position], CultureInfo.InvariantCulture);
        }

        private double Function(string name, IReadOnlyList<double> args)
        {
            var value = args[0];
            var angle = _angleMode == "deg" ? Math.PI / 180 : 1;
            return name switch
            {
                "sqrt" => Math.Sqrt(value),
                "cbrt" => Math.Cbrt(value),
                "exp" => Math.Exp(value),
                "sin" => Math.Sin(value * angle),
                "cos" => Math.Cos(value * angle),
                "tan" => Math.Tan(value * angle),
                "asin" => Math.Asin(value) / angle,
                "acos" => Math.Acos(value) / angle,
                "atan" => Math.Atan(value) / angle,
                "atan2" => Math.Atan2(args[0], args[1]) / angle,
                "sinh" => Math.Sinh(value),
                "cosh" => Math.Cosh(value),
                "tanh" => Math.Tanh(value),
                "sec" => 1 / Math.Cos(value * angle),
                "csc" => 1 / Math.Sin(value * angle),
                "cot" => 1 / Math.Tan(value * angle),
                "log" or "log10" => Math.Log10(value),
                "log2" => Math.Log2(value),
                "ln" => Math.Log(value),
                "abs" => Math.Abs(value),
                "floor" => Math.Floor(value),
                "ceil" => Math.Ceiling(value),
                "round" => args.Count > 1 ? Math.Round(value, (int)args[1]) : Math.Round(value),
                "root" => Math.Pow(args[0], 1 / args[1]),
                "min" => args.Min(),
                "max" => args.Max(),
                "fact" => Factorial(value),
                "ncr" => Choose(args[0], args[1]),
                "npr" => Choose(args[0], args[1]) * Factorial(args[1]),
                "gcd" => Gcd((long)value, (long)args[1]),
                "lcm" => Math.Abs((long)value * (long)args[1]) / Gcd((long)value, (long)args[1]),
                "mod" => args[0] % args[1],
                _ => throw new FormatException()
            };
        }

        private bool StartsPrimary()
        {
            SkipWhitespace();
            return Current == '(' || char.IsDigit(Current) || Current == '.' || char.IsLetter(Current);
        }

        private static double Factorial(double value)
        {
            if (value < 0 || value > 170 || value != Math.Truncate(value)) throw new FormatException();
            var result = 1d;
            for (var i = 2; i <= value; i++) result *= i;
            return result;
        }

        private static double Choose(double n, double r) => Factorial(n) / (Factorial(r) * Factorial(n - r));
        private static long Gcd(long a, long b) => b == 0 ? Math.Abs(a) : Gcd(b, a % b);
        private string ParseIdentifier()
        {
            var start = _position;
            while (char.IsLetter(Current) || char.IsDigit(Current)) _position++;
            return _text[start.._position];
        }
        private char Current => _position < _text.Length ? _text[_position] : '\0';
        private bool Take(char character) { SkipWhitespace(); if (Current != character) return false; _position++; return true; }
        private bool Take(string token) { SkipWhitespace(); if (!_text.AsSpan(_position).StartsWith(token, StringComparison.Ordinal)) return false; _position += token.Length; return true; }
        private bool TakeWord(string word) => Take(word) && (Current == '\0' || !char.IsLetterOrDigit(Current));
        private void Require(char character) { if (!Take(character)) throw new FormatException(); }
        private void SkipWhitespace() { while (char.IsWhiteSpace(Current)) _position++; }
    }
}
