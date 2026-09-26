# Browser field detection correction — Ver1.1.1 / Ver2.0.1

## Defect and correction

The old `FocusedInputProbe` followed MSAA focus, checked `STATE_SYSTEM_FOCUSED`
and `STATE_SYSTEM_READONLY`, and only then requested IAccessible2 metadata.
That ordering can reject a browser's initial document/host placeholder before
requesting the information needed to expose the actual focused input.

Chromium's accessibility implementation activates different levels of support
when APIs are used. Minimal state access enables native APIs; properties on web
content enable basic web accessibility, and extended properties enable additional
metadata. See the upstream implementation:

- [Chromium accessibility activation](https://github.com/chromium/chromium/blob/main/content/browser/accessibility/browser_accessibility_state_impl.cc)
- [Chromium Windows accessibility API](https://github.com/chromium/chromium/blob/main/ui/accessibility/platform/ax_platform_node_win.cc)

The shared V1/V2 probe now requests IA2 metadata at every node in the focus chain
before deciding that no usable input is present. Later polling handles asynchronous
tree population. Only the final node's metadata can be used for routing, and that
node must still be focused, enabled and writable. Unknown or cyclic providers fail
closed. No field values, labels, page text, URLs or keystrokes are logged.

This fixes a reproducible detection-protocol defect. It is not yet proof that this
was the only cause of the reported failure on the contact page.

## Verification on 2026-09-26

- The reported contact email field was inspected in Codex's browser and Chrome.
  It is an enabled, writable `input type="email"`.
- The installed version is V2 2.0.0. Its saved configuration has no app exclusions.
- A lazy accessibility-provider regression fails under the original call order
  (`Unknown` instead of `Direct`) and passes with the correction.
- All 35 Full regression scenarios pass, including five new cases: lazy provider
  initialization, unfocused placeholders, ancestor metadata isolation, providers
  without IA2, and cyclic focus chains.
- Release builds for both Full and Simple pass with warnings treated as errors.
- Noninteractive settings checks pass for both editions, including V2 Japanese /
  English settings and V1's restricted feature set.
- The Windows automation tool stopped because it could not confidently determine
  Chrome's current URL. No physical-key end-to-end test was completed. Browser DOM
  focus checks and test doubles are not substitutes for that test.

The initial investigation did not replace public installers or the installed
application. The owner subsequently approved publishing this correction as
Ver1.1.1 / Ver2.0.1 on September 26 with the remaining test limitation disclosed.
The existing installed application is not silently upgraded by website publication.

## Commands

```powershell
dotnet build -c Release -p:Edition=Full --warnaserror
dotnet build -c Release -p:Edition=Simple --warnaserror
dotnet build tests/RoutingTests.csproj -c Release -p:Edition=Full --warnaserror
dotnet tests/bin/Full/Release/net8.0-windows/RoutingTests.dll
# For each edition (Full and Simple):
dotnet build tests/UiSmoke/UiSmoke.csproj -c Release -p:Edition=Full --warnaserror
dotnet tests/UiSmoke/bin/Full/Release/net8.0-windows/UiSmoke.dll
```

The optional `WindowsSmoke --field-metadata <HWND-decimal>` diagnostic reads only
the specified window's focus handles, API result codes, structural states, field
classification and element identifiers. It never switches layouts or types text.

## Remaining acceptance check

On a fresh browser session with only the patched router providing accessibility
requests, switch from a Japanese text field to the reported email field and verify
the configured target layout and actual punctuation keys. Repeat after focus changes
and page navigation, and confirm ordinary Japanese composition is preserved.
This check remains outstanding after the approved publication. Do not describe
the reported browser-input issue as confirmed resolved until it passes.
