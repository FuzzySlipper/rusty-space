# Space sailing handling campaign

Durable technical companion to
[`space_sailing_reactive_ship_ideas.md`](ideas/space_sailing_reactive_ship_ideas.md)
and
[`ship_physics_implementation_notes.md`](ideas/ship_physics_implementation_notes.md).
The code-level owners these invariants apply to are mapped in
[`gameplay-design.md`](gameplay-design.md).

> Den owns sequencing, status, and progress. This document holds the canonical
> state split, ownership boundaries, invariants, and known hazards. It
> deliberately carries no task status, checklist, or roadmap state.

## Thesis

One dynamic planar rigid body, one heavily authored flight controller, one
fictional environmental force model, one logical collection of wonky ship
parts, and one fake-but-reactive interior set.

Rapier integrates. The product decides what every force means. The interior
performs. The physics solver is the ship's accountant, not its captain.

The game is a deliberate stack of compatible fakes. The part that matters is
that those fakes exchange meaningful state.

## Canonical state split

Owners below are target state. Some exist today; others are the owners this
campaign introduces.

Physical, authoritative state — pose, linear velocity, angular velocity, mass
properties — belongs to Engine Dynamics. The product reads it and never
rewrites it.

Authored simulation state — flight intent, actuator state, coupling,
stabilizer state, installed parts, health, temperature, faults — belongs to
the product's Flight and ShipSystems owners. This is where the personality
lives.

Derived observability state — accelerations, controller effort, saturation,
field load, collision impulse — belongs to product telemetry. It is
disposable and never authoritative.

Presentation state — camera, lights, props, audio, instruments, HUD —
belongs to product presentation and stays strictly downstream.

Presentation never reaches backward. Controls and repairs change authored
ship-system state; nothing a camera shake or a lamp does may alter the
solver.

## Three spatial fictions

Strategic space, local navigation space, and interior set space stay
separate. Local navigation uses the XZ plane with yaw around Y and
numerically comfortable chart units; FTL never means asking the solver for
literal superluminal speed. The interior is a stationary ship-local set that
never travels, never rotates with the rigid body, and never contains a
character standing on a moving body. Navigation exports telemetry; the set
interprets it.

## Invariants

These hold for every change in this campaign and are what a review checks.

1. **Heading is not velocity.** Input commands intent; it never sets a
   transform.
2. **Engine is the only integrator.** No C# integration, no post-step
   velocity correction, no parallel spatial authority, no emulated axis
   locking.
3. **No solver damping as space drag.** Solver linear damping is
   world-relative and would erase inertial flight. Any drag-like behavior is
   authored against the local field: `relative = ship_velocity -
   local_field_velocity`.
4. **Releasing thrust never silently brakes.** Rotational assistance never
   implies linear auto-braking. Any velocity or trajectory hold is an
   explicit ship system with visible effort and limits.
5. **Parts are logical effectors.** One body. No jointed modules, no
   per-part colliders. Only a component that physically breaks free becomes
   its own body.
6. **Good wobble only.** Actuator lag, underdamped response, saturation,
   hysteresis, asymmetry, and load-dependent oscillation are authored and
   reproducible. Turbulence is spatially and temporally continuous. No
   white-noise torque, no frame-rate-dependent noise, never raw solver
   instability sold as personality.
7. **Failure becomes a situation.** Physics mistakes create recoverable
   consequences that feed repair and scavenging, not deletion.
8. **Every secondary system bends back toward piloting.** If removing a
   system would not change how the ship flies, it is decorative or too
   disconnected.
9. **Admitted time is the only clock.** The fixed delta and step count the
   Engine admits drive every per-substep actuator, force, and Dynamics
   operation, and any duration a turn reports to telemetry or the camera. A
   rate written into product code is a second clock that quietly disagrees
   with the host.
10. **Controls are declared, not detected.** Every control the product answers
    to is a named intent with its mapping in the product manifest, and the
    Engine maps physical controls onto those names before a turn reaches the
    product. Product code holds no vocabulary of physical labels, so a gesture
    cannot act twice and re-binding is a manifest edit.

