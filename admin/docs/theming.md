# Admin UI Theming

## Architecture

CSS custom properties (variables) for systematic theming without touching component markup.

```
app.css                  <- structural layout (stays across themes)
themes/amber-crt.css     <- variable definitions + overrides for amber terminal
themes/x11-twm.css       <- future: swap this in for a different look
```

Each theme file defines the same set of variables (`--bg`, `--fg`, `--border`, `--font-family`, etc.) with different values. The structural CSS references only variables, never hardcoded colors or fonts. Adding a new theme = writing a new set of variable values.

Bootstrap 5 CSS variables can be overridden at the `:root` level.

## First Theme: Amber CRT Terminal

Aesthetic: amber monochrome monitor, 80x50 PC style.

- **Font**: VT323 from Google Fonts (retro/pixelated, true to 80x50 CRT) or IBM Plex Mono (cleaner, more readable) -- TBD
- **Palette**: amber `#FFB000` on `#0A0A0A`, dim amber for secondary text
- **Borders**: 1px solid, no rounded corners, no shadows, no gradients
- **Accents**: reverse video (amber bg / black text) for buttons and active states
- **Optional**: subtle `text-shadow` glow for CRT authenticity

## Future Themes

Designed so that alternative themes (e.g., X11 twm style) can be added by creating a new CSS file with the same variable set and different values.
