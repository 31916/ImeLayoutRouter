# Change log

## 2026-09-26 — Ver1.1.1 / Ver2.0.1

- Request structural IAccessible2 metadata along the focus chain before rejecting
  an unfocused/read-only browser placeholder. Chromium can expose the actual input
  field only after these requests enable web accessibility metadata.
- Keep routing restricted to the final focused, enabled, writable field; never use
  a parent/document's input type. Bound cyclic providers and retain native password
  detection when IAccessible2 is unavailable.
- Add five regression cases and an opt-in, metadata-only Windows diagnostic.
- V1/V2 builds and noninteractive settings checks pass. Physical-key verification
  on the reported contact page is still pending. Publication was approved with
  this limitation disclosed; it is not counted as a successful compatibility test.
  See [validation details](docs/browser-field-fix-2026-09-26.md).
- Use a single version label on the download page and installer filenames:
  `Ver1.1.1` / `Ver2.0.1`, and `ImeLayoutRouter-<version>-Setup.exe`.

## 2026-09-20 — V1 1.1.0 / V2 2.0.0

- Publish separate Japanese essentials (V1) and full (V2) installers on the
  Japanese/English website; retain the original application icon throughout.
- Both editions include the repaired direct-input/field-metadata routing engine,
  separate settings and installer identities, and startup ownership cleanup.
- V1 excludes advanced controls and runtime modules; V2 includes the controls below.
- Publication was approved with incomplete compatibility coverage. Native Google
  Japanese/Swiss layout checks and installer lifecycle checks passed; browser
  fields, Microsoft Japanese, Chinese and Korean IME composition remain unverified.
  See [acceptance evidence](docs/acceptance-2026-09-20.md).

### V2 routing controls

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

The preview entries above are historical internal milestones. Public installers
are available only from https://31916.ch/IMELayOutRouter/; GitHub Releases is no
longer used. Browser/CJK acceptance remains follow-up work, not a completed check.
