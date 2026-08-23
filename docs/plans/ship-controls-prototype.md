# Ship controls + physics movement prototype plan

The first Rusty Space prototype proves the core flight feel before any universe,
interior, or salvage systems: a single ship, empty space, classic Asteroids
controls, and a hard max-speed cap.

## One invariant

> **Heading is not velocity.**

Input produces force → acceleration → persistent velocity, never
"input → desired movement." Releasing thrust stops adding force but never
brakes the ship. This is the single property every stage must preserve.

## Ownership shape

- **Rust = capabilities**: the fixed-step loop, the flight controller
  equations, the rigid-body/integration wiring, the max-speed mechanism, and
  semantic admission/compilation.
- **TypeScript DSL = expression**: the feel constants (max speed, thrust, turn
  rate, response times) are authored in `gameplay/authoring` catalogs,
  materialized through the `gameplay-rules` envelope (schema 2, binary64), and
  compiled by Rust into a canonical `ShipHandlingDefinition`.
- **Missing Engine surface → a task in `rusty-engine`**, never a local
  emulation. No parallel structures, no local integrator, no second spatial
  authority.

Reference shape: `rusty-dagger/gameplay/src/{authoring,catalogs,packages}` and
the Engine `greenfield-downstream-product.md` / `upstream-promotion-and-authoring-dsl.md`.

## First-cut feel constants (authored in TS, trivially tunable)

| Field | Value | Meaning |
| --- | --- | --- |
| `max_speed` | 12.0 u/s | hard velocity cap (Asteroids) |
| `max_thrust` | 18.0 u/s² | main-drive acceleration along ship-forward |
| `max_turn_rate` | 3.0 rad/s | yaw angular-rate ceiling (~172°/s) |
| `throttle_response_time` | 0.08 s | spool lag on the main drive |
| `steering_response_time` | 0.12 s | angular-rate response lag |

Units are local "chart units" (u) and seconds on the XZ plane (Y-up, yaw around
Y). These are starting values; tuning happens in the TS catalog, not in Rust.

## Stages

| Task | Stage | Outcome |
| --- | --- | --- |
| #7220 | P0a Rename | `rusty-template` → `rusty-space` throughout |
| #7221 | P1 Authoring spine | `ShipHandlingDefinition` authored in TS, compiled in Rust |
| #7222 | P2 Flight controller | pure `controller(state, command, handling) -> wrench` |
| #7223 | P3 Fixed-step runtime | live loop + one dynamic body via `svc-collision` |
| #7224 | P4 Browser-host | WebSocket dev adapter + keyboard turn/thrust |
| #7225 | P5 Nav view | ship marker + heading/velocity vectors + path line |
| #7226 | P6 Field source | one current + one wake, coupling bends route |

Stages are sequential; P4–P6 depend on P3. P1 and P2 are headless-Rust-testable
without the Engine loop.

## Engine dependencies (project `rusty-engine`)

| Task | Need | Status |
| --- | --- | --- |
| #7217 | Per-axis translation/rotation locks (planar XZ body) | planned |
| #7218 | Per-tick force-recompute seam + 60 Hz benchmark | planned |
| #7219 | Explicit mass properties (center of mass / inertia) | deferred |
| #7057 | Canonical float support in the rules envelope | done |

P3 is blocked on #7217 and #7218. Do not emulate planar locking by zeroing
out-of-plane velocities after each step.

## Deferred (not this prototype)

The 3D reactive interior, the full synthetic-reconstruction navigation display,
parts/repair/handling variation, combat, and cruise/travel structure. They land
after the core feel (P2–P6) is proven — see `docs/ideas/`.
