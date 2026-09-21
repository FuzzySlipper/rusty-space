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
- `Flight/HullForceModel` — how a hull at one state turns the environment and
  its fitted hardware into the push the Engine is asked to integrate, naming
  which source lands at which center. The admitted substep and the projected
  line both resolve through it, so a line on the screen cannot drift away from
  the hull it claims to predict.
- `Flight/TrajectoryProjection` — walks the hull's line forward on the Engine's
  call-local kinematic lane, over whole fixed steps. It owns no integrator, no
  accumulator, and no clock; the line it hands the view is a `FlightPath` of
  points and the interval between them.
- `Field/StellarField` — the authored environment sample at a position:
  local flow, intensity, gradient, turbulence.
- `Field/FieldResponse` — how the hull converts slip against that sample into
  push, scaled by coupling and by the body's real mass.
- `Field/DriftCurrent` — one finite drift band, under the same coupling gate.
  It reports the flow it puts at a point and its own shape, so a view that has
  to draw the current asks for it instead of re-deriving the falloff.
- `Field/OrbitalGravity` — the planet's mass well, deliberately outside that
  gate: a mass relation is not a flow the hull can decline.
- `ShipSystems/InstalledShip` — the hardware fitted to the hull: which parts,
  where each is mounted, what the fit weighs and how hard it is to yaw, and
  what each part's actuator actually reached. It is the owner of the ship's
  several centers — center of mass, main thrust, field coupling, steering
  authority, stabilization — and of nothing else. It never integrates the hull
  and never issues an Engine action.
- `ShipSystems/ActuatorResponse` — the second-order response every part's
  actuator is built from: `response'' + 2·ζ·ω·response' + ω²·response =
  ω²·command`. Frequency is how fast a part gets where it is told; damping
  ratio is how much it overshoots. Both are authored per part, so two sides of
  the same effector pair can disagree, which is where a ship's quirks come
  from.
- `Navigation/PlanarFrame`, `Navigation/PlanarVector` — the planar frame and
  its sign convention, including yaw torque for an offset force and turning a
  part's local mount offset into the world axes.
- `Viewing/TrackingCamera` — framing policy around the Engine camera service:
  smoothed chase position, zoom, camera cut on reset.
- `Presentation/SpacePresentation` — product readouts out to Engine
  appearance and UI facts, including the navigation reading described below;
  retains the current snapshot and retires it before the terminal runtime
  reclaims resources. It reads the environment through its owners and
  re-derives nothing.
- `Debugging/FlightDebugModule` — read-only product debug commands.
- `Tuning/SpaceTuning` — the single composition-root aggregate of the
  per-owner tuning records, admitted once at composition.

Host facts stay the Engine's: mode, generation, admitted and dropped steps,
and delivered content are what the Engine reports about itself, and Space
keeps no copy of them. `Lifecycle/SpaceLifecycleState` is the product's own
state machine — the guard that a turn is admitted only while running, that a
pause is resumed rather than started, and that teardown is idempotent.

## One hull, several centers

The ship is one rigid body and stays one rigid body. Installed parts are
logical effectors: each has a mount offset from the center of mass, a mass of
its own, and an actuator, and it contributes force, torque, mass, and inertia
modifiers to that single body rather than becoming one. No joints, no extra
colliders, no second spatial authority. Only something that physically breaks
loose would ever need a body of its own, and that is not a fit.

A force applied away from the center of mass turns the hull as well as
driving it, and which center each source acts at is most of a ship's
character:

- flow-coupled push — the stellar field and every drift band — arrives at the
  **emitter's mount**. Fitted forward of the center, the same flow that drives
  the hull also swings the bow into itself, so an oversized coil weathervanes.
- main thrust arrives at the **drive's mount**. One hung off the keel makes the
  throttle a steering input the ship has to hold off as a matter of course.
- the heading effector pair pushes on opposite sides of the keel, and the turn
  it delivers is the sum of what the two sides reach. A side that cannot reach
  its share shortfalls the turn and reports the disagreement, rather than the
  other side quietly being asked for more than it has.
- the orbital well is the one source with no lever: a mass relation pulls on
  the hull where the hull's mass is, at the center of mass itself.

