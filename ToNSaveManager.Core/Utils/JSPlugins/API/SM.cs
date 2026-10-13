using Jint;
using Jint.Runtime.Interop;
using ToNSaveManager.Models;

namespace ToNSaveManager.Utils.JSPlugins.API {
    // Same names and values as the WinForms MessageBox enums, scripts use them as AlertButtons, AlertIcon and AlertResult
    internal enum AlertButtons { OK = 0, OKCancel = 1, AbortRetryIgnore = 2, YesNoCancel = 3, YesNo = 4, RetryCancel = 5, CancelTryContinue = 6 }
    internal enum AlertIcon { None = 0, Hand = 16, Stop = 16, Error = 16, Question = 32, Exclamation = 48, Warning = 48, Asterisk = 64, Information = 64 }
    internal enum AlertResult { None = 0, OK = 1, Cancel = 2, Abort = 3, Retry = 4, Ignore = 5, Yes = 6, No = 7, TryAgain = 10, Continue = 11 }

    [JSEngineAPI("SM")]
    internal static class SM {
        private static string Source => JSEngine.GetLastSyntaxSource();

        /// <summary>
        /// Shows a script alert, set by the frontend. Called on the JS thread with message, title, buttons and icon.
        /// Without a handler the message is only logged.
        /// </summary>
        internal static Func<string, string, AlertButtons, AlertIcon, AlertResult>? AlertHandler;

        internal static void Register(Engine engine) {
            engine.SetValue("AlertButtons", TypeReference.CreateTypeReference<AlertButtons>(engine));
            engine.SetValue("AlertIcon", TypeReference.CreateTypeReference<AlertIcon>(engine));
            engine.SetValue("AlertResult", TypeReference.CreateTypeReference<AlertResult>(engine));
            engine.SetValue("alert", Alert);
        }

        internal static AlertResult Alert(string message, AlertButtons buttons = AlertButtons.OK, AlertIcon icon = AlertIcon.None) {
            if (AlertHandler == null) {
                Console.Log("alert: " + message);
                return AlertResult.None;
            }

            return AlertHandler(message, Source, buttons, icon);
        }

        public static Entry? LatestSave => AppCore.SaveData.FindRecentEntry();
    }
}
