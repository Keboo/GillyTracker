namespace GillyTracker.Core.Sightings;

public static class CoordinateValidator
{
    public static bool IsValid(decimal latitude, decimal longitude)
    {
        return latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180;
    }
}