## Known hazards

### Planar sign convention

The product planar frame is authoritative for ship attitude. `PlanarFrame`
in `Navigation` owns every expression of it: forward and right for a
heading, the heading of a direction or of an Engine attitude, the heading
rate an Engine angular velocity carries, the single heading to-attitude
conversion, the Engine value of a heading-positive angular quantity, and
the heading sense of a torque built from an in-plane offset and force.
Positions, velocities, and forces cross into Dynamics with the plane's
coordinates taken identically as `(X, Z)`; no planar vector is mirrored on
the way across.

The Engine is right-handed Y-up while a planar `(X, Z)` pair is left-handed
about `+Y`, so a heading `h` needs an Engine rotation of `-h` to face `(cos
h, sin h)`. That negation is one rule about one axis rather than three
separate quirks: the Engine's whole angular channel — attitude angle,
angular velocity, and torque — is the negation of the heading quantity it
stands for, in both directions. `PlanarFrame` crosses it in both directions,
and the crossings have to agree. Read one of them in the other's direction
and the ship spins one way while its nose, its thrust, and its readouts
report the other: wrong at every nonzero heading, and invisible at the zero
heading a default spawn happens to use. Anything that touches an Engine
attitude or the `+Y` angular channel goes through this type, which is the
only place the flip is written down.

Hand-computed torque from an off-center force inherits the question. Call
`PlanarFrame.YawTorque` rather than a cross product already in hand: it
returns the heading-positive sense, which is the negation of the
world-space cross product of the same two vectors, and it reaches a solver
only through `PlanarFrame.EngineYaw`.
Get it wrong and the ship weathercocks, trims, and asymmetry-corrects in the
mirrored direction, silently, with perfectly stable numbers.

Asymmetric collision silhouettes start mattering once local geometry exists.
The tuned ship half-extents are already unequal in X and Z, so a mirrored
attitude presents the wrong cross-section to the thing it hits. Settling
that means either authoring silhouettes in the body's mirrored frame or
making body attitude agree with the planar heading, which takes the steering
and input signs with it.

### Force staleness under catch-up

`AdmittedStepCount` can exceed one, and `IDynamicsService.Step` applies the
actions in a request once before running its own substeps: rapier keeps a
body's added force across every step of one simulation, so a request that
carries four steps applies one frozen answer four times. `SpaceFlight.Admit`
instead resolves every source and steps once per fixed substep, reading the
body back between substeps, so each push acts on the state it meets. The
Engine rebuilds its world from canonical state on each `Step` call, which is
what makes the separate calls correct and also what makes each one cost a
rebuild; that cost lands only on turns that were already catching up.

The actuator spool advances per fixed substep for the same reason: an
admitted step is one fixed step of simulated time, so a turn that catches up
four steps has had four steps of throttle travel. The spool and the coupling
level are their owners' own state, each moved over the one admitted interval it
is handed and read back from the owner, so an interval cannot be counted twice
and nothing a turn did is left waiting for a separate publication.

### Coupling is the ship's, the well is the planet's

Field coupling is a ship-owned actuator, not a constant on the field. The
hull carries a live level that trim winds up and down at a bounded rate,
and every flow-coupled source — the stellar field and the drift bands —
scales by it. At zero the environment is declined exactly: the ship keeps
the velocity it arrived with and no authored river can bend it. The tuning
record therefore holds the cradle setting and the travel time, never the
level itself, and the `trim_response(relative_flow, coupling_trim)` term
in the notes' force sketch is realized as this actuator's travel rather
than as a separate push, so one handle keeps one meaning.

The orbital well stays outside that gate on purpose. A gravity well is a
mass relation, not a flow the hull can decline to catch, so a ship that
has wound itself off still falls toward the planet while holding a
straight line through a river. Gating the well as well would make zero
coupling mean "nowhere to fall", which is a different fiction and a worse
one.

The emergency release dumps the actuator to zero and leaves it there until
the player winds it back in. What bailing out costs is the travel time
back to the cradle, never a stolen velocity.

### Mass consistency

