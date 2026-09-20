# Distribution website

A small Japanese/English static site inspired by the information hierarchy of classic
software distribution pages. Original text and styling; no copied assets.

Run `npm run site:preview` and open `http://127.0.0.1:4173/preview.html` to compare
three palettes. The palette picker is a review tool and is excluded from deploys.
The user selected the forest-green-on-ivory palette (B). Japanese lives at `/`
and English at `en.html`; both link to each other without
automatic language redirects. Keep availability and limitations equivalent.
The production pages need no JavaScript, external fonts, analytics or framework.

Use the existing `Assets/app.ico` for the page masthead and favicon, matching the
application, settings windows, tray and installer. The site build copies this
canonical asset byte-for-byte to `app.ico`; do not replace it with a new design.

Build with `npm run site:build`. Static output directory: `.site-dist`.
Build the palette comparison with `npm run site:build -- --review`; its separate
`.site-review-dist` output adds noindex headers and is deployed to a preview branch.
The application repository's default branch is `V2`. The requested public download
page belongs under `31916.ch/IMELayOutRouter/` in the existing portfolio repository;
do not add a homepage link. The owner approved publication with incomplete
compatibility coverage on 2026-09-20. On 2026-09-21 the owner requested a compact
AviUtl-inspired index. Put downloads immediately after the short introduction,
before news, so both installers are visible on the initial desktop/mobile view.
Keep the download table to installer, version and release date. Keep at most the
three latest news items as short dated lines; put older details in the linked
history article. This is static HTML, without a news widget or JavaScript.
Date each V1/V2 news item and link the edition to its article in
`docs/releases/`. Put feature details, instructions, sizes, SHA-256 and verified /
unverified environments in those Japanese/English GitHub Markdown articles.
The website points readers to the articles for compatibility information.
Keep the original icon and selected palette; no status column or long feature blocks.
History dates must have Git evidence and distinguish implementation from release.

Production uses the existing GitHub Pages site, not the historical Cloudflare
design preview. The user has retired the prototype GitHub Release. Do not create
new GitHub releases. After final package checks, place installers under
`IMELayOutRouter/downloads/` in the portfolio repository and link to them relatively.
Keep individual files below 100 MiB and the complete site below 1 GB. Include the
actual file size and SHA-256 in the linked articles; require final-version
installer lifecycle checks when changing a package. A page-only edit does not
change the installer or its release date.

Before release: use actual versions/assets/checksums, publish verification results,
and check the download links after upload. See `docs/release-plan.md`.

Limits: https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits
and https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github.
