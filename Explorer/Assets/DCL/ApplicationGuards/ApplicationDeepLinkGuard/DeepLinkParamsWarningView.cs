using Global.AppArgs;
using MVC;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ApplicationGuards
{
    /// <summary>
    ///     Startup warning shown when the launch deep link carried params that failed the allowlist
    ///     (see <see cref="DeepLinkAllowlist" />). Exiting is the highlighted default; continuing is deliberately
    ///     two-step (Advanced, then an explicitly unsafe confirmation) so it cannot be dismissed by reflex.
    /// </summary>
    public class DeepLinkParamsWarningView : ViewBase, IView
    {
        private const string LEAD =
            "Someone may be trying to change how your Explorer behaves.";

        private const string HEADER =
            "This link carries parameters the Explorer does not accept from links, because they can change what "
            + "this client connects to and which safety checks run:";

        private const string FOOTER_SAFE =
            "Unless you built this link yourself, the safe choice is to exit.";

        private const string FOOTER_RISK =
            "You are about to apply parameters that were blocked to protect you. They will take effect for this "
            + "entire session, including any change to the servers this client talks to and the checks it skips. "
            + "By continuing you accept full responsibility for the consequences.";

        private const int MAX_SHOWN_VALUE_LENGTH = 40;

        // A link can carry any number of params; enumerating all of them would push the warning text over the
        // buttons and bury the message. The count of the rest is still reported.
        private const int MAX_SHOWN_PARAMS = 6;

        [field: SerializeField]
        public Button ContinueButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ExitButton { get; private set; } = null!;

        [field: SerializeField]
        public Button AdvancedButton { get; private set; } = null!;

        // Rich text is disabled on this component: the enumerated keys/values come from an
        // attacker-controllable deep link and must never be able to inject TMP markup.
        [SerializeField] private TMP_Text description = null!;

        // The enumerated params, built once, so revealing the risk text only swaps the closing paragraph.
        private string deniedParamsBlock = string.Empty;

        public void SetDeniedParams(IReadOnlyDictionary<string, string> deniedParams)
        {
            var sb = new StringBuilder();
            var shown = 0;

            foreach ((string key, string value) in deniedParams)
            {
                if (shown == MAX_SHOWN_PARAMS)
                {
                    sb.Append("- and ").Append(deniedParams.Count - shown).AppendLine(" more blocked parameter(s).");
                    break;
                }

                shown++;

                sb.Append("- ");
                AppendSingleLine(sb, key, key.Length);

                if (!string.IsNullOrEmpty(value))
                {
                    sb.Append(" = ");
                    AppendSingleLine(sb, value, MAX_SHOWN_VALUE_LENGTH);
                }

                sb.AppendLine();
                sb.Append("   ").AppendLine(DeepLinkParamDescriptions.For(key));
            }

            deniedParamsBlock = sb.ToString();
            SetDescription(FOOTER_SAFE);
        }

        /// <summary>
        ///     Second step of the continue path: swaps the closing text for the responsibility statement and replaces
        ///     the Advanced button with the explicitly unsafe confirmation.
        /// </summary>
        public void RevealContinueOption()
        {
            SetDescription(FOOTER_RISK);
            AdvancedButton.gameObject.SetActive(false);
            ContinueButton.gameObject.SetActive(true);
        }

        private void SetDescription(string footer)
        {
            var sb = new StringBuilder();

            sb.AppendLine(LEAD);
            sb.AppendLine();
            sb.AppendLine(HEADER);
            sb.AppendLine();
            sb.AppendLine(deniedParamsBlock);
            sb.Append(footer);

            description.text = sb.ToString();
        }

        // Truncates and flattens control characters so a crafted value cannot fake extra list entries
        // or message lines in the warning.
        private static void AppendSingleLine(StringBuilder sb, string value, int maxLength)
        {
            int length = Mathf.Min(value.Length, maxLength);

            for (var i = 0; i < length; i++)
            {
                char c = value[i];
                sb.Append(char.IsControl(c) ? ' ' : c);
            }

            if (value.Length > maxLength)
                sb.Append("...");
        }
    }
}
