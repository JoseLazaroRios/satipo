using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseDefaultFiles();   // sirve wwwroot/index.html en "/"
app.UseStaticFiles();    // permite cargar JS/CSS/imagenes de wwwroot
app.UseSwagger();
app.UseSwaggerUI();

// ---- Configuración ----
// Pega aquí tu API Key de https://openweathermap.org/api
const string API_KEY = "8c060772551de8f5af7b485fee9c09cf";
const string WEATHER_URL = "https://api.openweathermap.org/data/2.5/weather";
const string AIR_POLLUTION_URL = "https://api.openweathermap.org/data/2.5/air_pollution";

// Coordenadas fijas: Satipo, Junín, Perú
const double SATIPO_LAT = -11.2522;
const double SATIPO_LON = -74.6386;

// ---------------------------------------------------------------------------
// GET /api/reporte-incendio
// Reporte combinado (clima + calidad del aire + índice de riesgo) para Satipo
// ---------------------------------------------------------------------------
app.MapGet("/api/reporte-incendio", async (IHttpClientFactory httpClientFactory) =>
{
    var http = httpClientFactory.CreateClient();

    // 1) Clima actual por coordenadas
    var weatherUrl = $"{WEATHER_URL}?lat={SATIPO_LAT}&lon={SATIPO_LON}&appid={API_KEY}&units=metric&lang=es";
    var weatherResp = await http.GetAsync(weatherUrl);
    if (!weatherResp.IsSuccessStatusCode)
        return Results.Problem("No se pudo obtener el clima de Satipo. Revisa tu API Key.");
    var weather = await weatherResp.Content.ReadFromJsonAsync<OpenWeatherResponse>();

    // 2) Calidad del aire (útil para detectar humo / partículas de incendios cercanos)
    var airUrl = $"{AIR_POLLUTION_URL}?lat={SATIPO_LAT}&lon={SATIPO_LON}&appid={API_KEY}";
    var airResp = await http.GetAsync(airUrl);
    AirPollutionResponse? air = airResp.IsSuccessStatusCode
        ? await airResp.Content.ReadFromJsonAsync<AirPollutionResponse>()
        : null;

    if (weather is null)
        return Results.Problem("Respuesta vacía del proveedor de clima.");

    // 3) Cálculo del índice de riesgo de incendio (heurística simplificada)
    var temp = weather.Main.Temp;
    var humedad = weather.Main.Humidity;
    var vientoMs = weather.Wind.Speed;
    var lluviaUltimaHora = weather.Rain?.OneHour ?? 0;

    var riesgo = CalcularRiesgoIncendio(temp, humedad, vientoMs, lluviaUltimaHora);

    var airData = air?.List?.FirstOrDefault();
    var pm25 = airData?.Components.Pm2_5 ?? 0;
    var aqi = airData?.Main.Aqi ?? 0;

    return Results.Ok(new
    {
        ubicacion = new
        {
            nombre = "Satipo, Junín, Perú",
            latitud = SATIPO_LAT,
            longitud = SATIPO_LON
        },
        condicionesActuales = new
        {
            temperaturaC = temp,
            sensacionTermicaC = weather.Main.FeelsLike,
            humedadPorcentaje = humedad,
            vientoMs = vientoMs,
            descripcion = weather.Weather.FirstOrDefault()?.Description ?? "sin datos",
            lluviaUltimaHoraMm = lluviaUltimaHora
        },
        calidadDelAire = new
        {
            indiceAqi = aqi,                       // 1=Buena ... 5=Muy mala (escala OpenWeather)
            categoriaAqi = CategoriaAqi(aqi),
            pm2_5 = pm25,
            interpretacion = pm25 > 35
                ? "Nivel de PM2.5 elevado: posible presencia de humo en el aire."
                : "Nivel de PM2.5 dentro de rango normal."
        },
        riesgoIncendio = riesgo,
        consultadoEn = DateTime.UtcNow
    });
});

