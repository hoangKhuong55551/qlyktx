using System;
using System.Linq;

namespace Qlykytucxa.Services
{
    public static class CsvHelper
    {
        // Ghep 1 dong CSV: boc moi o trong dau ngoac kep, nhan doi dau " ben trong (chuan RFC 4180)
        // va them ' truoc o bat dau bang = + - @ de Excel khong hieu nham la cong thuc.
        public static string Row(params object[] values)
        {
            return string.Join(",", values.Select(Escape));
        }

        private static string Escape(object value)
        {
            string s = Convert.ToString(value) ?? "";
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0 && !(value is decimal || value is int))
            {
                s = "'" + s;
            }
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
