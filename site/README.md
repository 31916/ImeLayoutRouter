# Distribution website

A small Japanese static site inspired by the information hierarchy of classic
software distribution pages. Original text and styling; no copied assets.

Run `npm run site:preview` and open `http://127.0.0.1:4173/preview.html` to compare
three palettes. The palette picker is a review tool and is excluded from deploys.
The production page needs no JavaScript, external fonts, analytics or framework.

Build with `npm run site:build`. Cloudflare Pages output directory: `.site-dist`.
Build the palette comparison with `npm run site:build -- --review`; its separate
`.site-review-dist` output adds noindex headers and is deployed to a preview branch.
Use `main` as production branch only after the site and release plan are approved.
The site's text honestly marks both new editions as unavailable; it does not
link to nonexistent downloads or present the old v1 test build as the new V1.

Cloudflare Pages limits each asset to 25 MiB. Current self-contained app packages
exceed that. Host binaries on GitHub Releases initially, with direct download
links from this site, or use Cloudflare R2 after account/billing setup is agreed.
Do not deploy EXE/ZIP packages as Pages assets or change filenames of old tags.

Before release: decide the palette, confirm hosting/domain, replace preparation
copy with actual verified versions/assets/checksums, publish verification results,
and check the download links after upload. See `docs/release-plan.md`.

Sources: https://developers.cloudflare.com/pages/framework-guides/deploy-anything/
and https://developers.cloudflare.com/pages/platform/limits/.
