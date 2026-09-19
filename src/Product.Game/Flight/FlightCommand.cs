namespace Rusty.Space.Product.Flight;

/// <summary>
/// What the player asked the ship's controls to attempt, never a pose request.
/// Throttle and turn are analog intents; coupling trim is the rate the coupling
/// actuator is wound at, negative being toward the uncoupled end; the
/// stabilizer switch decides whether released steering holds attitude or lets
/// rotation carry; an emergency uncouple dumps coupling to nothing at once.
/// </summary>
internal readonly record struct FlightCommand(
    double Throttle,
    double Turn,
    double CouplingTrim,
    bool StabilizerEnabled,
    bool EmergencyUncouple)
{
    /// <summary>
    /// What the controls report when nothing is touched: no drive, no turn, no
    /// trim demand, the attitude hold left engaged, and no emergency release.
    /// </summary>
    internal static FlightCommand Neutral { get; } = new(
        0.0,
        0.0,
        0.0,
        StabilizerEnabled: true,
        EmergencyUncouple: false);
}
