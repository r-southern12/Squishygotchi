# Squishy Dumpling (working title)

A Tamagotchi-style mobile game: a squishy dumpling pet lives inside a 3D bamboo steamer room that the player furnishes. Players care for it, cook for it, decorate its room and open mystery steamers. Neglect can kill it.

## Sources of truth

- `docs/spec.md` is the game design spec. Follow it. Its numbers are starting points for tuning; keep them in data, not hard-coded.
- `reference/steamer-room.html` is the working browser prototype. **The Unity game must replicate it exactly**: every system, model (same shapes, sizes, colours, patterns), HUD and panel (same layout at a 375×812 reference, fonts, icons, colours), animation and number. Port its logic and geometry faithfully, translated into clean Unity code (don't copy its one-file structure). Numbers still live in data assets. No simplified stand-ins. (Decided 25 Sep 2026.)
- If the spec and the prototype disagree, the spec wins. If the spec is silent, ask before inventing a system.

## Engine and platform

- Engine: **Unity 6 (LTS) with URP and C#**, unless told otherwise. (If the project switches to Godot 4, update this section first.)
- Targets: iOS and Android, **portrait only**.
- Performance budget: 60 fps target, 30 fps minimum on mid-range phones from about 2021. One full-screen post effect (tilt-shift + colour grade + flash combined). Cheap lit materials for scenery; the higher-quality material only on the squishy. Instanced, pooled particles. Adaptive resolution.

## Architecture rules

- **Data-driven content.** Styles, item types, skins, squishy finishes, recipes, ingredients, snacks, tools, tool skins, tasks, shop prices, room levels and drop tables live in data assets (ScriptableObjects or JSON), not in code. Adding a style or recipe should never need a code change.
- **Simulation separate from presentation.** Needs, economy, gacha, tasks and progression are plain C# with no Unity scene dependencies, so they can be unit-tested and run in fast-forward.
- **Time-based needs.** Store timestamps and compute drain on resume (offline progress). Plan for server time later to stop clock cheating; keep a single `IClock` abstraction.
- **Deterministic gacha.** The drop table and pity counters are data plus a seeded RNG, with a test that simulates 10,000 opens and checks rates (Common 76 / Rare 18 / Epic 5 / Legendary 1; pity Rare+ in 10, Epic+ in 50).
- **Pieces vs skins.** An item *piece* has a type and a current *skin*; skins are unlocked separately. Stations max one per room (stools two); decor limited by room level slots.
- **Save system** from the start: versioned save data, local first; cloud sync later.

## Project layout

- `Assets/_Project/Scripts/Simulation` (`Squishy.Simulation`, no Unity references): `Game/` holds content records (`GameContent`), saved state (`GameState`) and all rules (`GameRules`: needs, comfort, kitchen, gacha with pity, tasks, wallet); plus save data/migrations, `IClock`, `Pcg32`.
- `Assets/_Project/Resources/Content/game_content.json`: all content and tuning numbers (styles, item types, finishes, tools, recipes, tasks, rules, starter room).
- `Assets/_Project/Scripts/Runtime` (`Squishy.Runtime`): the faithful prototype port. `Three/` (three.js geometry, materials, canvas-painted textures; the world sits under a root scaled (1,1,-1) so the prototype's numbers copy across unchanged), `Models/` (steamer, items, kitchen, squishy), `World/` (particles, ray picking, lighting, post globals), `Game/` (`SteamerGame` partials: Home, View, Unbox, Panels; thumbnails, sound), `UI/` (UI Toolkit HUD at the 375px CSS scale).
- `Assets/_Project/Shaders`: `ThreeLit` (three.js r128 Lambert/Standard lighting), `ThreeBasic`, `Sparkles`, `TiltShiftGrade` (the one full-screen pass).
- `Assets/_Project/Scripts/Editor`: `Squishy > Setup` (portrait, URP, tilt-shift pass, scene). Batch: `-executeMethod Squishy.EditorTools.ProjectSetup.Batch`. `Assets/_Project/Tests/EditMode`: NUnit tests.
- `Legacy/` (outside Assets, ignored by Unity): the pre-port stand-in code, kept for reference.
- Content ids are lowercase snake_case strings; saves store ids, never asset references. Bump `SaveMigrator.CurrentVersion` and add a migration when save fields change meaning.

## Art direction

Soft-block diorama with a tilt-shift miniature look. Chunky, soft-edged, matte furniture; muted warm palette (terracotta, sage, cream, dusty teal, mustard); the squishy is the one soft, round, glossy thing in every frame. The steamer walls lower on the camera side so the room is always visible.

## Design pillars (check every feature against these)

1. One squishy you love.
2. Caring is the player's job (autonomy caps needs at about 50%).
3. Every pull matters.
4. Every item has a purpose; no filler.
5. Your room is yours; forgiving placement.
6. Fair and calm: published odds, pity, no forced ads.

## Working agreements

- Work in small, playable steps. After each step the game should run.
- Write unit tests for simulation code (needs, economy, gacha, recipes, tool wear, room limits).
- Commit after each working step with a clear message.
- Ask before adding any third-party SDK (ads, analytics, IAP, notifications) or anything that touches money or player data.
- The audience skews young: no open chat, no dark patterns, odds always visible.
- Original IP only. Never use the "Squishy Bun" name, RMS USA's smiley-bao design, or other brands' characters.
- Placeholder art is fine; mark it clearly (for example a `Placeholder` folder) so it's easy to replace.

## Milestones

1. **Project setup:** Unity project, URP mobile settings, folder structure, data asset types, save system skeleton, test assembly.
2. **Room and squishy:** steamer room with cut-away walls, the squishy hopping around, follow camera (turn, tilt, zoom), tilt-shift post effect.
3. **Care:** four needs, autonomy cap, stations and activities, decline stages, death and next generation.
4. **Arrange:** free placement with soft collision, wall snap, rotate, undo, storage, skins strip, room levels and decor slots.
5. **Kitchen:** recipes, Cook menu, tool on stove animation, mastery stars, tool wear and repair, snacks.
6. **Steamers:** hold-to-charge reveal, drop table with pity, reveal plate and card, odds screen.
7. **Economy:** coins, care tasks, happy income, shop.
8. **Catalogue:** content pipeline for 24 styles × 19 types, rendered thumbnails, style preview.
9. **Polish and performance:** haptics, sound, adaptive resolution, device testing.
10. **Store prep:** IAP (cosmetics only unless decided otherwise), privacy, age rating, odds disclosure.

## Glossary

- **Piece:** one placeable item of a type (a bed). **Skin:** its look (Moroccan Riad bed).
- **Station:** an item the squishy uses to fill a need. **Decor:** adds Comfort; limited by room level.
- **Comfort:** slows need drain (2% per point, max 40%) and raises happy income.
- **Steamer:** the mystery box. **Pity:** guaranteed rarity after a run of lower pulls.
- **Favourite:** the squishy living in the room; copies of it make it grow.
