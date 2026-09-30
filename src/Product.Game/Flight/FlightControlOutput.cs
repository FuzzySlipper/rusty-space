namespace Rusty.Space.Product.Flight;

/// <summary>Controller demands and the speed ceiling's intervention.</summary>
internal readonly record struct FlightControlOutput(
    FlightWrench Drive,
    FlightWrench Steering,
    double DriveEffort,
    bool DriveSaturated);