// Endpoint simple anterior (se mantiene por si lo sigues usando en Swagger)
app.MapGet("/clima/{ciudad}", async (string ciudad, IHttpClientFactory httpClientFactory) =>
{
    var http = httpClientFactory.CreateClient();
    var url = $"{WEATHER_URL}?q={Uri.EscapeDataString(ciudad)}&appid={API_KEY}&units=metric&lang=es";
    var response = await http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        return Results.Problem("No se pudo obtener el clima. Revisa el nombre de la ciudad o tu API Key.");
    var data = await response.Content.ReadFromJsonAsync<OpenWeatherResponse>();
    if (data is null) return Results.Problem("Respuesta vacía del proveedor de clima.");
    return Results.Ok(new
    {
        ciudad = data.Name,
        temperatura = $"{data.Main.Temp} °C",
        sensacionTermica = $"{data.Main.FeelsLike} °C",
        descripcion = data.Weather.FirstOrDefault()?.Description ?? "sin datos",
        humedad = $"{data.Main.Humidity}%"
    });
});

app.Run();

// ---------------------------------------------------------------------------
// Índice de riesgo de incendio (heurística educativa, NO es un índice oficial
// como el FWI canadiense o el McArthur; sirve como aproximación simple
// combinando temperatura, humedad, viento y lluvia reciente).
// ---------------------------------------------------------------------------
static object CalcularRiesgoIncendio(double tempC, int humedadPct, double vientoMs, double lluviaMm)
{
    int puntaje = 0;

    if (tempC >= 32) puntaje += 2;
    else if (tempC >= 27) puntaje += 1;

    if (humedadPct <= 30) puntaje += 2;
    else if (humedadPct <= 50) puntaje += 1;

    if (vientoMs >= 8) puntaje += 2;
    else if (vientoMs >= 4) puntaje += 1;

    if (lluviaMm > 0) puntaje -= 2; // lluvia reciente reduce el riesgo

    puntaje = Math.Clamp(puntaje, 0, 6);

    var (nivel, color, recomendacion) = puntaje switch
    {
        >= 5 => ("Extremo", "#d32f2f", "Evitar cualquier fuente de ignición. Vigilancia activa y alerta a la población recomendada."),
        >= 3 => ("Alto", "#f57c00", "Condiciones favorables para propagación rápida. Reforzar monitoreo de sensores."),
        >= 1 => ("Moderado", "#fbc02d", "Mantener monitoreo regular; sin acciones urgentes por ahora."),
        _ => ("Bajo", "#388e3c", "Condiciones desfavorables para el fuego. Monitoreo de rutina.")
    };

    return new { nivel, puntaje, color, recomendacion };
}

static string CategoriaAqi(int aqi) => aqi switch
{
    1 => "Buena",
    2 => "Aceptable",
    3 => "Moderada",
    4 => "Mala",
    5 => "Muy mala",
    _ => "Sin datos"
};

// ---------------------------------------------------------------------------
// DTOs para leer las respuestas JSON de OpenWeatherMap
// ---------------------------------------------------------------------------
class OpenWeatherResponse
{
    public string Name { get; set; } = "";
    public MainData Main { get; set; } = new();
    public List<WeatherData> Weather { get; set; } = new();
    public WindData Wind { get; set; } = new();
    public RainData? Rain { get; set; }
}
class MainData
{
    public double Temp { get; set; }
    [JsonPropertyName("feels_like")] public double FeelsLike { get; set; }
    public int Humidity { get; set; }
}
class WeatherData
{
    public string Description { get; set; } = "";
}
class WindData
{
    public double Speed { get; set; }
}
class RainData
{
    [JsonPropertyName("1h")] public double OneHour { get; set; }
}

class AirPollutionResponse
{
    public List<AirPollutionEntry> List { get; set; } = new();
}
class AirPollutionEntry
{
    public AirPollutionMain Main { get; set; } = new();
    public AirPollutionComponents Components { get; set; } = new();
}
class AirPollutionMain
{
    public int Aqi { get; set; }
}
class AirPollutionComponents
{
    [JsonPropertyName("pm2_5")] public double Pm2_5 { get; set; }
    public double Pm10 { get; set; }
    public double Co { get; set; }
}
