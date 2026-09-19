# Direct input investigation

## Why email fields escape version 1

The installed configuration on the investigation machine uses Google Japanese
Input and Swiss German. The published v1.0.0 and main at the start of the
investigation used the same routing monitor. PR #9 merged the repair into main.

1. **InputScope and IME state can disagree.** Mozc's design explicitly keeps
   TSF open/hiragana while its effective mode becomes direct input on entry to
   an email field. Therefore checking only IMC_GETOPENSTATUS cannot detect
   this case. Reading conversion mode alone is also insufficient.
2. **Transition-only decisions lose persistent direct input.** After observing
   the source once, v1 requires a consecutive true -> false observation. An
   unknown observation, another application, or a rejected first request can
   leave A active indefinitely.
3. **Focus changes are mistaken for a return to Japanese.** A global previous
   layout can cause an IME to be reopened in a different application's field.
4. **Request is not completion.** PostMessage only queues a request; v1 does
   not verify the resulting layout or retry. SendMessage can also block the
   monitor indefinitely if the other application is hung.

The repair uses persistent-state reconciliation, separate open/conversion
state, focus-aware restoration with a deadline, bounded Windows messages,
observed completion and retries. Focused field metadata from MSAA/IAccessible2
provides a separate signal for email/URL/number/telephone/password fields,
including when IMM still reports native input. It never reads field values,
labels, document content or keystrokes. A stalled accessibility provider runs
on a separate background thread and cached hints expire.

## Limits of the evidence

19 deterministic regression scenarios cover decisions and field metadata.
The Release build passes with warnings treated as errors. Three live Windows
checks passed: native IMM state, actual direct-input routing to Swiss German,
and password-field metadata detection. These use the new implementation in an
isolated WinForms fixture; the installed v1 process was stopped during the run.
Browser end-to-end and Chinese/Korean IME behavior require the manual
matrix below; passing synthetic traces does not establish universal app or
IME compatibility. InputScope-only custom controls that expose neither an
effective IMM state nor supported accessibility metadata remain unsupported.

Post-merge validation on 2026-09-19 passed the Release build and all 19
regression scenarios. The desktop rerun could not acquire foreground focus
on its test control, so that rerun is not a passing integration result.
The fixture now requires the expected foreground control and cancels when
its window loses activation. Run it in an interactive Windows session.
This release remains a preview pending the full acceptance matrix.

The later priority-controls development change passes 30 regression scenarios
and Japanese/English settings round-trip checks. Its desktop attempt could not
enumerate an enabled Japanese IME in the execution session and stopped before
input testing. This does not establish the IME configuration of the user's
normal desktop session. See [the feature validation record](priority-features.md).

## Acceptance matrix

For each of Google Japanese Input, Microsoft Japanese IME, Microsoft Pinyin,
Microsoft Bopomofo and Microsoft Korean IME:

- Use a native editor, Edge/Chrome and Firefox. Enter normal text, email,
  URL, number and password fields with dummy content only.
- Confirm native text stays in the IME. Direct fields must use the selected
  target layout, including punctuation/AltGr specific to that layout.
- Switch back to the IME in a normal field and check native composition.
- Move between fields in the same browser HWND and between applications.
- Repeat with an IME-disabled field and an unresponsive application.
- Confirm full-width Latin is preserved in a normal field.
- Confirm unrelated languages are left unchanged.

Run `ImeLayoutRouter.exe --diagnose 60 diagnostic.log` to collect only handles,
layout IDs, mode classifications and structural field flags. It is read-only;
an independently running router may still change input state.

## Primary references

- [Mozc InputScope design](https://github.com/google/mozc/blob/master/docs/design_doc/input_scope.md)
- [Windows conversion flags](https://learn.microsoft.com/en-us/windows/win32/intl/ime-conversion-mode-values)
- [Bounded SendMessageTimeout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw)
- [IAccessible2 COM interface](https://github.com/LinuxA11y/IAccessible2/blob/master/api/Accessible2.idl)
- [HTML accessibility mappings](https://www.w3.org/TR/html-aam/)
