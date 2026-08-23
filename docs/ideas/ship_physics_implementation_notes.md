# Ship Physics and Reactive Interior Implementation Notes

> Exploratory implementation companion to [`space_sailing_reactive_ship_ideas.md`](./space_sailing_reactive_ship_ideas.md).
>
> The target is not realistic spacecraft simulation. It is a readable, expressive 2D inertial-thrust flight game whose forces are made immersive by a completely staged 3D ship interior.

## Working decision

The strongest implementation hypothesis is:

> **One dynamic planar rigid body, one heavily authored flight controller, one fictional environmental force model, one logical collection of wonky ship parts, and one fake-but-reactive interior set.**

Rapier supplies integration, momentum, angular inertia, and collision response.

The game supplies all of the interesting lies:

- what a graviton current is,
- how a drive catches it,
- how steering responds,
- how damaged parts oscillate,
- how much assistance the ship provides,
- and how the bridge set pretends any of this is happening to a room.

The physics solver is the ship's accountant, not its captain.

---

# 1. Why use a dynamic body at all?

Asteroids-style inertia does not require a physics engine. It can be integrated directly:

```text
velocity += acceleration * dt
position += velocity * dt
```

A custom kinematic flight model would be easy to control and easy to tune.

The reason to test a dynamic body is not basic inertia. It is the way this particular game wants several force-producing systems to compose:

- player thrust,
- steering authority,
- stabilizer correction,
- planetary wakes,
- stellar currents,
- turbulence,
- asymmetric damaged equipment,
- shifted centers of force,
- collision impulses,
- towing and cargo mass,
- and temporary failures.

A dynamic body gives all of these systems one common language:

```text
force + torque -> acceleration -> persistent velocity
```

That matters because the desired feel comes from the player wrestling with several forces that remain coherent with one another.

The usual warning against physics-driven player characters still applies, but a free-flying ship is a much friendlier physical object than a walking capsule:

- it rarely rests on contact manifolds,
- it does not climb stairs,
- it does not need exact positional obedience,
- it is expected to drift and overshoot,
- and resistance from the world is part of the fantasy.

The ship can therefore be physically authoritative without giving the solver authority over the control design.

---

# 2. Three separate spatial fictions

The game should not attempt to unify every representation into one literal world.

## 2.1 Strategic space

Strategic space represents astronomical relationships:

- star systems,
- routes,
- planets,
- stations,
- travel opportunities,
- and broad field conditions.

Its units are invented navigation units, not kilometers.

## 2.2 Local navigation space

Local navigation space is the actual flight game:

- planar inertial motion,
- local current fields,
- nearby masses and obstacles,
- projected trajectories,
- docking approaches,
- and collisions.

It should use numerically comfortable local units. FTL does not mean asking Rapier to move a body at literal superluminal speed.

Assuming Rusty Engine's Y-up convention, the navigation plane should be XZ:

- allow X and Z translation,
- lock Y translation,
- allow yaw around Y,
- lock rotation around X and Z.

## 2.3 Interior set space

The ship interior is a stationary ship-local set.

It does not:

- travel through navigation space,
- rotate with the Rapier body,
- occupy astronomical coordinates,
- or physically contain a character standing on a moving rigid body.

The old Star Trek bridge trick is the correct model. The set remains stable while actors, props, lights, cameras, audio, and instruments sell the external event.

The navigation simulation exports telemetry. The interior presentation interprets it.

This avoids the entire moving-reference-frame swamp while preserving the useful consequences of the flight simulation.

---

# 3. Canonical state split

A useful separation is:

```rust
struct NavigationBodyState {
    position: Vec2,
    heading: f64,
    linear_velocity: Vec2,
    angular_velocity: f64,
}

struct ShipSystemsState {
    drive_spool: f64,
    field_coupling: f64,
    steering_response: f64,
    steering_response_velocity: f64,
    stabilizer_response: f64,
    stabilizer_response_velocity: f64,
    heat: f64,
    stored_energy: f64,
    parts: Vec<InstalledPartState>,
}

struct ShipTelemetry {
    linear_acceleration_local: Vec2,
    angular_velocity: f64,
    angular_acceleration: f64,
    field_load: f64,
    controller_effort: f64,
    stabilizer_effort: f64,
    structural_load: f64,
    power_draw: f64,
    heat: f64,
    collision_impulse_local: Vec2,
}
```

The navigation body is physical state.

The ship systems are authored simulation state.

Telemetry is disposable presentation input.

The interior never reaches backward and changes the physics merely because a camera shake animation happened. Useful controls and repair interactions may change `ShipSystemsState`, but presentation remains downstream.

