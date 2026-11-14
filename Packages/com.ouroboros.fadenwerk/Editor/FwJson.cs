using System.Globalization;
using System.Text;

namespace Fadenwerk.EditorTools
{
    /// <summary>Kleiner JSON-Schreiber. JsonUtility kann keine Arrays auf oberster Ebene und keine Asset-Infos.</summary>
    internal sealed class FwJson
    {
        readonly StringBuilder _sb = new StringBuilder(4096);
        // true, sobald im aktuellen Objekt/Array schon ein Wert steht (dann braucht der nächste ein Komma).
        readonly bool[] _hasValue = new bool[64];
        int _depth;
        bool _afterKey;

        public override string ToString() { return _sb.ToString(); }

        void BeforeValue()
        {
            if (_afterKey) { _afterKey = false; return; }
            if (_depth > 0)
            {
                if (_hasValue[_depth]) _sb.Append(',');
                _hasValue[_depth] = true;
            }
        }

        public FwJson BeginObject() { BeforeValue(); _sb.Append('{'); Push(); return this; }
        public FwJson EndObject() { _sb.Append('}'); _depth--; return this; }
        public FwJson BeginArray() { BeforeValue(); _sb.Append('['); Push(); return this; }
        public FwJson EndArray() { _sb.Append(']'); _depth--; return this; }

        void Push()
        {
            _depth++;
            if (_depth >= _hasValue.Length) throw new System.InvalidOperationException("FwJson: zu tief verschachtelt");
            _hasValue[_depth] = false;
        }

        public FwJson Key(string key)
        {
            if (_hasValue[_depth]) _sb.Append(',');
            _hasValue[_depth] = true;
            Escape(key);
            _sb.Append(':');
            _afterKey = true;
            return this;
        }

        public FwJson Str(string v)
        {
            BeforeValue();
            if (v == null) _sb.Append("null"); else Escape(v);
            return this;
        }

        public FwJson Num(int v) { BeforeValue(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public FwJson Num(long v) { BeforeValue(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }

        public FwJson Num(float v)
        {
            BeforeValue();
            if (float.IsNaN(v) || float.IsInfinity(v)) _sb.Append('0');
            else _sb.Append(System.Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture));
            return this;
        }

        public FwJson Bool(bool v) { BeforeValue(); _sb.Append(v ? "true" : "false"); return this; }
        public FwJson Null() { BeforeValue(); _sb.Append("null"); return this; }

        /// <summary>Fügt bereits fertiges JSON ein (z. B. aus JsonUtility.ToJson).</summary>
        public FwJson Raw(string json) { BeforeValue(); _sb.Append(string.IsNullOrEmpty(json) ? "null" : json); return this; }

        public FwJson Prop(string k, string v) { return Key(k).Str(v); }
        public FwJson Prop(string k, int v) { return Key(k).Num(v); }
        public FwJson Prop(string k, float v) { return Key(k).Num(v); }
        public FwJson Prop(string k, bool v) { return Key(k).Bool(v); }

        void Escape(string s)
        {
            _sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    case '\b': _sb.Append("\\b"); break;
                    case '\f': _sb.Append("\\f"); break;
                    default:
                        // < und > maskieren, damit JSON gefahrlos in HTML landen kann.
                        if (c < 0x20 || c == '<' || c == '>' || c == '\u2028' || c == '\u2029')
                            _sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }

        public static string Error(string message)
        {
            return new FwJson().BeginObject().Prop("ok", false).Prop("error", message ?? "Fehler").EndObject().ToString();
        }

        public static string Ok()
        {
            return "{\"ok\":true}";
        }
    }
}
