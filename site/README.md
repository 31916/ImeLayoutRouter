# Distribution website

A small Japanese/English static site inspired by the information hierarchy of classic
software distribution pages. Original text and styling; no copied assets.

Run `npm run site:preview` and open `http://127.0.0.1:4173/preview.html` to compare
three palettes. The palette picker is a review tool and is excluded from deploys.
The user selected the forest-green-on-ivory palette (B). Japanese lives at `/`
and English at `en.html`; both link to each other without
automatic language redirects. Keep availability and limitations equivalent.
The production pages need no executable JavaScript, external fonts, analytics or
framework. JSON-LD in the head describes the public page and product for crawlers.

Use the existing `Assets/app.ico` for the page masthead and favicon, matching the
application, settings windows, tray and installer. The site build copies this
canonical asset byte-for-byte to `app.ico`; do not replace it with a new design.

Social link previews use the checked-in 1200 x 630 PNG files `social-ja-v2.png`
and `social-en-v2.png`, composed from the same original icon and site palette.
Describe the purpose as "IMEの直接入力を、指定した配列で" / "Your chosen layout
for IME direct input", not a broad claim of automatic keyboard layout switching.
Explain that the router selects an existing, enabled Windows input layout for
the application receiving input. The active layout and resulting characters can
change; do not claim that layouts never change. Distinguish this from creating or
editing individual key mappings, layout definitions or keyboard drivers, which
the app does not do. Keep the brief clarification below the download table and
the detailed explanation in the README and edition notes, in both languages.
The image revision suffix is separate from the application's V1/V2 editions.
Keep the original `social-*-v1.png` URLs serving the corrected artwork too, for
previously cached cards that still reference them.
Regenerate them on Windows with
`powershell -NoProfile -ExecutionPolicy Bypass -File tools/generate-social-images.ps1`
(uses built-in System.Drawing and the Windows Georgia/Meiryo fonts).
Both pages declare absolute HTTPS Open Graph image URLs and X/Twitter large-image
cards with matching language-specific alternative text. These images are metadata
assets, not added page content. When the artwork changes, use a new filename and
update both the metadata and build/preview allowlists to avoid stale image caches.
The portfolio's legacy redirect pages inherit the canonical page's sharing metadata.

Build with `npm run site:build`. Static output directory: `.site-dist`.
Build the palette comparison with `npm run site:build -- --review`; its separate
`.site-review-dist` output adds noindex headers and is deployed to a preview branch.
The application repository's default branch is `V2`. The requested public download
page belongs under `31916.ch/IMELayoutRouter/` in the existing portfolio repository;
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
Update the stylesheet URL version in both pages when changing CSS so returning
visitors do not combine the new HTML with their cached previous design.
History dates must have Git evidence and distinguish implementation from release.

Use the exact product spelling `IMELayoutRouter` in titles, headings and descriptions;
retain `IME Layout Router` as an alternate name in metadata. Titles and descriptions
should explain that this is a Windows IME/keyboard-layout utility. Keep reciprocal
Japanese/English `hreflang` links fully qualified and each page self-canonical.
`sitemap.xml` lists only the two current canonical pages. Update its `lastmod` dates
only when the corresponding pages materially change, not on every deployment.
The portfolio's root `robots.txt` advertises this sitemap and allows crawling.
Keep the portfolio homepage free of a new navigation link, as requested.
WebPage JSON-LD describes existing visible content; do not invent ratings, reviews,
downloads or compatibility claims to obtain software rich results.

Production uses the existing GitHub Pages site, not the historical Cloudflare
design preview. The portfolio repository's Pages workflow packages the static
files with `tools/build-pages.py`. The canonical path is `/IMELayoutRouter/`;
legacy `/IMELayOutRouter/` pages redirect to it, while old direct asset and installer
URLs remain available. Aliases exist only in the deployment archive so Windows
checkouts do not need two directories that differ only in letter case.
The user has retired the prototype GitHub Release. Do not create
new GitHub releases. After final package checks, place installers under
`IMELayoutRouter/downloads/` in the portfolio repository and link to them relatively.
Keep individual files below 100 MiB and the complete site below 1 GB. Include the
actual file size and SHA-256 in the linked articles; require final-version
installer lifecycle checks when changing a package. A page-only edit does not
change the installer or its release date.

Before release: use actual versions/assets/checksums, publish verification results,
and check the download links after upload. See `docs/release-plan.md`.

Limits: https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits
and https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github.