---

# 4. Fixed-step force pipeline

Each fixed simulation tick should look roughly like this:

```text
raw player input
    -> flight intent
    -> actuator and controller response
    -> local field sample
    -> per-part force contributions
    -> damage and failure contributions
    -> summed force and torque
    -> Rapier step
    -> new pose and velocities
    -> telemetry derivation
    -> helm and interior presentation
```

A useful common type is a **wrench**, meaning force plus torque:

```rust
#[derive(Default, Clone, Copy)]
struct ShipWrench {
    force_world: Vec3,
    torque_world: Vec3,
}
```

Each subsystem contributes a named wrench:

```rust
struct FlightForces {
    main_drive: ShipWrench,
    steering: ShipWrench,
    stabilizer: ShipWrench,
    field_coupling: ShipWrench,
    turbulence: ShipWrench,
    damage_bias: ShipWrench,
    towing: ShipWrench,
}
```

Keep these contributions separate until the final sum. That makes them available for:

- debugging,
- instrumentation,
- audio,
- interior effects,
- damage calculation,
- and tuning graphs.

The total passed to Rapier is simply:

```text
net_force = sum(all force contributions)
net_torque = sum(all torque contributions)
```

Rusty Engine already exposes force and torque on a rigid-body action, so force-at-point behavior can initially be reduced to an equivalent net wrench in `rusty-space`.

---

# 5. Player input should command intent, not transforms

Do not directly set the body pose.

Do not make the ordinary steering input a request for an instantaneous rotation.

Input should request what the ship's controls attempt to do.

A minimal command could be:

```rust
struct FlightCommand {
    throttle: f64,
    turn: f64,
    coupling_trim: f64,
    stabilizer_enabled: bool,
    emergency_uncouple: bool,
}
```

The controller and installed parts turn that command into bounded forces.

## 5.1 Main thrust

Main thrust can be authored as:

```text
desired_output = throttle * maximum_output
actual_output = spool_response(desired_output)
force = ship_forward * actual_output
```

The response can depend on:

- installed drive,
- power availability,
- temperature,
- damage,
- current coupling,
- and controller quality.

Releasing thrust stops adding forward force. It does not erase velocity.

## 5.2 Steering as angular-rate control

A readable assisted steering model is:

```text
desired_angular_velocity = turn_input * maximum_turn_rate
error = desired_angular_velocity - current_angular_velocity
requested_torque = inertia * error / response_time
authoritative_torque = clamp(requested_torque, available_torque)
```

This means:

- input response is predictable in calm space,
- external torque can overpower the ship,
- damaged steering can lag or overshoot,
- better parts can increase authority,
- and weak stabilization allows rotation to continue after release.

A manual mode can expose more direct torque control for expert play, but ordinary controls should probably be angular-rate intent rather than a raw torque lever.

## 5.3 No automatic linear stopping by default

Rotational assistance does not imply linear auto-braking.

The core rule remains:

> **Heading is not velocity.**

Any velocity-hold, trajectory-hold, or auto-counterthrust behavior should be an explicit ship system with visible effort and limitations. It should never quietly turn the ship back into a ship-shaped person.

---

# 6. Fictional space weather as a velocity field

The simplest useful environmental representation is a continuously sampled local flow field.

```rust
struct FieldSample {
    flow_velocity_world: Vec2,
    intensity: f64,
    gradient: Mat2,
    turbulence: Vec2,
    instability: f64,
}
```

At the ship's location:

```text
relative_flow = ship_velocity - field_flow_velocity
```

This is analogous to a boat's motion relative to water or a sail's motion relative to air.

Resolve it into ship-local axes:

```text
forward_slip = dot(relative_flow, ship_forward)
lateral_slip = dot(relative_flow, ship_right)
```

Then the engaged field drive can create authored forces from those values:

```text
field_force =
    longitudinal_response(forward_slip, coupling)
    + lateral_response(lateral_slip, coupling)
    + trim_response(relative_flow, coupling_trim)
    + turbulence_response(field_sample)
```

The exact equations can be complete nonsense. The important behavior is:

- an uncoupled ship preserves inertial motion,
- coupling lets the environment act on the ship,
- orientation changes how the ship catches the flow,
- trim changes the resulting force,
- stronger gradients produce stronger or less stable responses,
- and the current can pull against the player's intended line.

## Do not use global damping as space drag

Rapier linear damping slows velocity relative to the world origin. That would gradually destroy inertial flight.

Keep solver linear damping at zero or nearly zero.

