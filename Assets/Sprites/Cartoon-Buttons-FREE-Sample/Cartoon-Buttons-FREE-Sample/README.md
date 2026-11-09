# Cartoon Buttons: free sample

A free sample of **Cartoon Buttons**, a casual cartoon game UI kit (Candy theme, SVG and PNG).

## What is in this sample

- `btn_pill_wide_192x72`: a pill button in **green and orange**, each in all five states (normal, hover, pressed, disabled, focused) = 10 files
- `panel_window_320x256`: a cream window panel with a header strip
- `progress_frame_256x40`, `progress_fill_248x32_green` and `stripe_tile_32x32`: a striped progress bar (frame, fill and the stripe tile that is scrolled to animate it)
- `counter_coin_192x56` and `btn_plus_small_56x64_green_normal`: a resource counter plate and its plus button
- `icon_coin`, `icon_gem`, `icon_heart`, `icon_star` (48 x 48): four chunky icons

That is **20 SVG files** in `svg/candy/` and the same 20 elements as **60 PNG files** (1x, 2x and 3x) in `png/`. `nine_slice.json` lists the margins of the stretchable pieces (the pill button, the window panel, the progress frame and fill, the counter plate). `preview/sample_sheet.png` shows everything. The files are byte-identical to the ones in the full pack.

## Style

One outline weight (4 px), sizes on an 8 px grid, flat shapes only (no gradients, filters, text or logos). A button has a darker lip at the bottom that shrinks when pressed. Interactive files have a 4 px transparent margin around the button for the gold focus ring, so a 192 x 72 canvas holds a 184 x 64 button.

## Using it

- **Stretching:** the pill button and the counter plate are 3-slice horizontal pieces: stretch the width only and keep the height (margins in `nine_slice.json`, in 1x pixels; multiply by 2 or 3 for the 2x and 3x files). The window panel and the progress parts are described there too.
- **Progress bar:** the fill sits inside the frame at offset (4, 4). Show the left part of the fill for the value. Lay the stripe tile over the fill (only where the fill is opaque) and move it 32 px per cycle to animate it.
- **Counter:** put the number in the dark capsule; put the plus button so that its right edge is at the plate's right edge and its top is 4 px above the plate's top.

## Want the rest?

The full pack has 503 elements per theme in three themes, nine button types in seven colours, all controls, bars, toasts, reward slots, 24 icons, a Godot demo project and a recolour script: https://stationsupply.itch.io/cartoon-buttons-ui-kit

## License

See `LICENSE`: use in your own games and apps (commercial too); no resale or redistribution of the assets themselves; no machine-learning training.

## AI disclosure

All graphics are hand-coded SVG vectors; no AI image generation was used. The SVG code was written with an AI assistant.
