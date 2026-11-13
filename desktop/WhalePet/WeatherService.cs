using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace WhalePet;

public sealed class WeatherInfo
{
    public string Place { get; init; } = "";
    public double Temperature { get; init; }
    public double FeelsLike { get; init; }
    public double Humidity { get; init; }
    public double Precipitation { get; init; }
    public double Rain { get; init; }
    public double Snow { get; init; }
    public double WindSpeed { get; init; }
    public int WindLevel { get; init; }
    public string WindDirection { get; init; } = "";
    public int WeatherCode { get; init; }
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "";
    public bool IsDay { get; init; } = true;
    public DateTime UpdatedAt { get; init; } = DateTime.Now;

    public string PrecipitationText => Snow > 0.01
        ? $"降雪 {Snow:0.#} mm"
        : Rain > 0.01 || Precipitation > 0.01
            ? $"降雨 {Math.Max(Rain, Precipitation):0.#} mm"
            : "无降雨";

    public string Summary => $"{Place} · {Icon} {Math.Round(Temperature)}°C · {Description}";
    public string Detail => $"{Icon} {Math.Round(Temperature)}°C（体感 {Math.Round(FeelsLike)}°C） · {Description} · {PrecipitationText} · {WindDirection}风 {WindLevel} 级 · 湿度 {Math.Round(Humidity)}%";
}