Any drag-like behavior should be authored relative to the local field:

```text
relative_velocity = ship_velocity - local_field_velocity
```

This lets the ship be carried by a current without the universe mysteriously applying brakes whenever thrust is released.

Angular damping should likewise be an authored stabilizer torque with finite authority rather than universal solver syrup.

---

# 7. Centers of force create natural handling character

The ship should conceptually have several different centers:

- center of mass,
- center of main thrust,
- center of field coupling,
- center of steering authority,
- center of stabilization.

A force applied away from the center of mass contributes torque:

```text
torque = offset_from_center_of_mass x force
```

For one rigid body, `rusty-space` can calculate this directly:

```rust
fn add_force_at_point(
    wrench: &mut ShipWrench,
    force_world: Vec3,
    point_world: Vec3,
    center_of_mass_world: Vec3,
) {
    wrench.force_world += force_world;
    wrench.torque_world +=
        (point_world - center_of_mass_world).cross(force_world);
}
```

This provides a large amount of handling character without physically constructing the ship from jointed rigid bodies.

Examples:

- A bow-mounted emitter makes the ship weathercock strongly into currents.
- A mismatched port emitter yaws the ship under high coupling.
- An aft-heavy cargo load changes turn response.
- A damaged stabilizer compensates at low load but saturates in violent flow.
- An oversized emitter produces more speed and more torque than the controller can comfortably tame.

---

# 8. Parts are logical effectors, not separate rigid bodies

The installed ship should remain one physical body.

Do not model normal installed components as:

- separate rigid bodies,
- jointed nacelles,
- physically constrained capacitors,
- motors trying to hold machinery together,
- or a stack of collision shapes fighting the solver.

That would spend the project's complexity budget on joint error, collision filtering, solver jitter, and catastrophic edge cases.

Instead, each installed part is a stateful logical effector:

```rust
struct InstalledPartState {
    part_id: PartId,
    mount_position_local: Vec2,
    health: f64,
    temperature: f64,
    response: f64,
    response_velocity: f64,
    fault_state: FaultState,
}
```

A part may contribute:

- force,
- torque,
- mass,
- inertia modifiers,
- heat,
- power demand,
- control delay,
- oscillation,
- sensor information,
- or presentation telemetry.

Only a component that actually breaks free needs to become its own debris body.

---

# 9. Good wobble comes from authored dynamics

There are two unrelated kinds of wobble.

## Bad wobble

- unstable timestep behavior,
- contact jitter,
- joint error,
- solver explosions,
- giant corrective impulses,
- frame-rate-dependent noise.

This communicates that the program is broken.

## Good wobble

- actuator lag,
- underdamped response,
- delayed coupling,
- asymmetric output,
- saturation,
- hysteresis,
- load-dependent oscillation.

This communicates that the ship is a machine.

A second-order response is a useful primitive:

```text
response'' + 2 * damping_ratio * frequency * response'
           + frequency^2 * response
           = frequency^2 * command
```

Interpretation:

- frequency controls response speed,
- damping ratio controls overshoot,
- a low damping ratio creates a stable oscillation,
- damage can alter either value,
- and mismatched port/starboard channels can use different values.

A worn stabilizer might begin a predictable left-right shimmy above a particular field load. The player can learn when it starts, compensate for it, or eventually use it deliberately.

Avoid white-noise torque. Turbulence should be spatially and temporally continuous, deterministic, and low-frequency enough to read.

The player should think:

> I know what this ship is doing.

Not:

> The game randomly stole my input.

---

# 10. The interior is a telemetry theater

The interior is not a simulated moving room. It is an instrumented set that stages the implications of navigation-space events.

## 10.1 Derive useful telemetry

After each physics step, derive values such as:

```text
linear_acceleration = (velocity_after - velocity_before) / dt
angular_acceleration = (angular_velocity_after - angular_velocity_before) / dt
```

Transform acceleration and collision impulses into ship-local coordinates.

Also retain authored system facts that cannot be reconstructed from body motion alone:

- port and starboard emitter load,
- steering controller saturation,
- stabilizer effort,
- drive spool,
- capacitor discharge,
- heat flow,
- structural stress estimate,
- field instability,
- and fault activation.

## 10.2 Presentation consumers

The interior can use telemetry to drive:

- camera lean and spring response,
- console vibration,
- loose-object animation,
- hanging cable sway,
- light dimming,
- power bus flicker,
- drive audio pitch,
- hull creaks,
- warning lamps,
- instrument needles,
- screen distortion,
- collision kicks,
- and dust or debris events.

