using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace WoLChaosDepths
{
    // A small JSON reader and writer for the save and loot files. Unity 5.6's JsonUtility can't
    // see classes from a plugin loaded at runtime: it writes "{}" and reads nothing back. This
    // handles what those files hold: public fields of strings, numbers, bools, lists and nested
    // [Serializable] classes. Fields missing from a file keep their defaults, unknown keys are
    // ignored, so older and hand-edited files still load.
    public static class Json
    {
        // ---- Writing ----

        public static string Write(object value, bool pretty = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, bool pretty, int depth)
        {
            if (value == null)
                sb.Append("null");
            else if (value is string)
                WriteString(sb, (string)value);
            else if (value is bool)
                sb.Append((bool)value ? "true" : "false");
            else if (value is float)
                sb.Append(((float)value).ToString("R", CultureInfo.InvariantCulture));
            else if (value is double)
                sb.Append(((double)value).ToString("R", CultureInfo.InvariantCulture));
            else if (value is int || value is long || value is short || value is byte)
                sb.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture));
            else if (value is Enum)
                WriteString(sb, value.ToString());
            else if (value is IList)
            {
                IList list = (IList)value;
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    NewLine(sb, pretty, depth + 1);
                    WriteValue(sb, list[i], pretty, depth + 1);
                }
                if (list.Count > 0)
                    NewLine(sb, pretty, depth);
                sb.Append(']');
            }
            else
            {
                sb.Append('{');
                bool first = true;
                foreach (FieldInfo field in Fields(value.GetType()))
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    NewLine(sb, pretty, depth + 1);
                    WriteString(sb, field.Name);
                    sb.Append(pretty ? ": " : ":");
                    WriteValue(sb, field.GetValue(value), pretty, depth + 1);
                }
                if (!first)
                    NewLine(sb, pretty, depth);
                sb.Append('}');
            }
        }

        private static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty)
                return;
            sb.Append('\n');
            sb.Append(' ', depth * 4);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // Public instance fields, as JsonUtility would serialize them.
        private static IEnumerable<FieldInfo> Fields(Type type)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!field.IsNotSerialized && !field.IsInitOnly)
                    yield return field;
        }

        // ---- Reading ----

        public static T Read<T>(string text) where T : new()
        {
            int i = 0;
            object parsed = Parse(text, ref i);
            return (T)Convert(parsed, typeof(T));
        }

        private static object Convert(object parsed, Type type)
        {
            if (parsed == null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            if (type == typeof(string))
                return parsed as string ?? System.Convert.ToString(parsed, CultureInfo.InvariantCulture);
            if (type == typeof(bool))
                return parsed is bool ? parsed : false;
            if (type.IsEnum)
                return parsed is string ? Enum.Parse(type, (string)parsed, true) : Enum.ToObject(type, System.Convert.ToInt32(parsed));
            if (type.IsPrimitive)
                return parsed is double ? System.Convert.ChangeType(parsed, type, CultureInfo.InvariantCulture) : Activator.CreateInstance(type);
            if (typeof(IList).IsAssignableFrom(type))
            {
                var list = (IList)Activator.CreateInstance(type);
                Type element = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                var items = parsed as List<object>;
                if (items != null)
                    foreach (object item in items)
                        list.Add(Convert(item, element));
                return list;
            }
            object result = Activator.CreateInstance(type);
            var dict = parsed as Dictionary<string, object>;
            if (dict == null)
                return result;
            foreach (FieldInfo field in Fields(type))
            {
                object raw;
                if (dict.TryGetValue(field.Name, out raw))
                    field.SetValue(result, Convert(raw, field.FieldType));
            }
            return result;
        }

        // Parses into Dictionary<string, object>, List<object>, string, double, bool or null.
        private static object Parse(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length)
                throw new FormatException("unexpected end of the file");
            char c = s[i];
            if (c == '{')
            {
                var dict = new Dictionary<string, object>();
                i++;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return dict;
                }
                while (true)
                {
                    SkipSpace(s, ref i);
                    string key = ParseString(s, ref i);
                    SkipSpace(s, ref i);
                    Expect(s, ref i, ':');
                    dict[key] = Parse(s, ref i);
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    Expect(s, ref i, '}');
                    return dict;
                }
            }
            if (c == '[')
            {
                var list = new List<object>();
                i++;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return list;
                }
                while (true)
                {
                    list.Add(Parse(s, ref i));
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    Expect(s, ref i, ']');
                    return list;
                }
            }
            if (c == '"')
                return ParseString(s, ref i);
            if (Match(s, ref i, "true"))
                return true;
            if (Match(s, ref i, "false"))
                return false;
            if (Match(s, ref i, "null"))
                return null;
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
                i++;
            if (i == start)
                throw new FormatException($"unexpected '{c}' at character {i}");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string ParseString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= s.Length)
                    break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("unterminated string");
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c)
                throw new FormatException($"expected '{c}' at character {i}");
            i++;
        }

        private static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                return false;
            i += word.Length;
            return true;
        }
    }
}
