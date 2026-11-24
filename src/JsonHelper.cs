using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AnrFgManager
{
    public static class JsonHelper
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int idx = 0;
            return ParseValue(json, ref idx);
        }

        public static Dictionary<string, object> ParseObject(string json)
        {
            return Parse(json) as Dictionary<string, object>;
        }

        public static string GetString(Dictionary<string, object> d, string key, string defVal)
        {
            if (d == null) return defVal;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return defVal;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool defVal)
        {
            if (d == null) return defVal;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return defVal;
            if (v is bool) return (bool)v;
            bool b;
            if (bool.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out b)) return b;
            return defVal;
        }

        public static int GetInt(Dictionary<string, object> d, string key, int defVal)
        {
            if (d == null) return defVal;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return defVal;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch { return defVal; }
        }

        public static Dictionary<string, object> GetDict(Dictionary<string, object> d, string key)
        {
            if (d == null) return null;
            object v;
            if (!d.TryGetValue(key, out v)) return null;
            return v as Dictionary<string, object>;
        }

        public static List<object> GetList(Dictionary<string, object> d, string key)
        {
            if (d == null) return null;
            object v;
            if (!d.TryGetValue(key, out v)) return null;
            return v as List<object>;
        }

        public static List<string> GetStringList(Dictionary<string, object> d, string key)
        {
            List<string> result = new List<string>();
            if (d == null) return result;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return result;
            List<object> list = v as List<object>;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null)
                    {
                        string s = Convert.ToString(list[i], CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(s)) result.Add(s);
                    }
                }
            }
            else if (v is string)
            {
                string s = (string)v;
                if (!string.IsNullOrEmpty(s)) result.Add(s);
            }
            return result;
        }

        public static string Serialize(object obj)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, obj, 0);
            sb.Append("\r\n");
            return sb.ToString();
        }

        private static void WriteIndent(StringBuilder sb, int level)
        {
            for (int i = 0; i < level; i++) sb.Append("  ");
        }

        private static void WriteValue(StringBuilder sb, object val, int indent)
        {
            if (val == null)
            {
                sb.Append("null");
                return;
            }
            if (val is bool)
            {
                sb.Append((bool)val ? "true" : "false");
                return;
            }
            if (val is string)
            {
                WriteString(sb, (string)val);
                return;
            }
            if (val is int || val is long || val is short || val is byte || val is double || val is float || val is decimal)
            {
                sb.Append(Convert.ToString(val, CultureInfo.InvariantCulture));
                return;
            }
            IDictionary dict = val as IDictionary;
            if (dict != null)
            {
                if (dict.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }
                sb.Append("{\r\n");
                int i = 0;
                foreach (DictionaryEntry kv in dict)
                {
                    if (i > 0) sb.Append(",\r\n");
                    WriteIndent(sb, indent + 1);
                    WriteString(sb, Convert.ToString(kv.Key, CultureInfo.InvariantCulture));
                    sb.Append(": ");
                    WriteValue(sb, kv.Value, indent + 1);
                    i++;
                }
                sb.Append("\r\n");
                WriteIndent(sb, indent);
                sb.Append("}");
                return;
            }
            IEnumerable arr = val as IEnumerable;
            if (arr != null)
            {
                List<object> items = new List<object>();
                foreach (object item in arr) items.Add(item);
                if (items.Count == 0)
                {
                    sb.Append("[]");
                    return;
                }
                sb.Append("[\r\n");
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) sb.Append(",\r\n");
                    WriteIndent(sb, indent + 1);
                    WriteValue(sb, items[i], indent + 1);
                }
                sb.Append("\r\n");
                WriteIndent(sb, indent);
                sb.Append("]");
                return;
            }
            WriteString(sb, Convert.ToString(val, CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 32)
                                sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else
                                sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n' || s[i] == '\uFEFF'))
                i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObj(s, ref i);
            if (c == '[') return ParseArr(s, ref i);
            if (c == '"') return ParseStr(s, ref i);
            if (c == 't' && i + 4 <= s.Length && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (c == 'f' && i + 5 <= s.Length && s.Substring(i, 5) == "false") { i += 5; return false; }
            if (c == 'n' && i + 4 <= s.Length && s.Substring(i, 4) == "null") { i += 4; return null; }
            return ParseNum(s, ref i);
        }

        private static Dictionary<string, object> ParseObj(string s, ref int i)
        {
            Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++; // '{'
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                string key = ParseStr(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object val = ParseValue(s, ref i);
                d[key] = val;
            }
            return d;
        }

        private static List<object> ParseArr(string s, ref int i)
        {
            List<object> list = new List<object>();
            i++; // '['
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                list.Add(ParseValue(s, ref i));
            }
            return list;
        }

        private static string ParseStr(string s, ref int i)
        {
            StringBuilder sb = new StringBuilder();
            if (i < s.Length && s[i] == '"') i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length)
                            {
                                int code;
                                if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                    sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static object ParseNum(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                    i++;
                else
                    break;
            }
            string num = s.Substring(start, i - start);
            long lv;
            if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv))
                return lv;
            double dv;
            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out dv))
                return dv;
            return 0;
        }
    }
}
