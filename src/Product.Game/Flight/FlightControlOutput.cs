namespace Rusty.Space.Product.Flight;

/// <summary>
/// One controller turn. Drive and steering stay separate so each source is
/// observable on its own, and each carries how hard its actuator was working
/// relative to what it could deliver.
/// </summary>
internal readonly record struct FlightControlOutput(
    FlightWrench Drive,
    FlightWrench Steering,
    double ThrottleLevel,
    double DriveEffort,
    double SteeringEffort,
    bool DriveSaturated,
    bool SteeringSaturated);
