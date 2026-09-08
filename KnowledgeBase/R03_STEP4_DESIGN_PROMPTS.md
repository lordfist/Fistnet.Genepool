# R03 Step 4 — visual concept prompts

Genepool Analyzer · 2026-09-08. Built-in image-generation tool. Concepts only, not production assets or simulation evidence. B and C each received a targeted correction after visual inspection; final proposal uses only the corrected versions.

## A — Board layout

Use case: ui-mockup. Create a polished, credible desktop artificial-life simulator design concept, wide landscape 1920x1080. This is the first of three review mockups for FISTNET GENEPOOL, not an implemented screenshot. Put a small visible "DESIGN CONCEPT A" label at top.

Design direction: a restrained 3D board seen through an orthographic camera looking downward about 55 degrees, with a calm dark slate desktop interface. No fantasy game, no AAA scenery. The board is a flat square grid, no mountains, water, cliffs or elevated terrain columns. A close neighborhood crop of roughly 18 by 15 cells fills about 75% of the window. Let the board reach the viewport edges instead of floating as a small diamond in a huge black void. A tiny minimap indicates that this is part of a larger world. The angled board gives modest depth, while retaining readable spatial relationships.

Food palette: matte earthy brown depleted soil, tan/brown low food, muted olive medium food, soft fresh green plentiful food. Continuous brown-to-green resource scale; no black food cells, no neon green. Flat lightly varied material, subtle cell seams visible only at this neighborhood scale. Very short sparse sprouts or low moss clumps in food-rich cells communicate quantity but never hide organisms. No decorations unrelated to food. Add a small legend with exact text "FOOD", "Empty", "Plentiful" and a brown through olive to green strip.

Organisms: roughly 50 clearly visible, compact rounded microbe bodies in warm ivory, cyan, lilac and amber, each centered in its occupied cell. Strong dark border plus fine pale outer rim, gentle shading, very low dome height, sparse simple internal markings and one small directional tick on selected organism only. They should occupy roughly half a cell, unmistakably larger than tiny pixels. Shapes are abstract cells rather than animals, no eyeballs, mouths, legs, teeth. Colors depict existing DNA identity and must not label biological species or fixed predator classes. One cyan organism is selected with a clean amber ring and one subtle short movement arrow to an adjacent cell. All other organisms remain quiet with NO text labels or health bars.

UI: top concise title "GENEPOOL", "Paused", buttons "Run", "Step", "New world". A second slim toolbar shows "Top-down / Angled", "Food", "Actions: Selected", and a four-position zoom control "World  •  Habitat  •  Organism  •  Inspect" with Habitat active. Narrow right inspector shows "Selected organism", "DNA", "Recent action" and simple readable example rows "Move → East" and "Completed". Small bottom collapsed strip "Population   Food   Activity". No giant graphs or technical debug text. Generous board space, tidy typography. At the bottom small caption "Illustrative layout • not simulation data". Aim for plausible economical Godot graphics, not cinematic realism.

## B — Zoom levels

Use case: ui-mockup / visual design board. Produce a high-quality 1920x1080 landscape four-panel concept sheet for an artificial-life simulation called GENEPOOL. Title "CONCEPT B — FOUR LEVELS OF DETAIL". This is a proposal for review, not a screenshot of running software. Clean dark slate backing with four equally sized panels in a 2 by 2 arrangement, crisp simple typography, each panel is a different zoom scale of the same visual language. Do not merely repeat a similarly sized organism in all panels; the increase in scale and information must be unmistakable.

Consistent world design: flat square-cell soil board, orthographic gently angled 3D camera; matte brown depleted food -> muted olive -> soft green abundant food. There are no hills, trees, rocks, water, hexagons, walls, giant terrain columns or decorative biomes. Soil is subdued, organisms are contrasting ivory/cyan/lilac/amber abstract rounded microbes with a dark contour and thin pale edge. No eyes, faces, legs, monsters. Shallow 3D bodies on a flat board, not photorealism. Sparse tiny sprouts ONLY in green food cells and ONLY in close panels. Colors represent DNA identity, not fixed animal species. Amber selection ring is consistent.

TOP LEFT label "1 · WORLD", subline "Distribution, not individual detail". Show the full large square board with hundreds of tiny but crisp colored dots over broad brown/olive/green patches; simple full-board outline, no cell borders, names, bars or action effects. This panel must read as zoomed far out, not several giant cells.

TOP RIGHT label "2 · HABITAT", subline "Clear bodies and food patches". Board neighborhood crop roughly 22 x 18 cells fills panel. Clear solid round microbe bodies, far fewer visible than world view, fine pale rims and low shadows. Food stays calm, cell seams barely visible. One selected organism amber ring, no text labels floating over other organisms.

BOTTOM LEFT label "3 · ORGANISM", subline "Forms, direction and nearby actions". Roughly 7 x 6 cell crop. Larger rounded dome/bean bodies, subtle internal spot and mark variation, scarce short food sprouts. On one selected cyan organism a short white arrow points to its neighboring cell and a tiny label "Move". One small local action pulse, no all-board fireworks. Clear low-contrast square cell boundaries.

BOTTOM RIGHT label "4 · INSPECT", subline "DNA slot, target and actual outcome". Roughly 3 x 3 cell close-up on selected cyan microbe; selected cell and neighbor clearly separate. Single amber outline on source, dashed target outline on neighboring cell, one arrow to target. Small unobtrusive information card with exact text "DNA slot 2", "Move → East", "Completed". A compact strip of 8 small DNA slot boxes beneath with second box highlighted. No fabricated health or energy numbers. The panel is illustrative and should not imply the simulated organism has biological organs.