Environmental sources that scale by mass must scale by the same real body
mass. A fixed constant in one response path silently diverges from the rest
the moment parts, cargo, or towing change mass.

## Engine boundary

The Engine requests named in `ship_physics_implementation_notes.md` §11 are
already satisfied by the pinned `Rusty.Engine` package: per-axis translation
and rotation locks, optional explicit mass properties, contacts and collision
impulses, body update, spatial sessions bindable to a dynamics world,
lights, sprites, billboards, particles, audio voices and buses, and a
generated product-facing debug command catalog.

Continuous collision also exists at the pinned revision and reaches the
managed surface as `DynamicsBodyProperties.ContinuousCollision`, with the
per-step motion limit widening for bodies that opt in. Spatial collision
shapes also become fixed bodies when a session is bound to a dynamics world,
so a collision space needs no new Engine mechanism. The one remaining gap is
narrower than it first appears: the generic create path omits the properties
bag the shape-typed configs carry. That is filed as a narrow Engine request
rather than worked around downstream.

Handles the product opens come back down in the reverse of the order that
opened them, and the Engine's lease wrappers are what make that safe: a release
issued inside a staged call is committed or rolled back with it, and once the
runtime has completed terminally a release drops its action instead of issuing
a native call. So a product-side retry list or private lease registry is never
the answer to a lifetime question.

Missing capability is a valid result. File one purpose-neutral owning
request and stop that slice; never substitute a C# renderer, loop, timer,
bridge, or browser simulation.

## Anti-goals

No real orbital mechanics, literal astronomical scale, physically moving ship
interior, character standing inside a moving world-space rigid body,
articulated jointed ship, realistic fluid dynamics, random torque noise sold
as turbulence, universal drag that erases inertia, or engine-level
space-sailing policy.

## Evidence

Smallest evidence that answers the seam: focused C# build, CoreCLR staging or
`rusty dev` startup, and NativeAOT only when a phase needs fidelity or
release proof. Pure product math — navigation helpers, response primitives,
field sampling — is covered by focused unit tests. This campaign does not
revive the retired Rust or TypeScript gates, browser bundles, packaging
frameworks, or interactive-parity claims.

Handling is a felt property, so a phase that changes feel also wants the ship
driven by real input while these values are read. The `playtest` service
documented in the separate `crew-services` repository provides that: `den-serve`
publishes the product on a LAN origin, `playtest start rusty-space` opens a
browser with a native Xbox controller on the GPU host, and key, stick, and
trigger holds arrive as ordinary Engine input. The live-debug commands are then
read from the same running product. This is an evidence lane, not a gate, and it
adds no product infrastructure.

One trap there: a `playtest` session can enter a degraded phase and refuse
further input while the product keeps running normally. A zero in the
contribution table after that says nothing about flight, so confirm the session
phase before reading a null result as a finding.

## Phase map

Scope and acceptance for each phase live in Den under campaign task
**rusty-space #8305**. The order below is a dependency order, not a status
board.

- Telemetry spine and product debug surfaces (#8306). Observability before
  content. Everything after this is tunable instead of guessable.
- One named planar navigation helper (#8307). Removes the mirrored-torque and
  mirrored-silhouette hazard before any mount offset or local geometry
  depends on attitude.
- Per-substep force recompute (#8308). Closes the catch-up staleness,
  measured rather than assumed.
- Field coupling as a real ship system (#8309). Restores the design's
  central handle and the ability to decline the environment.
- Installed parts as logical effectors (#8310). The hinge of the whole
  design: hardware that changes behavior, not numbers.
- Navigation-view legibility (#8311). Gives the player something to read
  before they are pushed.
- One collision space (#8312). Impacts become impulses and faults.
- Bridge set theater (#8313). Makes flight easier to read, strictly
  downstream.
- Failure becomes a situation (#8314). Closes the loop: faults change the
  hands, and repairs are felt before they are read.

One Engine-side dependency is filed separately: **rusty-engine #8304**,
body-properties parity on the generic rigid-body create path. It is not
blocking; see the Engine boundary section.
