using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AllLive.Core.Helper
{
    public static class Utils
    {
        /// <summary>
        ///时间戳(秒)
        /// </summary>
        /// <returns></returns>
        public static long GetTimestamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds);
        }
        /// <summary>
        /// 时间戳(毫秒)
        /// </summary>
        /// <returns></returns>
        public static long GetTimestampMs()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalMilliseconds);
        }

        public static string ToMD5(string data)
        {
            MD5 md5 = MD5.Create();

            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(data));
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var item in hash)
            {
                stringBuilder.Append(item.ToString("x2"));
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// 解码转义utf8字符串
        /// </summary>
        /// <returns></returns>
        public static string UnEscapeUTF8String(string text)
        {
            return Encoding.UTF8.GetString(Encoding.Default.GetBytes(text));
        }

        public static string MatchText(this string input, string pattern, string _default = "0")
        {
            try
            {
                return Regex.Match(input, pattern).Groups[1].Value;
            }
            catch (Exception)
            {
                return _default;
            }

        }
        public static string MatchTextSingleline(this string input, string pattern, string _default = "0")
        {
            try
            {
                return Regex.Match(input, pattern, RegexOptions.Singleline).Groups[1].Value;
            }
            catch (Exception)
            {
                return _default;
            }

        }
        public static int ToInt32(this object input)
        {

            if (int.TryParse(input?.ToString() ?? "0", out var result))
            {
                return result;
            }
            else
            {
                return 0;
            }
        }
        public static long ToInt64(this object input)
        {

            if (long.TryParse(input?.ToString() ?? "0", out var result))
            {
                return result;
            }
            else
            {
                return 0;
            }
        }
        public static bool ToBool(this object input)
        {

            if (bool.TryParse(input?.ToString() ?? "false", out var result))
            {
                return result;
            }
            else
            {
                return false;
            }
        }

        public static Color NumberToColor(this int intColor)
        {

            var obj = intColor.ToString("X2");

            Color color = Color.White;
            if (obj.Length == 4)
            {
                obj = "00" + obj;
            }
            if (obj.Length == 6)
            {
                var R = byte.Parse(obj.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                var G = byte.Parse(obj.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var B = byte.Parse(obj.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                var A = 255;
                color = Color.FromArgb(A, R, G, B);
            }
            if (obj.Length == 8)
            {
                var R = byte.Parse(obj.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var G = byte.Parse(obj.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                var B = byte.Parse(obj.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                var A = byte.Parse(obj.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                color = Color.FromArgb(A, R, G, B);
            }

            return color;
        }
    }
}
