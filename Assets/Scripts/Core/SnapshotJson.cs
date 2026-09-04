using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RubikSim.Core
{
    // Deliberately limited to the versioned, flat snapshot schema. No reflection, Unity or JSON package needed.
    internal sealed class SnapshotJson
    {
        internal readonly struct Value
        {
            internal string Text { get; }
            internal bool IsString { get; }
            internal Value(string text, bool isString) { Text = text; IsString = isString; }
        }
        private readonly string input;
        private int at;
        private SnapshotJson(string input) { this.input = input; }
        internal static Dictionary<string,Value> Read(string json)
        {
            if (json == null || json.Length > 8192) throw new FormatException("Snapshot must be a JSON object of at most 8192 characters.");
            return new SnapshotJson(json).Object();
        }
        private Dictionary<string,Value> Object()
        {
            var result = new Dictionary<string,Value>(StringComparer.Ordinal);
            Expect('{'); White();
            if (Take('}')) { End(); return result; }
            while (true)
            {
                string key = String(); Expect(':'); White();
                var value = Peek() == '"' ? new Value(String(), true) : new Value(Integer(), false);
                if (result.ContainsKey(key)) throw Error("Duplicate field '" + key + "'");
                result.Add(key, value); White();
                if (Take('}')) break;
                Expect(',');
            }
            End(); return result;
        }
        private string String()
        {
            Expect('"'); var result = new StringBuilder();
            while (at < input.Length)
            {
                char c = input[at++];
                if (c == '"') return result.ToString();
                if (c < 32) throw Error("Unescaped control character in string");
                if (c != '\\') { result.Append(c); continue; }
                if (at >= input.Length) throw Error("Incomplete string escape");
                char escaped = input[at++];
                switch (escaped)
                {
                    case '"': case '\\': case '/': result.Append(escaped); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (at+4 > input.Length || !ushort.TryParse(input.Substring(at,4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code))
                            throw Error("Invalid Unicode escape");
                        result.Append((char)code); at += 4; break;
                    default: throw Error("Unknown string escape");
                }
            }
            throw Error("Unterminated string");
        }
        private string Integer()
        {
            White(); int start = at;
            if (Peek() == '-') at++;
            if (Peek() == '0') at++;
            else
            {
                if (Peek() < '1' || Peek() > '9') throw Error("Expected a string or integer field value");
                while (Peek() >= '0' && Peek() <= '9') at++;
            }
            return input.Substring(start, at-start);
        }
        private char Peek() => at < input.Length ? input[at] : '\0';
        private void White() { while (at < input.Length && (input[at] == ' ' || input[at] == '\n' || input[at] == '\r' || input[at] == '\t')) at++; }
        private bool Take(char c) { White(); if (Peek() != c) return false; at++; return true; }
        private void Expect(char c) { if (!Take(c)) throw Error("Expected '" + c + "'"); }
        private void End() { White(); if (at != input.Length) throw Error("Unexpected content after snapshot"); }
        private FormatException Error(string message) => new FormatException(message + " at JSON character " + (at+1) + ".");
    }
}