Most of these should be authored filters, not one-to-one copies of raw acceleration.

A tiny solver twitch should not necessarily shake the camera. A dangerous stabilizer saturation might deserve a large audible groan even before body motion becomes dramatic.

## 10.3 Useful feedback, not only spectacle

The set should help the player understand the flight model.

Examples:

- The left side of the helm strains before a port-coupling kick.
- A stabilizer lamp reaches its limit before the ship begins to rotate away from the commanded line.
- The current direction is visible on a physical repeater display.
- Capacitor lights show how much emergency correction remains.
- A particular vibration frequency warns that the damaged controller is entering oscillation.
- Hull audio changes as field load approaches an unsafe region.

The goal is for immersive details to become additional sensory channels for systems mastery.

---

# 11. Rapier setup for the first prototype

A likely initial body configuration is:

- dynamic rigid body,
- one simple cuboid collider,
- gravity disabled,
- zero linear damping,
- zero or nearly zero solver angular damping,
- Y translation locked,
- X and Z rotation locked,
- XZ translation enabled,
- yaw around Y enabled,
- sleeping disabled or aggressively woken by commands and field forces,
- CCD enabled only when local obstacle speeds justify it.

The game-specific flight policy should remain in `rusty-space`, not move into Rusty Engine.

## Current Rusty Engine surface

As of the current `rusty-engine` main branch:

- `svc-collision` owns `rapier3d-f64` 0.34.
- Rigid bodies accept mass, velocity, damping, gravity scale, collision properties, forces, torque, impulses, and torque impulses.
- Supported public shapes are sphere, cuboid, and local-Y capsule.
- Inertia is currently derived from shape and mass.
- A candidate dynamics world is rebuilt from canonical state for each requested step and then published atomically.
- The caller owns fixed-step scheduling and gameplay-selected forces.

Relevant Rusty Engine paths:

```text
rust/crates/svc-collision/src/dynamics.rs
rust/crates/engine-spatial/src/rigid_body.rs
rust/crates/entity-state/src/rigid_body.rs
docs/topics/rigid-body-dynamics.md
```

## Likely small engine additions

### Planar axis locks

Expose generic per-axis translation and rotation locks on the rigid-body component or request surface.

This is broadly useful and not space-game policy.

### Optional explicit mass properties

The current derive-from-collider inertia may be enough for the first prototype. Later, ship handling may need an authored center of mass and principal inertia independent of the collision proxy.

Keep this optional and validated. Most part personality can remain in force generation rather than constantly mutating physical mass properties.

### No force-at-point API required initially

`rusty-space` can sum logical part forces and calculate their torque contribution before sending one force and one torque action.

A generic force-at-point helper may later be convenient, but it is not necessary to prove the game.

---

# 12. Recompute control forces every fixed step

Rusty Engine can request several Rapier substeps while applying one aggregated action across them.

That is suitable for a constant force, but the ship controller wants to react to changing state:

- angular velocity changes,
- local field samples change,
- stabilizers saturate,
- heat thresholds are crossed,
- and nonlinear coupling changes along the trajectory.

For the player ship, begin with one physics substep per gameplay fixed tick:

```text
sample body state
compute controller and field forces
step once
sample new state
repeat
```

Try 60 Hz first. Test 120 Hz if the steering controller or high-frequency field response needs it.

Because the current Rusty Engine service rebuilds Rapier state for each candidate, benchmark rather than theorize. One free body plus sparse obstacles should be inexpensive, but persistent contact-heavy scenes would lose more from rebuilding solver caches.

That is another reason not to turn the interior or installed modules into a pile of active rigid bodies.

---

# 13. Keep the force model solver-neutral

Even while testing Rapier as the authoritative integrator, keep the interesting force model in plain game code.

For example:

```rust
trait FlightIntegrator {
    fn step(
        &mut self,
        body: NavigationBodyState,
        wrench: ShipWrench,
        dt: f64,
    ) -> NavigationBodyState;
}
```

A simple semi-implicit Euler reference integrator can coexist with the Rapier path during prototyping.

This is useful because:

- force logic can be unit tested without Rapier,
- field behavior can be visualized cheaply,
- tuning problems can be separated from solver problems,
- and Rapier can be A/B tested against a simple reference.

The target feel should come from authored forces. Rapier should preserve and combine them, not be a black box expected to invent good handling.

---

# 14. Collision and local-scale transitions

Most space sailing happens in sparse local navigation fields.

Dense geometry should be reserved for places where it adds play:

- asteroid belts,
- wreck fields,
- station approaches,
- docking spaces,
- megastructure interiors,
- and dangerous salvage routes.

