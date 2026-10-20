# V1 / V2 distribution plan

Ver1.1.2 and Ver2.0.2 are the patch versions prepared on 2026-10-20 for the
accessibility-monitor crash and the prototype startup migration in CHANGELOG.md.
Until their validated installers are deployed, the public files remain
Ver1.1.1 and Ver2.0.1 from 2026-09-26. Browser end-to-end and some IME compatibility
checks remain incomplete. Keep those limitations visible in both website languages.

## Editions and branches

| | V1 | V2 (default branch) |
| --- | --- | --- |
| Patch version | 1.1.2 | 2.0.2 |
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
6. Publish the prepared download tables, release articles and announcements only
   after both new EXE files and their verified SHA-256 entries are present. Keep
   all previous download files and Actions artifacts available.

Remaining IME checks require a suitable Windows test environment with the relevant
input methods enabled. An immediate
first key can precede asynchronous routing; do not claim universal compatibility.

## Hosting

- Use the existing GitHub Pages publication at https://31916.ch/IMELayoutRouter/.
  Keep the existing forest-green/ivory design and Japanese/English pages.
- Publish installers and SHA256SUMS.txt under IMELayoutRouter/downloads/ in the
  portfolio repository. Do not add a root navigation link or create GitHub Releases.
- Keep each asset below GitHub's 100 MiB file limit and the site below 1 GB;
  use Git, not LFS. Do not buy services or change DNS/hosting for this release.
- The portfolio repository owns the production pages and its Pages build. This
  application repository retains the original icon, social image sources and
  release articles, but no separate website build. Update production pages and
  verified binaries together when publishing a new version.

See [the roadmap](roadmap.md) for later work.
