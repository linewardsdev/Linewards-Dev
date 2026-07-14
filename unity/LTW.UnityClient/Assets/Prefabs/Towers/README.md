# Tower Prefabs

Runtime tower prefabs belong here.

Each prefab should include required children: `Body`, `RoleMarker`, `OwnerTrim`, and `RangeHalo`.

## 5x2 Roster Targets

Create authored low-poly prefabs for:

| Runtime Id | Prefab Name | Required Read |
| --- | --- | --- |
| `tower.arrow` | `Tower_Arrow.prefab` | Tall focused emitter / firing spine. |
| `tower.control` | `Tower_Control.prefab` | Wide ring, dish, or field controller. |
| `tower.relay` | `Tower_Relay.prefab` | Mast/support beacon with signal/economy read. |
| `tower.pulse` | `Tower_Pulse.prefab` | Compact burst core with expanding ring language. |
| `tower.prism` | `Tower_Prism.prefab` | Tall crystalline lens-spire for long-range focus. |

Keep primitive fallbacks active until each prefab passes the screenshot gates in `docs/GRAPHICS_2000_BASELINE_ROADMAP.md`.
