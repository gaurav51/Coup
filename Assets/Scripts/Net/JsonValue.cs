using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Coup.Net
{
    /// <summary>A small JSON tree. Enough for the Coup socket payloads, with nulls kept intact.</summary>
    public sealed class JsonValue
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind Type;
        public bool Bool;
        public double Number;
        public string String;
        public List<JsonValue> Array;
        public Dictionary<string, JsonValue> Object;

        public static JsonValue Null() => new JsonValue { Type = Kind.Null };

        public static JsonValue FromBool(bool value) => new JsonValue { Type = Kind.Bool, Bool = value };

        public static JsonValue FromNumber(double value) => new JsonValue { Type = Kind.Number, Number = value };

        public static JsonValue FromString(string value) =>
            new JsonValue { Type = Kind.String, String = value ?? "" };

        public static JsonValue FromObject(params (string key, JsonValue value)[] pairs)
        {
            var obj = new Dictionary<string, JsonValue>();
            for (var i = 0; i < pairs.Length; i++) obj[pairs[i].key] = pairs[i].value ?? Null();
            return new JsonValue { Type = Kind.Object, Object = obj };
        }

        public static JsonValue FromArray(List<JsonValue> items) =>
            new JsonValue { Type = Kind.Array, Array = items ?? new List<JsonValue>() };

        public JsonValue Get(string key)
        {
            if (Type != Kind.Object || Object == null) return null;
            return Object.TryGetValue(key, out var value) ? value : null;
        }

        public string Str(string key)
        {
            var value = Get(key);
            return value != null && value.Type == Kind.String ? value.String : null;
        }

        public int Int(string key, int fallback = -1)
        {
            var value = Get(key);
            if (value == null || value.Type != Kind.Number) return fallback;
            return (int)value.Number;
        }

        public bool Flag(string key)
        {
            var value = Get(key);
            return value != null && value.Type == Kind.Bool && value.Bool;
        }

        public List<JsonValue> Arr(string key)
        {
            var value = Get(key);
            return value != null && value.Type == Kind.Array ? value.Array : null;
        }

        public string Encode()
        {
            var builder = new StringBuilder();
            Write(builder);
            return builder.ToString();
        }

        public static JsonValue Parse(string text)
        {
            var parser = new Parser(text ?? "");
            var value = parser.Read();
            parser.Skip();
            if (!parser.Done) throw new FormatException("Unexpected trailing JSON.");
            return value;
        }

        private void Write(StringBuilder builder)
        {
            switch (Type)
            {
                case Kind.Null:
                    builder.Append("null");
                    break;
                case Kind.Bool:
                    builder.Append(Bool ? "true" : "false");
                    break;
                case Kind.Number:
                    builder.Append(Number.ToString(CultureInfo.InvariantCulture));
                    break;
                case Kind.String:
                    builder.Append('"');
                    var text = String ?? "";
                    for (var i = 0; i < text.Length; i++)
                    {
                        var c = text[i];
                        switch (c)
                        {
                            case '"': builder.Append("\\\""); break;
                            case '\\': builder.Append("\\\\"); break;
                            case '\n': builder.Append("\\n"); break;
                            case '\r': builder.Append("\\r"); break;
                            case '\t': builder.Append("\\t"); break;
                            default:
                                if (c < 0x20) builder.Append("\\u" + ((int)c).ToString("x4"));
                                else builder.Append(c);
                                break;
                        }
                    }
                    builder.Append('"');
                    break;
                case Kind.Array:
                    builder.Append('[');
                    for (var i = 0; i < Array.Count; i++)
                    {
                        if (i > 0) builder.Append(',');
                        (Array[i] ?? Null()).Write(builder);
                    }
                    builder.Append(']');
                    break;
                default:
                    builder.Append('{');
                    var first = true;
                    foreach (var pair in Object)
                    {
                        if (!first) builder.Append(',');
                        first = false;
                        FromString(pair.Key).Write(builder);
                        builder.Append(':');
                        (pair.Value ?? Null()).Write(builder);
                    }
                    builder.Append('}');
                    break;
            }
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _index;

            public Parser(string text) => _text = text;
            public bool Done => _index >= _text.Length;

            public void Skip()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
            }

            public JsonValue Read()
            {
                Skip();
                if (Done) throw new FormatException("Expected JSON.");
                var c = _text[_index];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return FromString(ReadString());
                if (c == 't' || c == 'f') return FromBool(ReadLiteral());
                if (c == 'n')
                {
                    Expect("null");
                    return Null();
                }
                return FromNumber(ReadNumber());
            }

            private JsonValue ReadObject()
            {
                _index++;
                var obj = new Dictionary<string, JsonValue>();
                Skip();
                if (!Done && _text[_index] == '}')
                {
                    _index++;
                    return new JsonValue { Type = Kind.Object, Object = obj };
                }

                while (true)
                {
                    Skip();
                    var key = ReadString();
                    Skip();
                    if (Done || _text[_index] != ':') throw new FormatException("Expected ':'.");
                    _index++;
                    obj[key] = Read();
                    Skip();
                    if (Done) throw new FormatException("Unclosed object.");
                    if (_text[_index] == '}')
                    {
                        _index++;
                        return new JsonValue { Type = Kind.Object, Object = obj };
                    }
                    if (_text[_index] != ',') throw new FormatException("Expected ','.");
                    _index++;
                }
            }

            private JsonValue ReadArray()
            {
                _index++;
                var items = new List<JsonValue>();
                Skip();
                if (!Done && _text[_index] == ']')
                {
                    _index++;
                    return FromArray(items);
                }

                while (true)
                {
                    items.Add(Read());
                    Skip();
                    if (Done) throw new FormatException("Unclosed array.");
                    if (_text[_index] == ']')
                    {
                        _index++;
                        return FromArray(items);
                    }
                    if (_text[_index] != ',') throw new FormatException("Expected ','.");
                    _index++;
                }
            }

            private string ReadString()
            {
                Skip();
                if (Done || _text[_index] != '"') throw new FormatException("Expected a string.");
                _index++;
                var builder = new StringBuilder();
                while (_index < _text.Length)
                {
                    var c = _text[_index++];
                    if (c == '"') return builder.ToString();
                    if (c != '\\')
                    {
                        if (c < 0x20) throw new FormatException("Unescaped control character.");
                        builder.Append(c);
                        continue;
                    }
                    if (_index >= _text.Length) break;
                    var esc = _text[_index++];
                    switch (esc)
                    {
                        case '"':
                        case '\\':
                        case '/': builder.Append(esc); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (_index + 4 > _text.Length) throw new FormatException("Bad escape.");
                            builder.Append((char)int.Parse(_text.Substring(_index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _index += 4;
                            break;
                        default: throw new FormatException("Bad escape.");
                    }
                }
                throw new FormatException("Unclosed string.");
            }

            private bool ReadLiteral()
            {
                if (Match("true")) return true;
                if (Match("false")) return false;
                throw new FormatException("Expected a boolean.");
            }

            private double ReadNumber()
            {
                var start = _index;
                if (!Done && _text[_index] == '-') _index++;
                while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                if (_index < _text.Length && _text[_index] == '.')
                {
                    _index++;
                    while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                }
                if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    _index++;
                    if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-')) _index++;
                    while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                }
                return double.Parse(_text.Substring(start, _index - start), CultureInfo.InvariantCulture);
            }

            private void Expect(string token)
            {
                if (!Match(token)) throw new FormatException($"Expected {token}.");
            }

            private bool Match(string token)
            {
                if (_index + token.Length > _text.Length) return false;
                if (_text.Substring(_index, token.Length) != token) return false;
                _index += token.Length;
                return true;
            }
        }
    }
}
