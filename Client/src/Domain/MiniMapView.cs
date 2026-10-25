namespace LibreKO.Domain;

public static class MiniMapView
{
    public static float ClampCenter(float position, float viewRadius, float extent) =>
        extent <= 2f * viewRadius ? extent * 0.5f : System.Math.Clamp(position, viewRadius, extent - viewRadius);
}
