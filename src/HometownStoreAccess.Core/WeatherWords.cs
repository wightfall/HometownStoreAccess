namespace HometownStoreAccess.Core
{
    /// <summary>
    /// How the weather looks (game 1.7.12). The game rolls rain at strength 20-100 and fog at 30-100 and
    /// draws it denser the stronger it is: the mod says light, moderate or heavy rain (thin, moderate or
    /// thick fog, as some languages word them differently).
    /// </summary>
    public static class WeatherWords
    {
        /// <summary>"light", "moderate" or "heavy" for a strength 0-100.</summary>
        public static string Strength(int intensity)
        {
            if (intensity < 40) return "light";
            if (intensity < 70) return "moderate";
            return "heavy";
        }

        /// <summary>
        /// The text key: "weather_clear", or "weather_rain_light" ... "weather_fog_heavy".
        /// <paramref name="kind"/> is the game's weather name (Clear, Rain, Fog); an unknown one (a later
        /// game version) is null.
        /// </summary>
        public static string Key(string kind, int intensity)
        {
            switch (kind)
            {
                case "Clear": return "weather_clear";
                case "Rain": return "weather_rain_" + Strength(intensity);
                case "Fog": return "weather_fog_" + Strength(intensity);
                default: return null;
            }
        }
    }
}
