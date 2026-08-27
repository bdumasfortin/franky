using System.Text.RegularExpressions;
using Franky.Runtime.Tools;

namespace Franky.Runtime.ControlBoard;

public static partial class KnownDeviceIntentRouter
{
    public static bool TryResolve(string text, out string actionName)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (HowIsFrankyDoingPattern().IsMatch(text))
        {
            actionName = DeviceActionTool.FrankySuuuperAction;
            return true;
        }

        actionName = string.Empty;
        return false;
    }

    [GeneratedRegex(
        @"^\s*(?:(?:hey|yo)\s+)?(?:franky[\s,]+)?(?:how(?:['’]s| is) it going|how are you(?: doing)?|how(?:['’]s| is) franky doing|how are things(?: going)?)(?:,?\s+franky)?\s*[?!.]*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex HowIsFrankyDoingPattern();
}
