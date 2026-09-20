# V1 / V2 distribution plan

On 2026-09-20 the owner explicitly approved publication after being told that
browser automation was blocked and real-machine compatibility coverage remained
incomplete. This supersedes the previous instruction to wait for the entire
acceptance matrix. It does not mark unrun tests as passed. Record and prominently
publish the remaining limitations in both website languages.

## Editions and branches

| | V1 | V2 (default branch) |
| --- | --- | --- |
| Version | 1.1.0 | 2.0.0 |
| Build edition | Simple | Full |
| Source IMEs | Japanese | Japanese, Chinese regional variants, Korean |
| UI | Japanese essentials | Japanese/English guided settings |
| Controls | Source, target, startup | Application rules, status, shortcuts, diagnostics, typing check |

Both editions share the repaired engine. V1 excludes advanced UI and runtime
modules at compilation. Preserve the intentional default Edition difference in
Directory.Build.props when merging shared changes. The old prototype 1.0.0 and
internal 2.0/3.0 previews are not these distribution packages. Never rename an
old package to a new version: assembly, settings UI and installer versions must agree.

## Publication checks

1. Build both versions and run regression/settings tests and installer lifecycle
   checks on Windows. Use the exact successful CI commit's artifacts.
2. Verify final installer metadata, install/reinstall/uninstall, shared instance
   handling and preservation of settings and other-edition startup registration.
3. Preserve Assets/app.ico byte-for-byte in the app and website.
4. Publish actual file sizes and SHA-256, then verify HTTPS download integrity.
5. Keep the [acceptance record](acceptance-2026-09-20.md) honest: Google Japanese
   Input / Swiss German layout composition passed in native fixtures; browser
   fields and Microsoft Japanese/Chinese/Korean IMEs are unverified. Startup at
   sign-in, diagnostic-save UI and every historical upgrade path are also pending.

The user declined adding input methods to this PC. Keep its input list unchanged.
Remaining IME checks require a suitable separate Windows environment. An immediate
first key can precede asynchronous routing; do not claim universal compatibility.

## Hosting

- Use the existing GitHub Pages publication at https://31916.ch/IMELayOutRouter/.
  Keep the existing forest-green/ivory design and Japanese/English pages.
- Publish installers and SHA256SUMS.txt under IMELayOutRouter/downloads/ in the
  portfolio repository. Do not add a root navigation link or create GitHub Releases.
- Keep each asset below GitHub's 100 MiB file limit and the site below 1 GB;
  use Git, not LFS. Do not buy services or change DNS/hosting for this release.
- The local site build produces page assets; verified binaries are copied into
  the portfolio subdirectory separately. Update pages and binaries together.

See [the roadmap](roadmap.md) for later work.
