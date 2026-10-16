---
target: review the UI again (all pages)
total_score: 26
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:src/DiskSteward.App/Features"
timestamp: 2026-10-16T11-32-19Z
slug: src-disksteward-app-features
---
DEGRADED: single-context (global rule requires explicit approval for subagents)

## Design Health Score
| # | Heuristic | Score |
|---|---|---|
| 1 | Visibility of System Status | 3 |
| 2 | Match System / Real World | 3 |
| 3 | User Control and Freedom | 3 |
| 4 | Consistency and Standards | 3 |
| 5 | Error Prevention | 3 |
| 6 | Recognition Rather Than Recall | 2 |
| 7 | Flexibility and Efficiency | 2 |
| 8 | Aesthetic and Minimalist Design | 3 |
| 9 | Error Recovery | 2 |
| 10 | Help and Documentation | 2 |
| Total | | 26/40 Acceptable |

## Design Specificity
Competent, category-interchangeable Fluent Settings look; the product's safety model is under-expressed. Detector returned [] because it does not read WPF XAML (not a clean bill).

## Priority Issues
- [P1] Clean buries its answer: total is a grey subtitle, groups lack plain descriptions, primary action looks disabled. layout, clarify.
- [P1] Details panel is mostly 0 B noise and duplicates the Analyze tree; rows hover like actionable items. distill.
- [P2] Apps blank date/size cells read as broken; unknowns order not stated. harden.
- [P2] Analyze lock labels (Protected vs Windows needs this) look identical; dead disabled tick boxes. clarify.
- [P2] No keyboard shortcuts anywhere (F5, Ctrl+F, Del, Space). harden.

## Persona Red Flags
Alex: no keyboard path, no jump to largest folder. Sam: nested tick box in toggle header, disabled tick boxes announced, log announced as selectable list. Jordan: no safety explanation per group, jargon, scan completion only in the log.

## Minor
Analyze selection bar against chevron; flat Memory chart reads as a block; CPU detail truncated; Monitor health hero oversized when 100; Analyze scan card dominates at minimum size.
