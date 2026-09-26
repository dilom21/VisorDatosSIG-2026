# VisorDatosSIG 2026

Proyecto Integrador de la materia **Sistemas de Información Geográfica** - UAGRM FICCT.

## Objetivo
Desarrollar una solución SIG web para migrar datos ESRI Shapefile en WGS 84 / SRID 4326 hacia SQL Server 2022 y permitir su visualización, identificación y consulta desde un visor web responsivo.

## Componentes
- **VisorDatosSIG.Domain**: entidades y reglas de dominio.
- **VisorDatosSIG.Application**: casos de uso, DTOs, validaciones e interfaces.
- **VisorDatosSIG.Infrastructure**: SQL Server, repositorios, lectura SHP y serialización GeoJSON.
- **VisorDatosSIG.Api**: servicios HTTP para capas, consultas y autenticación.
- **VisorDatosSIG.Web**: interfaz ASP.NET Core MVC/Razor, Leaflet y JavaScript.
- **VisorDatosSIG.Migrador**: aplicación de escritorio para validar y migrar SHP a SQL Server.
- **tests**: pruebas unitarias y de integración.
- **database**: scripts SQL, diagramas y notas de instalación.
- **docs**: documentación técnica, evidencias y manuales.

## Datos geográficos
- Manzanas.shp
- Lotes.shp
- CodigosFijos.shp
- Vias.shp

Sistema de referencia: **WGS 84 / EPSG:4326**.

## Tecnologías
- C# / .NET 10
- ASP.NET Core MVC / Web API
- SQL Server 2022
- Entity Framework Core o acceso equivalente
- NetTopologySuite
- Leaflet
- GeoJSON
- Bootstrap 5
- Git / GitHub

## Estrategia de ramas
- `main`: versión estable.
- `pruebas`: integración y validación.
- `dev-josias`
- `dev-harold`
- `dev-salet`
- `dev-alastor`

Flujo recomendado: `dev-*` → Pull Request → `pruebas` → validación → Pull Request → `main`.

## Regla de arquitectura
El frontend nunca se conecta directamente a SQL Server. Los datos espaciales se exponen desde el servidor mediante servicios y GeoJSON.