Use simple collision proxies and local chart units.

CCD is useful insurance against tunneling, but it should not be asked to rescue absurd scale choices.

A transition from strategic travel into a local obstacle chart can preserve an abstract normalized state:

- approach direction,
- relative speed,
- drive heat,
- field coupling,
- and entry timing.

It does not need to preserve literal interplanetary position or velocity units.

Collision output can feed:

- damage,
- cargo shifts,
- part faults,
- emergency uncoupling,
- and interior impact staging.

Again, the interior set itself does not collide with the asteroid. It receives the collision impulse and performs theater.

---

# 15. Debugging and tuning surfaces are essential

This design depends on understanding several interacting forces. Build observability before content.

The prototype should expose:

- current velocity vector,
- ship heading,
- desired angular velocity,
- each force contribution,
- each torque contribution,
- local field velocity,
- relative flow,
- controller saturation,
- actuator response state,
- part fault state,
- center of mass,
- centers of force,
- heat and power state,
- and telemetry sent to the interior.

Useful tools:

- vector overlays in the navigation view,
- time-series graphs for force and torque channels,
- per-system enable/disable toggles,
- deterministic input recording and replay,
- field visualization,
- slow motion,
- and side-by-side Rapier versus reference integration.

Without this, every handling problem becomes an occult ceremony involving six coefficients and a nervous restart button.

---

# 16. Prototype sequence

## Prototype A: dynamic inertial body

Prove:

- heading and velocity remain independent,
- releasing thrust preserves velocity,
- steering torque is readable,
- and the body remains stable with zero input.

No current field yet.

## Prototype B: one planetary wake

Add a simple analytic field:

- broad flow,
- curved wake,
- one region of stronger gradient.

Prove that coupling lets the environment bend the route without becoming global drag.

## Prototype C: three ship configurations

Fly the same route with:

### Healthy stock ship

- moderate coupling,
- well-damped steering,
- predictable response.

### Oversized scavenged emitter

- stronger field force,
- forward coupling point,
- pronounced weathercocking,
- better speed with worse control burden.

### Damaged stabilizer

- normal peak authority,
- slower response,
- underdamped oscillation,
- slight port/starboard asymmetry.

The test passes when a player can identify the configuration from handling alone.

## Prototype D: bridge set

Build only:

- helm,
- one engineering corner,
- a few lights,
- one instrument repeater,
- one loose prop,
- and basic audio layers.

Feed it real telemetry from Prototype C.

The set should make flight easier to read, not merely noisier.

## Prototype E: one collision space

Add a small asteroid or wreck approach.

Prove:

- local collision is stable,
- an impact becomes a useful impulse and damage event,
- the interior stages it convincingly,
- and the resulting fault changes the flight home.

---

# 17. Acceptance criteria

The initial implementation is promising when all of these are true:

- Releasing thrust never silently brakes the ship.
- Calm-space controls are predictable.
- Strong currents can contest the controller without making input feel irrelevant.
- Wobble is reproducible and learnable.
- Different parts change handling, not merely numeric performance.
- The same route supports safe and stylish solutions.
- The ship remains stable at rest with no random twitching.
- Interior cues reveal useful system state.
- The player can feel a repair or replacement before reading its stats.
- Physics mistakes usually create recoverable situations rather than immediate failure.

---

# 18. Explicit anti-goals

Do not build:

- real orbital mechanics,
- literal astronomical scale,
- a physically moving ship interior,
- a character standing inside a moving world-space rigid body,
- an articulated ship assembled from jointed modules,
- realistic fluid dynamics,
- random torque noise sold as turbulence,
- universal drag that erases inertia,
- raw solver instability sold as personality,
- or engine-level space-sailing policy.

The game is a deliberate stack of compatible fakes.

The important part is that the fakes exchange meaningful state.

---

# 19. Concise architecture

```text
RUSTY-SPACE GAMEPLAY

player command
    -> authored controller
    -> stateful logical parts
    -> fictional field sample
    -> named force/torque contributions
    -> net wrench

RUSTY-ENGINE / RAPIER

net wrench
    -> one constrained dynamic body
    -> pose + linear velocity + angular velocity
    -> collision impulses

RUSTY-SPACE PRESENTATION

motion + system telemetry
    -> 2D helm display
    -> static 3D bridge set
    -> camera, props, lights, audio, instruments
```

The implementation thesis in one sentence:

> **Use real integration for the relationship between forces, then use shameless stagecraft to make those forces feel like they are happening to a lived-in spaceship.**
