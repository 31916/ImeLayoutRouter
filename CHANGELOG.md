# Change log

## Unreleased — priority routing controls

- Wake on foreground/focus changes and refreshed field metadata; use a 50 ms
  routing fallback and isolate event delivery from slow accessibility providers.
- Reject stale focus generations before routing, including within one HWND.
- Add exact executable-path rules, exclusions and an optional allowlist default.
- Show observed layout/mode and pending, confirmed or suppressed routing reasons.
- Add opt-in, configurable pause/target/restore hotkeys with conflict reporting.
  Preserve manual layout choices until leaving the window or explicitly resuming.
- Add Japanese/English guided settings and a window-scoped, unsaved typing check.
- Migrate settings schemas 1/2 to 3; preserve existing choices and keep shortcuts off.
- Extend regression and noninteractive settings UI checks. Browser/CJK composition,
  real shortcut conflicts and actual latency still require interactive validation.

## 3.0.0-preview.1

- Optional routing for all enabled Japanese, Chinese and Korean IMEs.
- Make language-level routing explicit in settings and documentation.
- Read legacy v1 settings; save schema v2 atomically.
- Pause/resume, local diagnostic export and visible monitor failure reporting.
- Single-instance mutex; a second launch asks the first to open Settings.
- Ignore stale accessibility hints, including focus events within one HWND.
- Windows regression CI and manual package builds.
- Require the desktop test to own foreground focus; cancel on deactivation.

## 2.0.0-preview.1

- Select Japanese, Chinese regional variants and Korean IMEs.
- Restore native input without forcing Japanese conversion flags on other languages.

## 1.0.1 (repair implementation)

- Detect field-forced direct input even when Google IME still reports native.
- Reconcile persistent direct input and retry unconfirmed layout requests.
- Read both open state and conversion mode; preserve full-width Latin.
- Separate app/control changes from returning to a source IME.
- Bound native restoration and cross-process IME message waits.
- Add deterministic regression traces and read-only diagnostics.

These entries describe development milestones; consult GitHub Releases for
published packages. Chinese/Korean and browser acceptance testing remain
required before promoting the previews to stable.
