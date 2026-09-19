# V1 / V2 distribution plan

This plan supersedes the previously prepared public v3 preview release. No v3
GitHub release has been published. Do not publish that prepared package under a
new V1/V2 filename: the edition behavior, assembly version and installer metadata
must first agree with the release.

## Intended editions (scope awaiting confirmation)

| | V1 Japanese simple edition | V2 full edition |
| --- | --- | --- |
| Source IMEs | Japanese | Japanese, Chinese regional variants, Korean |
| Routing engine | Shared repaired implementation | Same repaired implementation |
| Settings | Japanese UI, source/target and startup essentials | All enabled CJK routing and full controls |
| Quality | Real Japanese/browser acceptance tests required | Full CJK/browser acceptance tests required |
| Suggested first public version | 1.1.0, preserving old v1.0.0 | 2.0.0 after acceptance |

Keep common fixes in one source tree, with explicit build edition selection.
Do not maintain diverging copies of the routing engine. Test edition restrictions,
version/resource metadata, installer identity and v1 settings migration. Choose
one active instance across editions; document upgrade/downgrade behavior and keep
settings backup until cross-edition migration has been verified.

V1 is a reduced feature set, not a relaxed quality bar. The old v1.0.0 has the
email-field bug and is not the new simple edition. Earlier 2.0/3.0 preview version
numbers describe internal milestones; keep those historical commits intact.

## Release gates

1. Confirm edition scope. The user selected palette B (forest green on ivory)
   and requested an English website. Build both pages on Cloudflare.
2. Implement the Japanese simple build and full build with consistent versions.
3. Run automated regression tests on both builds and verify packaged assets.
4. In an interactive Windows session, validate actual text composition, target
   punctuation/AltGr, email/URL/password/number fields and restoration across apps.
   Test Google and Microsoft Japanese IMEs for V1. Add Microsoft Pinyin, Bopomofo
   and Korean IME for V2. Exercise Edge/Chrome, Firefox and a native editor.
5. Verify settings migration, startup, single instance, pause, diagnostics, install,
   upgrade and uninstall. Do not disable Windows application-control protections.
6. Mark unsupported configurations explicitly. No synthetic test is evidence that
   all applications or all IMEs work. Current desktop test cannot acquire foreground
   focus in the agent environment; this is unresolved, not a passing result.
7. Upload reviewed packages, check SHA-256 and download links, then update the site
   from preparation status to available. V1 may ship first if V2 is still unverified.

## Hosting and presentation

- Cloudflare Pages for static HTML/CSS; no framework or account required to download.
- Simple title, information, separate V1/V2 tables, usage, compatibility, history.
- Selected palette: forest green on ivory (B). Japanese and English site pages.
- No fabricated release dates, success metrics or working download links before assets exist.
- Current packages exceed Pages' 25 MiB asset limit. Keep a dedicated website as the
  user-facing download destination, with GitHub release assets as storage, or choose
  R2 explicitly. R2 billing and public bucket setup are separate decisions.
- Use existing Cloudflare credentials only through the supported CLI; never commit
  credentials. Domain choice remains open. Do not purchase a domain or enable a
  paid service without explicit authorization.

See [the feature roadmap](roadmap.md) for proposed priorities beyond these releases.
