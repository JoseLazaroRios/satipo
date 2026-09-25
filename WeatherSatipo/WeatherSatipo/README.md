# Detector de Incendios Forestales — Satipo, Junín

Versión mejorada del proyecto de clima, enfocada específicamente en el caso de uso
real: **monitoreo de condiciones de riesgo de incendio forestal en Satipo, Junín**.

## Qué cambió respecto a la versión anterior

1. **Ubicación fija**: ya no se pregunta la ciudad — el backend consulta siempre
   las coordenadas de Satipo (-11.2522, -74.6386) por latitud/longitud, que es más
   preciso que buscar por nombre de ciudad.
2. **Mapa interactivo (Leaflet + OpenStreetMap, gratis, sin API key)**: muestra un
   marcador exactamente sobre Satipo.
3. **Segunda API gratuita de OpenWeatherMap añadida: Air Pollution API**
   (`/data/2.5/air_pollution`). Da el índice AQI y el nivel de **PM2.5**, que es
   clave porque un incendio cercano eleva las partículas en el aire — es una señal
   temprana adicional a la temperatura/humedad.
4. **Reporte de riesgo de incendio**: el backend ya no devuelve solo el clima "en
   crudo", sino un reporte interpretado con:
   - Condiciones actuales (temperatura, humedad, viento, lluvia reciente)
   - Calidad del aire (AQI, PM2.5, interpretación)
   - **Nivel de riesgo** (Bajo / Moderado / Alto / Extremo) calculado con una
     heurística simple que combina temperatura + humedad + viento + lluvia
     reciente, con una recomendación asociada.

> ⚠️ El índice de riesgo es una **heurística educativa** creada para este
> proyecto, no un estándar oficial como el FWI canadiense o el índice McArthur.
> Puedes mencionarlo así en el informe, y como trabajo futuro proponer
> reemplazarlo por un índice validado.

## Otras APIs gratuitas de OpenWeatherMap que podrías sumar después

- **5 Day / 3 Hour Forecast** (`/data/2.5/forecast`) — para ver la tendencia de
  las próximas horas, no solo el estado actual.
- **Geocoding API** (`/geo/1.0/direct`) — si en el futuro quieres permitir varias
  zonas de Satipo (caseríos, sectores) en vez de un solo punto fijo.
- **One Call API 3.0** (tiene capa gratuita limitada) — junta clima actual,
  pronóstico y alertas meteorológicas oficiales en una sola llamada.

Todas estas se agregan igual que la de Air Pollution: una constante con la URL,
una llamada `HttpClient`, y un DTO para leer el JSON.

## Cómo ejecutarlo

### Desde Visual Studio
1. Abre `WeatherSatipo.csproj`.
2. Verifica que la API Key en `Program.cs` (constante `API_KEY`) sea la tuya.
3. F5. Se abre el navegador en `http://localhost:5095/` mostrando el mapa y el
   reporte directamente (ya no necesitas entrar a Swagger para verlo).

### Desde VS Code (terminal)
```
dotnet run
```
Y abre en el navegador la URL que aparezca (ej. `http://localhost:5095`).

Si quieres seguir probando el endpoint puro (sin interfaz), Swagger sigue
disponible en `http://localhost:5095/swagger`.
