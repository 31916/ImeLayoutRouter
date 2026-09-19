# V1 / V2 distribution plan

This plan supersedes the previously prepared public v3 preview release. No v3
GitHub release has been published. Do not publish that prepared package under a
new V1/V2 filename: the edition behavior, assembly version and installer metadata
must first agree with the release.

## Confirmed editions and branches

`V2` is the GitHub default branch (full edition); `V1` is the Japanese simple
edition branch. Both keep the common engine and edition build selection. The
default `Edition` in `Directory.Build.props` is `Full` on V2 and `Simple` on V1.
Preserve this intentional difference when transferring shared fixes.
Branch publication is not release approval. Neither stable nor release-candidate
downloads may be published before the required real-machine checks pass.

| | V1 Japanese simple edition | V2 full edition |
| --- | --- | --- |
| Source IMEs | Japanese | Japanese, Chinese regional variants, Korean |
| Routing engine | Shared repaired implementation | Same repaired implementation |
| Settings | Japanese UI, source/target and startup essentials | All enabled CJK routing and full controls |
| Quality | Real Japanese/browser acceptance tests required | Full CJK/browser acceptance tests required |
| Suggested first public version | 1.1.0 after acceptance | 2.0.0 after acceptance |

Keep common fixes in one source tree, with explicit build edition selection.
Do not maintain diverging copies of the routing engine. Test edition restrictions,
version/resource metadata, installer identity and v1 settings migration. Choose
one active instance across editions; document upgrade/downgrade behavior and keep
settings backup until cross-edition migration has been verified.

V1 is a reduced feature set, not a relaxed quality bar. The old v1.0.0 has the
email-field bug and is not the new simple edition. Earlier 2.0/3.0 preview version
numbers describe internal milestones; keep those historical commits intact.

## Release gates

1. Use the confirmed edition scope, palette B (forest green on ivory), and Japanese
   and English download pages at `31916.ch/IMELayOutRouter/`. Do not add a portfolio link.
2. Implement the Japanese simple build and full build with consistent versions.
3. Run automated regression tests on both builds and verify packaged assets.
4. In an interactive Windows session, validate actual text composition, target
   punctuation/AltGr, email/URL/password/number fields and restoration across apps.
   Test Google and Microsoft Japanese IMEs for V1. Add Microsoft Pinyin, Bopomofo
   and Korean IME for V2. Exercise Edge/Chrome, Firefox and a native editor.
5. Verify settings migration, startup, single instance, pause, diagnostics, install,
   upgrade and uninstall. Do not disable Windows application-control protections.
6. Mark unsupported configurations explicitly. No synthetic test is evidence that
   all applications or all IMEs work. On 2026-09-20 both edition fixtures passed
   Japanese native/direct routing, explicit native restoration and password metadata.
   V2 also passed live exclusion, pause/resume and manual switching/restoration.
   Browser URL detection blocked the subsequent browser automation. Browser typing,
   Chinese/Korean IMEs, installer lifecycle and the remaining matrix are still unverified.
7. Upload reviewed packages, check SHA-256 and download links, then update the site
   from preparation status to available. V1 may ship first if V2 is still unverified.

The [2026-09-20 acceptance record](acceptance-2026-09-20.md) tracks current evidence.
The user declined adding input methods to this PC. Keep its input list unchanged;
use another suitable test environment for the remaining IMEs. Do not weaken the gate.

## Hosting and presentation

- Use the existing `31916.ch` GitHub Pages publication for the requested subdirectory.
  The Cloudflare design preview is separate. No framework or account is needed to download.
- Simple title, information, separate V1/V2 tables, usage, compatibility, history.
- Selected palette: forest green on ivory (B). Japanese and English site pages.
- No fabricated release dates, success metrics or working download links before assets exist.
- Do not create GitHub Releases. The user retired and deleted the prototype release.
- Publish validated installers directly under `IMELayOutRouter/downloads/` on the
  existing GitHub Pages site. Check each file is below Git's 100 MiB limit and that
  the complete published site stays below 1 GB. The previous note incorrectly
  applied a 25 MiB upload limit to GitHub Pages; 25 MiB is the browser upload limit,
  not a Pages asset limit. Use Git to publish and do not use LFS for download assets.
  References: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github
  and https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits
- An informational page may show the honest validation status before installers
  are ready. Do not expose unvalidated packages, even under unlinked URLs.
- Do not add a homepage navigation entry, purchase a domain, or enable a paid service.

See [the feature roadmap](roadmap.md) for proposed priorities beyond these releases.
