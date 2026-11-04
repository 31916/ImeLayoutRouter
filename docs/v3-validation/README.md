# V3 validation record

This record distinguishes passing evidence from failed investigations. See the
[V3 user guide](../../Editions/Guard/README.md) for behavior and limitations.

## Validation gates

- On 2026-11-04 JST, [run 41774873573](https://github.com/31916/ImeLayoutRouter/actions/runs/41774873573)
  at `13f44ee` successfully preserved the pre-sleep checkpoint. Windows
  measured the real popup at `0,0` / 96 DPI and `-1280,0` / 120 DPI; both
  monitors passed off/on suppression, normal-position restoration and the
  identical committed text. QEMU 10.2.1 observed actual S3 in the **V3-off**
  baseline and accepted the wake request, but Windows did not acknowledge
  resume within 180 seconds. This is a failed run, not a passing sleep test.
  For comparison, the hardware-timer test boots the same installed guest
  without Hyper-V synthetic timers following driver preparation. This keeps
  Windows Setup's known working configuration and changes no Windows boot
  policy. Timer causation is unproven. [Google IME](https://github.com/31916/ImeLayoutRouter/actions/runs/41774873564)
  and the [packaged lifecycle/native checks](https://github.com/31916/ImeLayoutRouter/actions/runs/41774873562)
  independently passed at the same revision.
- The mixed-monitor fixture initially failed while setting a disabled QXL
  source through the legacy mode API. `280c4c5` first activates Windows' actual
  extended topology, then requires the requested monitor rectangles and scales.
  At `ddef937`, Microsoft IME retained the live popup at `0,0` after IMM was
  notified of `300,180`, with V3 both off and on. The fixture now explicitly
  moves the real IME surface owned by its own input thread and verifies its
  physical position, drawing region and unchanged preedit/commit. This tests
  recovery from actual window movement, not whether TSF obeys a legacy IMM
  positioning request. Cross-monitor corner placement uses the same approach;
  the HWND, IME buffer, Windows monitors and DPI transitions remain real.
  These observations are from [run 40067358257](https://github.com/31916/ImeLayoutRouter/actions/runs/40067358257);
  at `c4decac`, both browser stages completed and all mixed-display checks
  finished, including the return to a single monitor. Microsoft IME initially
  placed the second-display popup at `0,0` even with `-1280,0` requested. The
  real popup reached each verified corner and moving it to a normal position
  restored drawing without changing its preedit or committed text. The job
  failed while archiving the still-open fixture log immediately before S3,
  so this run does **not** establish successful sleep/resume. The checkpoint
  now reads through a stream that allows the existing writer. A fresh run
  executes the sleep stage first while still requiring both browser stages.
- Packaged lifecycle checks passed on [Windows x64 at `1fc06e7`](https://github.com/31916/ImeLayoutRouter/actions/runs/40067004931)
  and [Windows 11 ARM at `8ec05b6`](https://github.com/31916/ImeLayoutRouter/actions/runs/40067041968).
  The latter installed and ran the actual self-contained **x64** package under
  Windows emulation, including its helper, first-run settings shutdown, running
  upgrade with settings open, uninstall, saved choices and V2 startup isolation.
  This is packaged lifecycle evidence; real IME observations on ARM were from
  the separate native ARM64 fixture below. The shutdown regression first failed
  at `11e92a4` ([run 40066771443](https://github.com/31916/ImeLayoutRouter/actions/runs/40066771443)).
  Initial settings now open after the application loop starts, and shutdown
  closes the owned dialog without re-enabling monitoring in its cleanup path.
- [Browser input completion run 40066188317](https://github.com/31916/ImeLayoutRouter/actions/runs/40066188317)
  passed at `bfd54ba` with Google Japanese Input. Chrome 156.0.8078.48 and
  Edge 155.0.4283.62 each completed
  12 fresh profiles, 48 real preedits and 48 first-candidate conversion commits,
  alternating V3 off-first and on-first. This uses the corrected native wait
  and observed conversion before Enter. Failed words are not resubmitted.
- [Windows 11 x64 investigation 40063353493](https://github.com/31916/ImeLayoutRouter/actions/runs/40063353493)
  reached actual S3 but was cancelled after the V3-off baseline guest stopped
  responding to wakeup. It is not a passing resume test. The hardware-timer
  comparison ([run 40066123948](https://github.com/31916/ImeLayoutRouter/actions/runs/40066123948))
  then stalled at Windows Setup 30%, before V3 ran, and was cancelled after
  about twelve minutes without progress. Direct synthetic-timer interrupts
  are being checked separately. Subsequent runs require a three-minute wake
  acknowledgement limit and preserve a fixed-evidence checkpoint before sleep.
  The separate Chrome mismatch was an observed `日本` versus `二本` selection,
  not missing input. Browser checks now observe the first `日本` conversion
  before Enter; candidate visibility/navigation remain separate native checks.
  The old Space/Space/Up sequence cannot assume identical panel behavior in
  Microsoft and Google IMEs. A repeated-mask failure at `13b7976` also had no
  timeout diagnostic, and the retained window dump showed the real frame
  already masked. The wait helper had re-read a successful native observation
  twice outside its polling loop. It now returns that observed result directly.
- [Browser ownership regression run 40062932157](https://github.com/31916/ImeLayoutRouter/actions/runs/40062932157)
  passed at `a0674a2` with Google Japanese Input on Windows Server 2025.
  Chrome and Edge each passed 12 fresh profiles, 48 real preedit checks and 48
  conversion commits after the fixture learned to track Chromium's normal
  privilege relaunch by its unique profile, local URL, unique title and verified
  executable. The fixture keeps the process handle through cleanup.
- [Windows 11 x64 investigation 40059471171](https://github.com/31916/ImeLayoutRouter/actions/runs/40059471171)
  is a failed investigation, not a passing release gate. Driver reboot enabled
  actual S3 and exposed both QXL adapters; canonical native MSI paths fixed
  Chrome installation. The initial Windows setup itself stopped with `0x3B`
  before any V3 fixture ran and then rebooted. At repair revision `df1a2ee`,
  the fixture read `にほｎ`, while the immediately following event trace already
  contained `にほん`. It had checked the renderer before queued input finished
  arriving. The corrected fixture observes the one submitted word for a bounded
  interval and waits for composition-end delivery; it never resends the word.
  Subsequent validation must establish success. Independent browser and raw-IME
  stages now retain their own evidence, and each corrected revision gets a
  bounded repair window within the hypervisor's fixed lifetime.
- [Windows 11 ARM completion run 40051834349](https://github.com/31916/ImeLayoutRouter/actions/runs/40051834349)
  passed at `7f88a8c` on 2026-10-24 JST with Microsoft Japanese IME.
  Chrome text input and textarea preserved actual `にほん` preedit and `日本`
  commits with V3 off/on. Normal and borderless fullscreen fallback tests,
  conversion selection, cancellation, restoration and repeated input passed.
  The fullscreen candidate comparison preserved all 1,965 baseline glyph
  pixels; the complete before/after screenshots were also reviewed. The
  isolated ARM runner's WSL prompt workaround was active. This is an ARM64
  fixture result, not a test of the x64 installer running under ARM emulation.
- [Actual display-scaling run 39946177768](https://github.com/31916/ImeLayoutRouter/actions/runs/39946177768)
  passed at `aa00156` with Google Japanese Input on Windows Server 2025.
  The disposable display used 1920×1080 with actual Windows scaling set to
  100% and 150%. `GetDpiForWindow` confirmed 96 and 144 DPI. At each scale,
  real preedit, suppression off/on, candidate selection and committed `日本`
  passed. Original scale and resolution were restored. This is actual DPI
  coverage on one virtual display, not mixed-monitor or physical GPU coverage.
- [Actual OpenTTD run 39944490548](https://github.com/31916/ImeLayoutRouter/actions/runs/39944490548)
  passed at `58d5444` with Google Japanese Input. Official OpenTTD 15.3 and
  OpenGFX 8.0 portable archives were checked against published SHA-256 hashes.
  Real OS keystrokes entered and converted the word; both windowed and actual
  Win32 fullscreen modes logged exactly `日本` with V3 off/on. Candidate
  screenshots were reviewed. The game's default console font lacks Japanese
  glyphs on this image in both states; its own UTF-8 log confirms the committed
  characters, and the IME candidate text is visible. This is compatibility
  evidence for this game, not a claim that it reproduced the corner-popup bug
  or that every graphics driver, exclusive DirectX game or anti-cheat is covered.
- [Final browser readiness run 39942734809](https://github.com/31916/ImeLayoutRouter/actions/runs/39942734809)
  passed at `e7b2b1a`, including the bounded CDP target-service wait. Chrome and
  Edge each passed 12 fresh starts, 48 preedit checks and 48 conversion commits.
- [Extended actual-input run 39941173718](https://github.com/31916/ImeLayoutRouter/actions/runs/39941173718)
  passed at `3b311b9`: 200 consecutive real compositions/cancellations with one
  continuously running helper. Show-event-to-empty-region observations ranged
  from 31 to 421 ms; these are not first-frame latency measurements.
- [Borderless fullscreen run 39942310612](https://github.com/31916/ImeLayoutRouter/actions/runs/39942310612)
  passed at `da89de2` with Google Japanese Input. An actual fullscreen Win32
  fixture preserved `にほん`, visible conversion candidates and the same `日本`
  commit with suppression off/on. Before/after screenshots were reviewed.
  This does not establish compatibility with a named game, exclusive DirectX
  fullscreen or anti-cheat software. The same run passed the browser fixture
  after adding a bounded wait for its own CDP target service: a separate
  [earlier run](https://github.com/31916/ImeLayoutRouter/actions/runs/39941171126)
  exhausted the original five-second HTTP timeout before any browser test text.
- [Caret regression and installer run 39940492216](https://github.com/31916/ImeLayoutRouter/actions/runs/39940492216)
  passed at `539981d`: 25 policy/settings and 43 native checks, plus installer
  lifecycle validation. Windows can expose the fallback composition surface's
  own caret through `GetGUIThreadInfo`. Treating that as the application's
  insertion point incorrectly exempted the corner popup. A native regression
  [failed before the fix](https://github.com/31916/ImeLayoutRouter/actions/runs/39940216170)
  at `86a51bc` and passed after excluding the verified IME surface's internal
  caret. A separate check preserves legitimate application corner carets.
- [Chrome/Edge cold-start run 39940492480](https://github.com/31916/ImeLayoutRouter/actions/runs/39940492480)
  passed at `539981d` with Google Japanese Input on Windows Server 2025. Each
  browser used 12 fresh profiles, alternating V3 off-first/on-first, and passed
  48 real preedit/commit checks across text inputs and textareas. No failed
  input was retried or discarded. Earlier failures exposed separate fixture
  races: reading `DevToolsActivePort` before it was fully written, treating a
  loaded DOM as a visible first frame, and using renderer focus before native
  text input was ready. The fixture now waits for the complete endpoint, checks
  an actually rendered local-page marker, clicks its own input, and verifies
  stable native focus/IME-on before sending the first measured character.
  Repeating an idempotent IME-on setup is bounded and precedes test text; it is
  never a retry of the word. These preparation steps exist only in the fixture.
- [Real Google IME run 39940496381](https://github.com/31916/ImeLayoutRouter/actions/runs/39940496381)
  passed at `539981d` after the caret correction, including candidates,
  cancellation, normal/corner-caret exclusions, focus changes, restoration,
  ten repeated compositions and Chrome input. Reviewed candidate evidence
  retains the conversion list while hiding the fallback frame. Show-event to
  empty-region observations were 47–156 ms, not first-frame latency.
- [Native and installer run 39877751275](https://github.com/31916/ImeLayoutRouter/actions/runs/39877751275)
  passed at `7f21609`: 25 policy/settings checks, 38 native checks, and development
  installer lifecycle checks. All 200 cross-process move/restore/remask cycles
  and 100 custom-region move/restore cycles passed. The ordinary-region
  move-to-mask observations were 31–95 ms, not real IME first-frame latency.
  Japanese/English settings renders retain the existing icon and were visually
  reviewed for missing text, overlap and clipping.
- A rare restoration failure was reproduced in
  [run 39871343642](https://github.com/31916/ImeLayoutRouter/actions/runs/39871343642).
  During a window move, `GetWindowRgn` briefly returned 0 and then 1 for the same
  masked window. The old code mistook the ambiguous 0 for an owner replacement,
  removed its recovery token and left an empty region. The fix keeps ownership
  on an ambiguous query and explicitly restores an originally unbounded window.
  A saved custom region is rechecked after a bounded settling interval. Regression
  tests inject the unavailable query and also verify deliberate owner changes.
  Diagnostic traces and query injection are compiled only into the synthetic
  test executable, never the application or real-IME fixture.
- Native tests use synthetic windows on a separate, non-displayed Win32 desktop.
  They check classification, candidate exclusion, owner-hide/change behavior,
  custom-region restoration, helper EOF, abrupt helper termination, interrupted
  recovery records and bounded recovery batches. **These tests do not establish
  real IME compatibility.**
- Installer tests cover installation, upgrade/uninstall while running, preserving
  saved choices on upgrade, refusing both operations when recovery is incomplete,
  and retaining V1/V2 startup registration. No release installer is uploaded.
- [Google IME and Chrome run 39877751312](https://github.com/31916/ImeLayoutRouter/actions/runs/39877751312)
  passed at `7f21609` on Windows Server 2025 with Google Japanese Input 3.34.6260.0.
  Actual keystrokes produced `にほん`; conversion committed `日本` with V3 off/on.
  The baseline/masked/candidate images show that only the fallback composition
  frame disappeared and candidates remained visible. Cancellation, preserving
  pending text, disabling suppression, normal positioning, a real corner caret,
  switching input threads and ten repeated input cycles passed. The ten
  show-event-to-empty-region observations were 32–125 ms. These measurements include
  event delivery and observation delay; they do not demonstrate zero-frame hiding.
- The same Google IME run passed ordinary Chrome text input and textarea checks:
  both emitted actual composition events and committed the same `日本` result
  with V3 off/on. Chrome uses a unique profile, a local test page, and its verified
  document window. CDP prepares/reads that page; OS keystrokes produce the text.
  This is input compatibility evidence, not coverage of every website.
- [Microsoft IME run 39871172870](https://github.com/31916/ImeLayoutRouter/actions/runs/39871172870)
  passed its independent Chrome text/textarea input stage at `d29a698` on Windows
  Server 2025. Actual preedit was `にほん` and the committed result was `日本`
  with suppression off/on. The separate raw-window fallback stage failed before
  V3 was enabled: the IME immediately committed each kana in that fixture.
  The overall run is failed and does not establish Microsoft fallback-popup
  suppression compatibility.
- [Windows 11 Microsoft IME run 39874656891](https://github.com/31916/ImeLayoutRouter/actions/runs/39874656891)
  passed on ARM64 build 26200 after validating repair revision `7f21609` on the
  same prepared disposable VM. The workflow's initial `e49d608` revision failed
  candidate observation; it is not the passing revision. Both browser and native
  fixture exit codes were 0 at `7f21609`. Real preedit `にほん`, conversion `日本`,
  candidate selection, cancellation, focus changes, disable/restore, normal
  positioning, a real corner caret and ten repeated input cycles passed.
  Candidate APIs exposed no IMM/TSF list or UIA descendants on this image, so the
  fixture compared the rendered panel and actual Up/Enter results. All 1,965
  measured candidate glyph pixels matched with V3 off/on. Full screenshots were
  also reviewed. Show-event-to-empty-region observations were 62–125 ms, not
  first-frame latency. ARM64 evidence does not establish Windows 11 x64 coverage.
  The original repair artifact copied an older `v3-ime-failure.png` forward;
  `revision.txt`, the checks log and baseline/enabled images identify the actual
  result. Later repair collection excludes files predating the tested revision.
- One earlier Google browser baseline at `319fef3`
  ([run 39877451559](https://github.com/31916/ImeLayoutRouter/actions/runs/39877451559))
  failed before V3 was enabled: Chrome received only `honn` with no composition
  events. Independent passes alone did not resolve it. The readiness changes
  and repeated off-first/on-first validation above supersede that fixture.
- Windows 11 x64 real input and real Windows multi-display topology on the
  disposable VM have now been exercised. Actual ACPI sleep/resume remains a release gate. Physical GPUs
  and every monitor model are not covered by VM testing. A passing synthetic test alone
  does not authorize a release installer or download-page update.
- The helper reacts to foreground/window show, hide, destroy and position events,
  with a 100 ms fallback scan and 10 ms event-burst coalescing. Notifications run
  outside the target process and do not inspect text. The helper uses per-monitor
  V2 DPI awareness. A brief initial appearance is still possible; zero-frame
  suppression has not been demonstrated and is not a feature claim.

### Real IME integration fixture

The `V3 real IME compatibility` workflow provisions Japanese basic typing on
disposable GitHub-hosted Windows VMs only. It preserves the VM's display
language. Provisioning is not a passing input test. The separate fixture sends a
fixed test word to its own Win32 window, uses that window's input context to
simulate misplaced composition coordinates, and compares IME conversion results
with suppression off/on. It also checks cancellation, disabling suppression and
normal composition positioning. It stops sending keys if its window loses focus.

Both the provisioning script and fixture refuse to execute outside GitHub-hosted
runners. They must never be run on the user's PC. The fixture can read its own
test composition strings; that code is excluded from the production application.
The workflow retains test evidence, not a release installer. Passing this fixture
alone does not establish compatibility with browsers, games, other IMEs, multiple
monitors or all Windows versions.

The separate `V3 Google IME compatibility` workflow installs a pinned Google
Japanese Input MSI from Google's download service. It checks both SHA-256 and
the Google Authenticode signature before installation. All installation and
input testing stays on the disposable VM. Google Japanese Input reproduced the
corner popup as a `CiceroUIWndFrame` containing a visible `MSCTFIME Composition`
child; checking top-level composition windows alone missed this real case.
The classifier now verifies that frame's IME owner and composition children.
Candidate windows and unrelated Cicero frames are excluded.

Microsoft IME capability provisioning runs only when manually dispatched.
Windows Update downloads and cumulative-update servicing can take many minutes
on hosted images. A provisioning timeout is an environment failure, not a passed
IME compatibility test; its CBS/DISM evidence is retained separately.
Before downloading input packages, the workflow checks whether its own fixture
can receive focus. Windows 11 hosted images can show a first-logon privacy page;
the preparation script applies Microsoft's managed-device policy and identifies
that page's host by package identity. Any focus click must land on the fixture's
own uncovered client area. These steps are restricted to the disposable VM.
Ordinary browser typing and fallback-popup reproduction run separately; passing
one never substitutes for the other.
Modern Microsoft IME candidates can be absent from the legacy IMM candidate API.
The fixture can also read `ITfCandidateListUIElement` on its own TSF thread,
checking visibility, candidate text and selection without changing the IME UI.
If neither candidate API supplies a list, the fixture checks the Microsoft input
host's accessibility tree from an MTA thread while pumping the owning STA. If
that tree is also empty, the fixed 1024×768 fixture compares a candidate-only
screen rectangle outside the preedit popup and test window. It requires actual
glyph changes after conversion, at least 500 dark glyph pixels and preservation
of at least 95% of baseline glyph pixels/count, plus the expected second-to-first
candidate selection and committed result. This is a fixture-specific visual
regression check, not OCR or a universal candidate detector. Each display mode
establishes its own V3-off reference before comparing V3-on, because Windows
can shift the candidate panel when a window enters fullscreen. The full before/
after screenshots remain subject to human review. All this observation code is
excluded from the application.

An optional, explicitly dispatched 10-minute repair window keeps the prepared
VM available for new V3 revisions after a failure. Each exact revision and result
is recorded separately. The workflow passes only if both input tests pass for
the initial revision or one recorded repair revision; failed evidence is retained.

The official Windows 11 x64 evaluation workflow runs inside a disposable KVM
guest with no incoming network port. Only that guest receives input, display
changes and sleep requests. Official ISO and display-driver downloads are
hash-checked; the guest also checks the driver publisher signature. The preceding
Windows build job requires a valid Google Authenticode signature on Chrome, then
both the Linux host and guest verify the SHA-256 of that exact transferred file.
This avoids an unbounded signature query observed during fresh guest startup;
the browser still requires both successful signature validation and matching
file hashes. The long-running fixture starts independently so Windows can finish
its first logon before input tests proceed.
After an input failure, it keeps the prepared VM for up to ten minutes to test a
changed public V3 revision. The failed revision's logs remain separate, and a
passing revision must complete its own full input and two-cycle ACPI checks.
No guest disk, credentials, installer or OS ISO is uploaded as evidence.

The hosted ARM image has a separately reported
[WSL updater focus-stealing defect](https://github.com/actions/runner-images/issues/14264).
The preparation script's workaround is restricted to this repository's
GitHub-hosted ARM VM. It is not compiled into V3 or applied to a developer PC.

### Reviewed screenshots (2026-10-23–24 JST)

These contain only fixed fixture input on disposable VMs. They are evidence for
the tested environment, not a promise for every application.

| IME | V3 off | V3 on |
| --- | --- | --- |
| Microsoft Japanese IME / Windows 11 ARM | [Composition and candidates](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/microsoft-before.png) | [Candidates preserved, composition hidden](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/microsoft-after.png) |
| Microsoft Japanese IME / borderless fullscreen fixture | [Composition and candidates](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/microsoft-fullscreen-before.png) | [Candidates preserved, composition hidden](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/microsoft-fullscreen-after.png) |
| Google Japanese Input / Windows Server 2025 | [Composition and candidates](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/google-before.png) | [Candidates preserved, composition hidden](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/google-after.png) |
| Google Japanese Input / borderless fullscreen fixture | [Composition and candidates](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/google-fullscreen-before.png) | [Candidates preserved, composition hidden](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/google-fullscreen-after.png) |

## Sources / applicability

- [Microsoft: SetDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig)
  documents extended-topology activation used only by the disposable fixture.
- [SPICE: multiple monitors](https://www.spice-space.org/multiple-monitors.html)
  describes the separate QXL devices used for the Windows test guest.
- [Microsoft: composition and candidate windows](https://learn.microsoft.com/en-us/windows/win32/intl/status--composition--and-candidates-windows)
- [Microsoft: WM_IME_SETCONTEXT](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-setcontext)
  describes in-application UI control; it is not a universal external suppression switch.
- [Microsoft: SetWindowRgn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn)
  defines region ownership and drawing behavior used here.
- [Microsoft: SetProp and UIPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setpropw)
  limits window-property changes to processes at the same or a lower integrity level.
- [Microsoft: LoadKeyboardLayout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-loadkeyboardlayoutw)
  requires the caller to own keyboard focus on Windows 8 and later; the browser
  fixture prepares its input language before handing focus to Chrome.
- [Microsoft: TSF candidate UI](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nn-msctf-itfcandidatelistuielement)
  supplies candidate count, selection and strings for the integration fixture.
- [Korean IME top-left composition report in WebView2](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5675)
  confirms that the symptom is not limited to Japanese. Chinese IMEs also use
  composition, but Chinese behavior has not been validated in this project.
