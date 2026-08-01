# Tower Weapon Effect Visibility At Board Scale

Each tower fired once at damage 6, tier 1, with the match paused so nothing else
draws into the measurement. Frames diffed against a baseline taken one frame before
the shot, at 1080x1920. A pixel counts as lit at +8/255 on any channel.

| Tower | Lit px | Peak | Reads |
| --- | --- | --- | --- |
| ARROW | 12353 | 218 | yes |
| CTRL | 4052 | 238 | yes |
| RELAY | 9622 | 227 | yes |
| PULSE | 146 | 213 | NO |
| PRISM | 5110 | 224 | yes |
| GATLING | 2720 | 226 | yes |
| TESLA | 6673 | 228 | yes |
| FOUNDRY | 12632 | 229 | yes |
| BULWARK | 10390 | 206 | yes |
| DRONE | 424 | 211 | NO |
| CANOPY | 18558 | 224 | yes |
| SAPLING | 10781 | 221 | yes |
| BLOOM | 11277 | 210 | yes |
| THORN | 10349 | 210 | yes |
| SPORE | 10724 | 224 | yes |
