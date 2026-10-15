using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace SignalForge
{
    // French when the game (or, before the game picked a language, Windows) is in French,
    // English otherwise. Read once: part texts are built once per launch.
    internal static class Lang
    {
        private static bool _known;
        private static bool _french;

        public static bool French
        {
            get
            {
                if (!_known)
                {
                    _french = Detect();
                    _known = true;
                }
                return _french;
            }
        }

        public static string T(string french, string english)
        {
            return French ? french : english;
        }

        private static bool Detect()
        {
            try
            {
                if (LocalizationSettings.InitializationOperation.IsDone && LocalizationSettings.SelectedLocale != null)
                    return LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Log.Once("Lang", e);
            }
            return Application.systemLanguage == SystemLanguage.French;
        }
    }
}
