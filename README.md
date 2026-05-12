# Costa Rica Fuel Prices

**Open-source API de precios de combustible para Costa Rica** — confiable, actualizada y lista para integrar en tus apps.

> **Demo & Documentación:** [crfuelpriceservice.netlify.app](https://crfuelpriceservice.netlify.app/)

---

## Por qué existe este proyecto

Los precios de combustible en Costa Rica son regulados por la **ARESEP** y publicados por **RECOPE**, pero no existe un API público, estable y bien documentado que los exponga de forma programática. Muchas apps ticas terminan haciendo scraping artesanal, rompible con cualquier cambio de layout, o simplemente no ofrecen esta funcionalidad.

Este proyecto nació para cambiar eso: una capa de acceso robusta, con múltiples fuentes de respaldo, validación cruzada de datos, historial completo y un API REST que cualquier desarrollador puede consumir sin preocuparse por los detalles del scraping.

---

## Características principales

- **Tres fuentes con cadena de prioridad** — RECOPE API (JSON oficial) → RECOPE HTML → ARESEP HTML → caché local. Si una fuente falla, la siguiente toma el relevo automáticamente.
- **Validación cruzada y detección de conflictos** — Compara los precios de la API y el HTML de RECOPE; si difieren más del 5%, baja el puntaje de confianza y registra el conflicto.
- **Puntaje de confianza** — Cada precio tiene un `confidenceScore` de 0.0 a 1.0 para que tu app sepa qué tan seguro es el dato (0.99 para API oficial, 0.30 para caché stale).
- **Historial completo** — Consulta precios históricos con filtros por fecha, tipo de combustible, fuente y más.
- **Monitoreo de salud de fuentes** — Endpoint dedicado que expone el estado en tiempo real de cada scraper.
- **Desglose de precio** — Precio sin impuesto, impuesto, margen de distribución y precio total al consumidor.
- **API versionada con Swagger** — Documentación interactiva disponible en `/docs`.
- **Rate limiting** — 100 solicitudes por minuto por IP.
- **Listo para producción** — Docker, PostgreSQL, health checks, retry con backoff exponencial, circuit breaker.

---

## Tipos de combustible

| Código | Descripción |
|---|---|
| `super` | Gasolina Súper |
| `regular` | Gasolina Regular |
| `diesel` | Diésel |
| `kerosene` | Keroseno |

---

## API Reference

Base URL: `https://crfuelpriceservice.netlify.app/api/v1`

### Precios actuales

```http
GET /fuel/latest
```

Retorna los precios oficiales vigentes para todos los tipos de combustible.

**Respuesta:**
```json
{
  "success": true,
  "data": [
    {
      "fuelType": "Super",
      "canonicalCode": "super",
      "price": 912.0,
      "currency": "CRC",
      "effectiveDate": "2025-10-25",
      "source": "RECOPE API",
      "confidenceScore": 0.99,
      "isStale": false,
      "priceWithoutTax": 690.5,
      "tax": 172.5,
      "averageMargin": 49.0
    }
  ]
}
```

---

### Precio por tipo de combustible

```http
GET /fuel/prices/{canonicalCode}
```

| Parámetro | Tipo | Descripción |
|---|---|---|
| `canonicalCode` | `string` | `super`, `regular`, `diesel` o `kerosene` |

---

### Historial de precios

```http
GET /fuel/history
```

| Query param | Tipo | Descripción |
|---|---|---|
| `from` | `yyyy-MM-dd` | Fecha de inicio |
| `to` | `yyyy-MM-dd` | Fecha de fin |
| `fuelType` | `string` | Tipo de combustible |
| `canonicalCode` | `string` | Código canónico |
| `source` | `string` | `RECOPE API`, `RECOPE HTML` o `ARESEP` |
| `hasConflict` | `bool` | Filtra registros con conflicto detectado |
| `page` | `int` | Número de página (default: 1) |
| `pageSize` | `int` | Tamaño de página (máximo: 100) |

---

### Salud de fuentes

```http
GET /fuel/sources/health
```

Retorna el estado de cada fuente de datos (Healthy / Degraded / Down), tiempo de respuesta promedio y número de fallos consecutivos.

---

### Metadata

```http
GET /fuel/metadata
```

Retorna los tipos de combustible disponibles, proveedores de datos, información regulatoria y zona horaria (America/Costa_Rica).

---

## Cómo funciona

```
┌─────────────────────────────────────────────────────────┐
│                    Source Arbiter                        │
│                                                         │
│  1. RECOPE API  ──(JSON oficial)──> confidence: 0.99    │
│       ↓ falla o dato vencido                            │
│  2. RECOPE HTML ──(scraping)──────> confidence: 0.92    │
│       ↓ falla                                           │
│  3. ARESEP HTML ──(regulador)─────> confidence: 0.85    │
│       ↓ todo falla                                      │
│  4. Caché local ──(last known)────> confidence: 0.30    │
└─────────────────────────────────────────────────────────┘
```

Los datos se actualizan automáticamente cada día a las **07:00 UTC** (1:00 AM hora Costa Rica). Si falla, reintenta a los 30 minutos.

Cada resultado incluye un **content hash** (SHA-256) para evitar duplicados, y la base de datos mantiene unicidad por `(FuelType, EffectiveDate)` cuando el precio está activo.

---

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| API | ASP.NET Core 8.0 (C#) |
| ORM | Entity Framework Core 8 |
| Base de datos | PostgreSQL 16 (prod) / SQLite (dev) |
| Scraping | HtmlAgilityPack + System.Text.Json |
| Resiliencia | Polly (retry + circuit breaker) |
| Documentación | Swagger / OpenAPI (Swashbuckle) |
| Contenedores | Docker (Alpine multi-stage) |
| CI | GitHub Actions |

---

## Correrlo localmente

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (opcional, para PostgreSQL)

### Con Docker Compose

```bash
git clone https://github.com/tu-usuario/costa-rica-fuel-prices.git
cd costa-rica-fuel-prices

cp .env.example .env
docker compose up --build
```

La API queda disponible en `http://localhost:8080` y Swagger en `http://localhost:8080/docs`.

### Sin Docker (SQLite local)

```bash
# Crea un appsettings.Development.json con:
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=costa_rica_fuel.db"
  }
}

dotnet run --project src/CRFuelScraper.API
```

Las migraciones se aplican automáticamente al iniciar.

---

## Variables de entorno

| Variable | Descripción | Ejemplo |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión (PostgreSQL o SQLite) | `Host=localhost;Port=5432;...` |
| `CORS_ORIGIN` | Origen permitido para CORS | `*` o `https://miapp.com` |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución | `Production` |

---

## Deployment

El proyecto está preparado para desplegarse en plataformas como **Render**, **Railway** o cualquier servicio que soporte contenedores Docker. El `Dockerfile` utiliza una imagen Alpine multi-stage para mantener el tamaño final mínimo.

Los endpoints de health check en `/health/ready` y `/health/live` son compatibles con cualquier orquestador (Kubernetes, Docker Swarm, etc.).

---

## Contribuir

Este es un proyecto open source y las contribuciones son bienvenidas. Si encontrás un bug, querés agregar una fuente nueva o mejorar la documentación, abrí un issue o un pull request.

Áreas donde hay trabajo pendiente o se agradece ayuda:

- Soporte para precios de GLP (gas licuado)
- Webhooks o notificaciones cuando cambian precios
- Endpoints de comparación histórica y variación porcentual
- Clientes SDK para JavaScript/TypeScript y Python
- Más apps ticas integrando el servicio :)

---

## Fuentes de datos

| Fuente | URL | Tipo |
|---|---|---|
| RECOPE API | `api.recope.go.cr` | JSON oficial |
| RECOPE HTML | `recope.go.cr/productos/precios-nacionales/tabla-precios/` | HTML |
| ARESEP | `aresep.go.cr/index.php/combustibles/precios` | HTML regulador |

Los precios son establecidos por la **Autoridad Reguladora de los Servicios Públicos (ARESEP)** y publicados por **RECOPE (Refinadora Costarricense de Petróleo)**. Este proyecto no es afiliado a ninguna de estas entidades; simplemente expone sus datos de forma accesible.

---

## Licencia

MIT — usalo, modificalo, integralo. Solo asegurate de que los datos que exponés son los oficiales.