Bottom footer "Smooth zoom between levels • Extra labels only near the selection • Illustrative design". Professional usable desktop simulation aesthetic. The purpose is to compare progressively revealed information, not competing art styles.

## B — Outcome correction

Edit the supplied CONCEPT B four-level zoom sheet. Preserve all four panels, camera, colors, board style, organism art and typography. Make a precise correction to the successful movement examples in the two bottom panels; completed movement must show the organism at its destination, not poised to enter it.

BOTTOM LEFT "3 ORGANISM": In the illustrated selected movement, the amber-ringed cyan organism has completed a move one cell to the right. Replace the current solid selected cyan at the center-left of the panel with a thin pale hollow ghost of its previous location. Put the solid cyan organism WITH the amber ring in the adjacent cell to its right, where the short arrow currently ends; remove the other cyan organism that was there so only the selected one occupies that destination. The short arrow points from the ghost to this actual destination. Keep "Move" label and all other bodies unchanged.
BOTTOM RIGHT "4 INSPECT": The CENTER cell is the previous position; remove solid cyan there and put only a faint hollow ghost there. The RIGHT-MIDDLE cell is the actual completed destination; put the solid cyan organism and amber selection outline there. Use a short arrow from the center ghost to this solid organism on the right. Keep the card "DNA slot 2 / Move → East / Completed", the eight numbered DNA slots with 2 highlighted, and other organisms unchanged. Remove sprouts underneath the destination organism if needed for readability. Do not change the other two panels or any labels.
This is a design concept illustrating a true completed outcome; avoid suggesting two live organisms share a cell.

## C — Action storyboard

Use case: ui-mockup / scientific-educational visual storyboard. Create a crisp 1920x1080 landscape concept sheet titled "CONCEPT C — SEE THE ACTION, UNDERSTAND THE RESULT". A six-panel 3 columns by 2 rows design board. Each panel is a SEPARATE illustrative example of a selected artificial-life organism's completed action, not simultaneous events in one world. Footer: "Illustrative actions • Actual effects, not just attempted DNA • Selected organism first". This is review concept art, not a running simulator screenshot.

Shared style in each panel: tiny three-by-three patch of flat square soil tiles viewed through a modest orthographic 3D camera from above. Brown low-food tiles, muted olive and green food-rich tiles, subtle flat seams, low tiny sprouts on only the green cells. Low rounded cyan microbe with dark contour, thin ivory edge, an amber selection ring and no eyes/faces/limbs. Very restrained shading; readable educational viewer rather than a videogame battle. Secondary organisms can be ivory or lilac. No grass forests, scenery, dramatic lighting or deep stacked terrain blocks. The six scene images should occupy most of their respective panels, with a plain short heading and result line.

Panel 1 top-left heading "MOVE". A cyan selected organism and a hollow previous-position outline in the adjacent cell, joined by one short pale arrow. End caption "Moved to adjacent cell". No long snake trail.
Panel 2 top-middle heading "GATHER". A green food tile next to the cyan selected organism; three small green-gold motes travel from food to the body. End caption "Food gathered". A very small down arrow at the ground and up arrow at a carried-food icon distinguish ground food from reserves. No attack or eating of an animal.
Panel 3 top-right heading "HEAL". Cyan organism with one brief soft teal plus-sign pulse above it, not fireworks. End caption "Health restored". No invented numeric values.
Panel 4 bottom-left heading "ATTACK". Cyan selected organism and ivory neighbor, one short red-orange directional slash toward the neighbor, one tiny hit mark on the affected neighbor. End caption "Target took damage". Neither organism is a fixed predator-shaped creature; same neutral body style. No gore.
Panel 5 bottom-middle heading "REPRODUCE". Cyan selected organism and one newly appearing small cyan body in an adjacent cell, one gentle expanding ring, short temporary parent-child link. End caption "Child placed". The smaller body illustrates the appearance animation only, not a claim about biological size rules.
Panel 6 bottom-right heading "BLOCKED". Cyan selected organism attempts to move toward an occupied neighboring cell. Dashed arrow ends in a small muted amber cross before the occupied cell; cyan remains at its original cell, no movement trail. End caption "Attempted move • No movement". Must clearly distinguish an attempted action from a successful action.

Along the bottom of the whole sheet above footer add one slim inspector strip with exact text "Chosen DNA slot → Target → Outcome" and eight slot boxes with one amber highlight. Calm dark slate UI and spacious typography. Effects are short and localized; avoid all-board noise or labels on every organism.

## C — Adjacency/outcome correction

Edit the supplied CONCEPT C storyboard. Preserve the overall layout, text, colors, six panels and visual style. Correct only the event geometry so the diagrams truthfully distinguish completion from an attempt.

1 MOVE panel: The move is ALREADY COMPLETED. Put the solid cyan organism and its amber selection ring in the RIGHT MIDDLE cell, not the center. Put a pale hollow ghost outline in the now-empty CENTER cell. A short arrow goes from the center ghost toward the solid organism on the right. Keep caption "Moved to adjacent cell".
4 ATTACK panel: Move the cyan attacker from the LEFT MIDDLE cell to the CENTER cell, so it is immediately adjacent to the ivory target on the RIGHT MIDDLE cell. Show a short red arrow from center to right and hit mark on the ivory target. No skipped/intervening cell.
6 BLOCKED panel: Move the cyan organism from LEFT MIDDLE to CENTER cell, immediately adjacent to the occupied RIGHT MIDDLE cell containing ivory. A short dashed arrow from cyan toward ivory ends in a cross at their border. Cyan must remain solid in center and the target remains occupied on right. No movement ghost because it was blocked.
All other panels, typography and captions unchanged. Keep modest rendering, no additional effects or labels.

