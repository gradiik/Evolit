# Evolit UI Icon Set

Canonical SVG library for Evolit.

- Design: Nature Tech + semi-cartoon minimalism.
- Every icon uses `viewBox="0 0 24 24"`.
- Canonical stroke: `1.8`, rounded caps and joins.
- Source color: Mist White `#EAF5F3`.
- Hover/selected tint comes from the Godot theme, normally Evolution Cyan `#20C7D4`.
- Disabled state also comes from the UI theme/modulation.
- Do not create `_hover`, `_selected`, or `_disabled` copies.
- No emoji or Unicode glyphs as production icons.
- No filters, blur, masks, embedded raster images, text, or complex gradients.
- Target readability: 16–20 px; typical rendering: 20–32 px.
- Naming: lowercase snake_case.

Folders: `menu`, `settings`, `actions`, `simulation`, `environment`, `biology`, `resources`, `status`.

When adding an icon, keep one clear silhouette, few paths, the 24×24 grid, and test it at small size first.