public sealed class WeatherService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public async Task<WeatherInfo?> GetAsync(PetSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var location = await ResolveLocationAsync(settings, cancellationToken);
            if (location is null) return null;

            var (latitude, longitude, place) = location.Value;
            var url = "https://api.open-meteo.com/v1/forecast"
                + "?latitude=" + latitude.ToString("0.####", CultureInfo.InvariantCulture)
                + "&longitude=" + longitude.ToString("0.####", CultureInfo.InvariantCulture)
                + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,rain,showers,snowfall,weather_code,wind_speed_10m,wind_direction_10m,is_day"
                + "&timezone=auto";

            using var document = JsonDocument.Parse(await Client.GetStringAsync(url, cancellationToken));
            var current = document.RootElement.GetProperty("current");
            var code = current.GetProperty("weather_code").GetInt32();
            var windSpeed = current.GetProperty("wind_speed_10m").GetDouble();
            var windDegrees = current.GetProperty("wind_direction_10m").GetDouble();
            var temperature = current.GetProperty("temperature_2m").GetDouble();

            return new WeatherInfo
            {
                Place = place,
                Temperature = temperature,
                FeelsLike = current.GetProperty("apparent_temperature").GetDouble(),
                Humidity = current.GetProperty("relative_humidity_2m").GetDouble(),
                Precipitation = current.GetProperty("precipitation").GetDouble(),
                Rain = current.GetProperty("rain").GetDouble() + current.GetProperty("showers").GetDouble(),
                Snow = current.GetProperty("snowfall").GetDouble(),
                WindSpeed = windSpeed,
                WindLevel = BeaufortLevel(windSpeed),
                WindDirection = WindDirectionName(windDegrees),
                WeatherCode = code,
                Description = WeatherDescription(code),
                Icon = WeatherIcon(code, current.GetProperty("is_day").GetInt32() == 1),
                IsDay = current.GetProperty("is_day").GetInt32() == 1,
                UpdatedAt = DateTime.Now,
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(double Latitude, double Longitude, string Place)?> ResolveLocationAsync(
        PetSettings settings,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.WeatherCity))
        {
            try
            {
                var city = Uri.EscapeDataString(settings.WeatherCity.Trim());
                using var document = JsonDocument.Parse(await Client.GetStringAsync(
                    "https://geocoding-api.open-meteo.com/v1/search?name=" + city + "&count=1&language=zh&format=json",
                    cancellationToken));
                if (document.RootElement.TryGetProperty("results", out var results)
                    && results.ValueKind == JsonValueKind.Array
                    && results.GetArrayLength() > 0)
                {
                    var hit = results[0];
                    var name = hit.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? settings.WeatherCity.Trim() : settings.WeatherCity.Trim();
                    var admin = hit.TryGetProperty("admin1", out var adminElement) ? adminElement.GetString() ?? "" : "";
                    var place = string.IsNullOrWhiteSpace(admin) || string.Equals(admin, name, StringComparison.OrdinalIgnoreCase)
                        ? name
                        : name + " · " + admin;
                    return (hit.GetProperty("latitude").GetDouble(), hit.GetProperty("longitude").GetDouble(), place);
                }
            }
            catch
            {
                // Fall back to IP location when the city is unknown or offline.
            }
        }

        try
        {
            using var document = JsonDocument.Parse(await Client.GetStringAsync("https://ipwho.is/", cancellationToken));
            var root = document.RootElement;
            if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True)
            {
                var city = root.TryGetProperty("city", out var cityElement) ? cityElement.GetString() ?? "" : "";
                var region = root.TryGetProperty("region", out var regionElement) ? regionElement.GetString() ?? "" : "";
                var place = string.IsNullOrWhiteSpace(city) ? region : city;
                if (root.TryGetProperty("latitude", out var latitudeElement)
                    && root.TryGetProperty("longitude", out var longitudeElement))
                {
                    return (latitudeElement.GetDouble(), longitudeElement.GetDouble(), string.IsNullOrWhiteSpace(place) ? "本地" : place);
                }
            }
        }
        catch
        {
            // Fall through to the second locator.
        }

        try
        {
            using var document = JsonDocument.Parse(await Client.GetStringAsync("https://ipapi.co/json/", cancellationToken));
            var root = document.RootElement;
            if (root.TryGetProperty("latitude", out var latitudeElement)
                && root.TryGetProperty("longitude", out var longitudeElement))
            {
                var city = root.TryGetProperty("city", out var cityElement) ? cityElement.GetString() ?? "" : "";
                return (latitudeElement.GetDouble(), longitudeElement.GetDouble(), string.IsNullOrWhiteSpace(city) ? "本地" : city);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string WeatherDescription(int code) => code switch
    {
        0 => "晴",
        1 => "大部晴朗",
        2 => "多云",
        3 => "阴",
        45 or 48 => "有雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 => "小雨",
        63 => "中雨",
        65 => "大雨",
        66 or 67 => "冻雨",
        71 => "小雪",
        73 => "中雪",
        75 => "大雪",
        77 => "米雪",
        80 => "阵雨",
        81 => "较强阵雨",
        82 => "强阵雨",
        85 => "阵雪",
        86 => "强阵雪",
        95 => "雷雨",
        96 or 99 => "雷雨伴冰雹",
        _ => "天气变化",
    };

    private static string WeatherIcon(int code, bool isDay) => code switch
    {
        0 => isDay ? "☀" : "☾",
        1 or 2 => isDay ? "🌤" : "☁",
        3 => "☁",
        45 or 48 => "🌫",
        51 or 53 or 55 or 56 or 57 => "🌦",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "🌧",
        71 or 73 or 75 or 77 or 85 or 86 => "❄",
        95 or 96 or 99 => "⛈",
        _ => "🌤",
    };

    private static int BeaufortLevel(double kilometersPerHour)
    {
        double[] limits = { 1, 5, 11, 19, 28, 38, 49, 61, 74, 88, 102, 117 };
        for (var index = 0; index < limits.Length; index++)
        {
            if (kilometersPerHour < limits[index]) return index;
        }
        return 12;
    }

    private static string WindDirectionName(double degrees)
    {
        string[] names = { "北", "东北", "东", "东南", "南", "西南", "西", "西北" };
        var index = (int)Math.Round(((degrees % 360) + 360) % 360 / 45.0) % 8;
        return names[index];
    }
}
