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
    ///     (see <c>DeepLinkAllowlist</c>). Enumerates them and lets the user either exit or explicitly
    ///     accept the risk and continue with those params applied.
    /// </summary>
    public class DeepLinkParamsWarningView : ViewBase, IView
    {
        private const string HEADER =
            "This launch link contains parameters that are not on the Explorer allowlist:";

        private const string FOOTER =
            "These parameters can change the environments and services the Explorer connects to. "
            + "Continue only if you crafted this link yourself or fully trust its source. "
            + "If you continue, these parameters will be applied and you do so at your own risk: "
            + "you are responsible for any consequences.";

        private const int MAX_SHOWN_VALUE_LENGTH = 48;

        [field: SerializeField]
        public Button ContinueButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ExitButton { get; private set; } = null!;

        // Rich text is disabled on this component: the enumerated keys/values come from an
        // attacker-controllable deep link and must never be able to inject TMP markup.
        [SerializeField] private TMP_Text description = null!;

        public void SetDeniedParams(IReadOnlyDictionary<string, string> deniedParams)
        {
            var sb = new StringBuilder();

            sb.AppendLine(HEADER);
            sb.AppendLine();

            foreach ((string key, string value) in deniedParams)
            {
                sb.Append("- ");
                AppendSingleLine(sb, key, key.Length);

                if (!string.IsNullOrEmpty(value))
                {
                    sb.Append(" = ");
                    AppendSingleLine(sb, value, MAX_SHOWN_VALUE_LENGTH);
                }

                sb.AppendLine();
            }

            sb.AppendLine();
            sb.Append(FOOTER);

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
