# 03 - Arquitectura

## Arquitectura lógica

La solución se organiza por capas y módulos independientes:

```text
VisorDatosSIG.Web ───────→ VisorDatosSIG.Application ───────→ VisorDatosSIG.Domain
       │                           ↑
       │                           │
       └──── consume API           │
                                   │
VisorDatosSIG.Api ────────────────┤
       │                           │
       └────→ VisorDatosSIG.Infrastructure ─────→ SQL Server 2022
                                   │
                                   ├────→ Lectura SHP
                                   └────→ GeoJSON

VisorDatosSIG.Migrador ───────────→ Application / Infrastructure / Domain
```

## Reglas de dependencia
- Domain no depende de otros proyectos.
- Application depende de Domain.
- Infrastructure depende de Application y Domain.
- Api depende de Application e Infrastructure.
- Web depende de Application y consume servicios HTTP para los datos espaciales.
- Migrador depende de Application, Infrastructure y Domain.
- JavaScript nunca accede directamente a SQL Server.

## Flujo SIG principal

```text
SHP + SHX + DBF + PRJ
        ↓
Validación
        ↓
Migrador
        ↓
SQL Server (geometrías SRID 4326)
        ↓
API
        ↓
GeoJSON
        ↓
Leaflet
        ↓
Mapa / capas / identificación
```