Wear is authored the same way. A tired side keeps its rated peak authority but
answers more slowly, rings past a load threshold instead of settling, and adds
a standing pull proportional to the load it is carrying — all continuous in the
load, so the onset is somewhere a player can find and remember. Nothing about
the response is random: no white-noise torque, and no dependence on how the
turn happened to be admitted.

The fit's weight and turn inertia go to the Engine through its body-update
lane, with authored mass properties: the hull is created with mass derived
from its shape, and the fit adds each part's mass and that mass times the
square of its distance from the center. The authored center of mass is the
hull's own origin, because mount offsets are measured from there and every
turn they cause is already counted where the force is resolved — moving the
simulated center as well would bill the same leverage twice. Note what the
update lane actually does: it replaces the whole property set rather than
merging into it, so an update carries the hull's current velocities as just
read, its locks, damping, and collision filtering. That is why a fit is
applied where the hull is freshly built, not opportunistically mid-flight.

## What the view says before the ship gets there

A hull that coasts is not a hull that stops, and a bow pointed one way with the
ship going another is two facts, not one. The navigation view is where that has
to be legible, so each reading is drawn as its own thing:

- which way the hull points is the hull. Which way it is going is a rod beside
  it, aimed along its actual velocity, and the two disagree when they should.
- local flow is shown on a lattice anchored to the world, not to the ship, so a
  reading a line is picked by stays where it was left. Each rod points where
  the flow at its point would carry a coupled hull, and length — not color —
  carries how strong that flow is, because lengths compare at a glance.
- every band is drawn twice: a wide faint region for the authority it actually
  has, which reaches past the slab that marks its core, and the core itself in
  the band's own color, or in the declined color when the hull has wound its
  coupling off. On that trim the band will not catch the hull, and the view
  says so rather than leaving the player to remember the gate.
- the line the hull is on is drawn ahead of it, one marker per sample.

The line is held to the same accounting as the hull, because a player who aims
by it is betting the ship on it. It is walked on the Engine's kinematic lane
over whole fixed steps, resolving the same `HullForceModel` the admitted
substep uses. The controls are held exactly as they are — throttle at what the
drive is delivering, bow at its heading, coupling at its trim — while the
environment is re-read at every point the line reaches. That is what makes the
line bend toward a current the ship has not entered yet, which is the whole
point of drawing it.
And nothing in it slows the ship that is not a force the hull would actually
feel: the projected path is a reading of the present extended forward, rebuilt
from where the hull really is every admitted turn, never a promise the product
keeps to itself.

The tuning-only readings — each source's push and each center of force the hull
has — go on the Engine's debug render layer, so a handling pass can see them
without a player ever having to.

What this is deliberately not: sensor uncertainty, FTL representation, or
camera rule experiments. Those stay in
[`ideas/navigation_view_reconstruction_ideas.md`](ideas/navigation_view_reconstruction_ideas.md)
until a phase asks for them.

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
change. Two things empty held state, and both are deliberate. An admitted
`Clear` is the Engine's: focus lost, device lost, or a binding change. And
Space resets the mapper itself whenever the hull is rebuilt — the mapped
reset intent and the product's own restart both run the same flight reset —
so a respawn starts from released controls rather than from whatever was
still being held when the ship was replaced.

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

## Release follows construction

`SpaceProductComposition` builds flight, then the presentation projection,
then the camera, and puts them down in the reverse of that order. A create
that fails partway releases whatever got as far as opening Engine handles, so
a failed construction leaves no owner holding a handle nobody can reach.

Teardown order matters once: shutdown retires the product's retained
appearance snapshot while the services it references are still reachable, and
the handles that snapshot pointed at are released afterwards.

The Engine's lease wrappers are what make releasing at teardown safe rather
than fragile. A release issued inside a staged call is enrolled and committed
or rolled back with that call; once the runtime has completed terminally, a
release drops its action instead of issuing a native call. So Space releases
what it opened on both paths and needs no retry list or lease registry of its
own to do it safely.

Space starts no external timelines, so it does not claim to complete them: the
Engine's default answer — none was completed — is the truthful one.

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
