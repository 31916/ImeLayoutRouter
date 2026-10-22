using System.Diagnostics;
using System.Text;
using System.Windows.Automation;

// Test-only inspection of the disposable VM's Microsoft candidate panel.
// The real application does not reference UI Automation or read any text.
internal static class AccessibleCandidates
{
    internal static bool AreVisible()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Candidate inspection requires the disposable VM");
        bool japan = false, two = false;
        Native.EnumWindows((window, _) =>
        {
            if (!Native.IsWindowVisible(window)) return true;
            var name = new StringBuilder(128); Native.GetClassNameW(window, name, name.Capacity);
            if (name.ToString() != "Windows.UI.Core.CoreWindow") return true;
            Native.GetWindowThreadProcessId(window, out uint pid);
            using var process = Process.GetProcessById((int)pid);
            if (process.ProcessName is not ("TextInputHost" or "InputApp")) return true;
            var root = AutomationElement.FromHandle(window);
            var elements = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.IsOffscreenProperty, false));
            for (int i = 0; i < Math.Min(elements.Count, 128); i++)
            {
                var info = elements[i].Current;
                if (info.BoundingRectangle.IsEmpty || info.BoundingRectangle.Width <= 0 || info.BoundingRectangle.Height <= 0) continue;
                string label = info.Name.Trim();
                Console.WriteLine($"Own Microsoft candidate UI: type={info.ControlType.ProgrammaticName}; name={label}; rect={info.BoundingRectangle}");
                string word = label.TrimStart(' ', '\t', '\r', '\n', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '、');
                japan |= word == "日本";
                two |= word == "二本";
            }
            return true;
        }, 0);
        return japan && two;
    }
}
