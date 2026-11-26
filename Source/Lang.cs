using System;
using System.Globalization;
using System.IO;

namespace ZenPin
{
    public static class Lang
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZenPin");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "lang.txt");

        private static string _current;

        static Lang()
        {
            Load();
        }

        public static bool IsTr
        {
            get { return _current == "tr"; }
        }

        public static string Code
        {
            get { return IsTr ? "TR" : "EN"; }
        }

        public static string T(string en, string tr)
        {
            return IsTr ? tr : en;
        }

        public static void Toggle()
        {
            _current = IsTr ? "en" : "tr";
            Save();
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string val = File.ReadAllText(ConfigPath).Trim().ToLowerInvariant();
                    if (val == "tr" || val == "en")
                    {
                        _current = val;
                        return;
                    }
                }
            }
            catch { }

            string sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            _current = (sysLang == "tr") ? "tr" : "en";
        }

        private static void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                File.WriteAllText(ConfigPath, _current);
            }
            catch { }
        }
    }
}
