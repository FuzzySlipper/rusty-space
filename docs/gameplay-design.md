# Space gameplay design

The current, concrete ownership map for Space's product code: which named
owner holds which state today, and where the adopted Engine surface is meant
to be used as planned systems land. Durable design only — sequencing, status,
and progress live in Den under campaigns #8305 and #8366.

- Boundary statement and the invariants a review checks:
  [space sailing campaign](space-sailing-campaign.md).
- Packaging and runtime lanes: [architecture](architecture.md).
- Code conventions: [code style](code-style.md).

## The pair the product runs on

One exact matched release pair: an SDK package version plus the runtime pack
built from the same Engine revision. Its identity lives in machine
configuration — `RustyEngineSdkPackageVersion` in `Product.Game.csproj`, the
launch commands in `.den-serve.json` and `.den-playwright.json`, and the
extracted pack under the ignored `.runtime/` tree — and is recorded on the
adopting Den task. Docs deliberately do not name it: a revision in guidance
goes stale the moment a pair is superseded, and a stale revision reads like a
supported capability.

Adoption is the file contract in the Engine's `docs/csharp-distribution.md`:
verify the archive against its adjacent checksum, extract, run the bundled
pair verifier, then point the product feed and launch commands at that pair.
No adjacent checkout, no Cargo, no downstream binding generation, no
compatibility negotiation.

## Who holds what

Engine Dynamics holds the ship's one rigid body: pose, velocity, mass, and
every integration. Nothing in C# integrates or corrects it.

Product owners, one mutable state family each:

- `Flight/SpaceFlight` — the admitted-turn flight spine. Reads the body,
  advances the controller and coupling actuator per fixed substep, resolves
  the force table, issues one Dynamics step per substep, captures telemetry,
  and owns reset.
- `Flight/FlightController` — throttle spool and steering: response shaping,
  saturation, and the effort and saturation facts telemetry reports. The
  attitude hold is a switch the player can throw, not an assumption.
- `Flight/FieldCoupling` — the coupling actuator: trim travel rate, clamps,
  emergency dump, cradle setting.
- `Flight/FlightInputMapper` — admitted named intents in, one closed
  `FlightCommand` out. Held control state lives here and nowhere else.
- `Field/StellarField` — the authored environment sample at a position:
  local flow, intensity, gradient, turbulence.
- `Field/FieldResponse` — how the hull converts slip against that sample into
  push, scaled by coupling and by the body's real mass.
- `Field/DriftCurrent` — one finite drift band, under the same coupling gate.
- `Field/OrbitalGravity` — the planet's mass well, deliberately outside that
  gate: a mass relation is not a flow the hull can decline.
- `Navigation/PlanarFrame`, `Navigation/PlanarVector` — the planar frame and
  its sign convention, including yaw torque for an offset force.
- `Viewing/TrackingCamera` — framing policy around the Engine camera service:
  smoothed chase position, zoom, camera cut on reset.
- `Presentation/SpacePresentation` — product readouts out to Engine
  appearance and UI facts; retains the current snapshot and retires it before
  the terminal runtime reclaims resources.
- `Debugging/FlightDebugModule` — read-only product debug commands.
- `Tuning/SpaceTuning` — the single composition-root aggregate of the
  per-owner tuning records, admitted once at composition.

`Lifecycle/` and `Content/` currently mirror host facts — lifecycle state,
last admitted update evidence, a content file count — with no product
consumer of their own; the campaign #8366 lifecycle child reconciles them
against the adopted contract.

## Time is admitted, not assumed

The Engine hands each update its facts: mode, lifecycle state, generation,
simulation step, fixed-step rate, admitted and dropped step counts, and the
fixed delta in seconds. Product work carries those admitted values through
each per-substep controller, coupling, force, and Dynamics operation, and
derives turn duration for telemetry and camera from the same facts. Step
counts and sequences stay because they carry meaning for diagnostics and
reset; a count multiplied by a separately written constant is a second clock
and is not how a turn is measured.

## Controls arrive as named intents

The product manifest declares what the pilot can touch: every intent and every
mapping from a physical control to it. Keyboard covers thrust, left and right
turn, couple, uncouple, and emergency uncouple as held-or-released, and reset,
abort, and the attitude-hold switch as presses. The controller contributes the
bumper turns, the trigger's analog thrust, the stick's turn and trim axes, and
the reset press. The wheel arrives as `space.camera.zoom`.

The Engine maps physical controls onto those names and admits the result. Space
reads names and holds no vocabulary of physical labels: there is no second path
that interprets raw keys when a mapped turn looks unrecognized, and the camera
does not listen for a raw wheel behind the declared intent. One gesture cannot
act twice, and re-binding a control is a manifest edit rather than a code
change. An admitted `Clear` is what empties held state; that is the Engine's
contract and the product's only reset of it.

Two handles are keyboard-only today: the attitude-hold switch, the emergency
uncouple, and abort have no controller button, and coupling trim has no digital
controller fallback. Those are open control decisions, tracked with the
readout and legibility work in #8311.

## State changes directly

Trusted local game state is ordinary C#: one owner per state family, direct
readable mutation inside it, and a named method for each operation that has
product meaning — advance the spool, dump the actuator, reset the hull. Where
a calculation is genuinely pure it returns a value, which is not an
acceptance transaction, and there is no staging-and-commit protocol
protecting in-process state from itself. Numerical guards that express real
physical ranges, domain clamps, and disposal guards stay; they are not
ceremony.

## Entities, components, and stats: planned, not present

The adopted SDK ships managed entity machinery (`Rusty.Engine.Entities`:
`EntityStore`, `ComponentType`, `Actor`, batches, edits) and mechanics
machinery (`Rusty.Engine.Mechanics`: stats, tracks, equipment, inventory).
`EntityStore` is Engine-maintained managed storage for product-owned typed
entity facts, deliberately independent of the host update pipeline and not a
projection of Rust entity state: Rust mechanisms stay reachable through their
generated services, and the store exists so an ordinary component read or
write costs no native crossing. `Actor` is an optional facade over an
existing entity; constructing one attaches nothing.

Space has one anonymous rigid body today, with no part identity and no
bounded resource. So it has no entity, no component, and no stat today, and
that is a disposition rather than a missed migration. Adoption belongs to the
tasks that create the identity:

- Installed systems (#8310): decide entity and component boundaries from real
  ship and part identity and lifetime. An independently addressed ship or
  part can be an entity in an `EntityStore` with class components holding
  installed-system state that force resolution, damage, diagnostics, and
  repair all read. A mount or channel with no independent identity stays a
  field on its owner. The one native rigid body stays the physical
  authority: no mirrored pose or velocity, no articulated physics.
- Bounded resources (#8314): `Stat` and `Track` where a requirement genuinely
  is a bounded scalar with a shared maximum and modifiers. Heat flow and
  actuator second-order response stay physical rate calculations in their own
  owners and are not flattened into generic effects.

Neither adoption turns every force sample into a component, and neither adds
a save system, an inventory, a stat catalogue, or crew features in order to
exercise an API.

## Persistence posture

Space persists nothing today. The Engine offers a persistence service; if a
save ever becomes a product requirement it is serialized product state at an
explicit boundary, owned by a named product owner — not a general catalogue,
and never a second record of physical state.

## Observation stays observation

Telemetry and readout records describe what a turn did; they do not decide
what the ship does next. First-and-last substep force samples, effort,
saturation, and acceleration in the ship's own frame remain useful evidence.
A debug command reports state and never writes it.
