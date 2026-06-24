namespace ZoneGuide.Mobile.ViewModels;

public struct Distance {
    public double Meters { get; }
    public double Kilometers => Meters / 1000;
    public Distance(double meters) { Meters = meters; }
    public static Distance FromMeters(double meters) => new Distance(meters);
    public static Distance FromKilometers(double km) => new Distance(km * 1000);
}

public enum PinType { Place }
public class Pin { 
    public string Label { get; set; } 
    public string Address { get; set; } 
    public Microsoft.Maui.Devices.Sensors.Location Location { get; set; } 
    public PinType Type { get; set; } 
}

public class MapSpan {
    public Microsoft.Maui.Devices.Sensors.Location Center { get; }
    public Distance Radius { get; }
    public MapSpan(Microsoft.Maui.Devices.Sensors.Location center, Distance radius) { Center = center; Radius = radius; }
    public static MapSpan FromCenterAndRadius(Microsoft.Maui.Devices.Sensors.Location center, Distance radius) => new MapSpan(center, radius);
}
