# Website assets and publication

The production download pages are maintained in
[31916/31916.github.io](https://github.com/31916/31916.github.io/tree/main/IMELayoutRouter),
and published by that repository's GitHub Pages workflow:

- Japanese: https://31916.ch/IMELayoutRouter/
- English: https://31916.ch/IMELayoutRouter/en.html

This application repository contains the original icon, social sharing images,
their generator and version articles. It does not build or deploy a second copy
of the website. The obsolete HTML, palette comparison and npm website commands
have been removed to prevent an older download page from being published again.
Existing installers are maintained in the website repository and are unaffected.

## Shared artwork

Use `Assets/app.ico` unchanged in the application and website. The social images
are 1200 x 630 PNG files composed from this icon and the forest-green/ivory palette:

- `site/social-ja-v2.png`
- `site/social-en-v2.png`

The `social-*-v1.png` files intentionally contain the same corrected artwork;
previously shared cards may still request those URLs. Keep these compatibility
assets even though their contents are duplicated. The suffix identifies the
image revision, not the application's V1/V2 edition.

Regenerate the images on Windows with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/generate-social-images.ps1
```

The generator uses System.Drawing and the Windows Georgia/Meiryo fonts. Review
the result and copy approved assets into the website repository. When changing
the image revision, update the Japanese/English sharing metadata and explicit
publication allowlists there. Preserve older image URLs for cached cards.

## Product wording and page design

Use the exact product name `IMELayoutRouter`. Describe its purpose as
"IMEの直接入力を、指定した配列で" / "Your chosen layout for IME direct input".
The application selects an existing Windows input layout for the application
receiving input; the active layout and resulting characters can change. It does
not create or edit individual key mappings, layout definitions or drivers.

Keep the compact download table visible immediately after the introduction.
Its columns are installer, version and release date. Show at most three short,
dated news items, and link to the Japanese/English articles in `docs/releases/`
for features, installation steps, checksums and compatibility limits. Keep those
limits equivalent in both languages, including tests that remain incomplete.

The canonical path is `/IMELayoutRouter/`. The website build retains legacy
`/IMELayOutRouter/` redirects and old direct asset/download URLs. Canonical URLs,
reciprocal language links, sharing metadata and sitemaps belong to the production
website. Do not manufacture reviews, ratings or compatibility claims for search.

## Publishing a version

1. Complete the package checks in [the release plan](../docs/release-plan.md).
2. Copy the verified installers into `IMELayoutRouter/downloads/` in the website
   repository. Keep their existing version-specific filenames and SHA-256 records.
3. Update the production download table and version articles with matching
   versions and dates. A page-only change does not change an installer release date.
4. After publication, verify HTTPS downloads against the recorded hashes.

GitHub Releases is not used for distribution. The website packager uses an
explicit allowlist; do not recursively publish a working directory containing
local logs or credentials. Retain existing downloadable installers and archived
version articles when adding a release.
