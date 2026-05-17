using GillyTracker.Core.Sightings;

namespace GillyTracker.Core.Tests.Sightings;

public class CoordinateValidatorTests
{
    [Test]
    [Arguments(47.6205, -122.3493, true)]
    [Arguments(-90, -180, true)]
    [Arguments(90, 180, true)]
    [Arguments(90.0001, 0, false)]
    [Arguments(0, 180.001, false)]
    public async Task ValidateCoordinates(decimal latitude, decimal longitude, bool expected)
    {
        var result = CoordinateValidator.IsValid(latitude, longitude);
        await Assert.That(result).IsEqualTo(expected);
    }
}
