# Distribution website

A small Japanese/English static site inspired by the information hierarchy of classic
software distribution pages. Original text and styling; no copied assets.

Run `npm run site:preview` and open `http://127.0.0.1:4173/preview.html` to compare
three palettes. The palette picker is a review tool and is excluded from deploys.
The user selected the forest-green-on-ivory palette (B). Japanese lives at `/`
and English at `en.html`; both link to each other without
automatic language redirects. Keep availability and limitations equivalent.
The production pages need no JavaScript, external fonts, analytics or framework.

Build with `npm run site:build`. Static output directory: `.site-dist`.
Build the palette comparison with `npm run site:build -- --review`; its separate
`.site-review-dist` output adds noindex headers and is deployed to a preview branch.
The application repository's default branch is `V2`. The requested public download
page belongs under `31916.ch/IMELayOutRouter/` in the existing portfolio repository;
do not add a homepage link. Do not publish downloads before desktop acceptance passes.
The site's text honestly marks both new editions as unavailable; it does not
link to nonexistent downloads or present the old v1 test build as the new V1.

Production uses the existing GitHub Pages site, not the historical Cloudflare
design preview. The user has retired the prototype GitHub Release. Do not create
new releases. After desktop acceptance, place validated installers under
`IMELayOutRouter/downloads/` in the portfolio repository and link to them relatively.
Keep individual files below 100 MiB and the complete site below 1 GB. Include the
actual file size and SHA-256; never publish an untested installer, even unlinked.

Before release: confirm hosting/domain, replace preparation
copy with actual verified versions/assets/checksums, publish verification results,
and check the download links after upload. See `docs/release-plan.md`.

Limits: https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits
and https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github.
