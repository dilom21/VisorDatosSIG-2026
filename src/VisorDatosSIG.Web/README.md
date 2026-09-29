# VisorDatosSIG.Web

Interfaz ASP.NET Core MVC/Razor del sistema.

La parte SIG del frontend se organiza en módulos JavaScript separados:
- map.js
- geojson.js
- layers.js
- markers.js
- legend.js
- identify.js

La aplicación web no debe conectarse directamente a SQL Server; consumirá servicios del backend/API.

## Puerto de desarrollo

`Properties/launchSettings.json` (versionado, sin secretos) ejecuta el visor en
`http://localhost:5000` durante Development:

```bash
dotnet run --project src/VisorDatosSIG.Web
```

## CORS

El navegador llama a la API (`http://localhost:5080`) desde otro origen
(`http://localhost:5000`), por lo que la **API** autoriza ese origen con la política `VisorWeb`
(ver `src/VisorDatosSIG.Api/README.md`, sección CORS). Los orígenes autorizados se configuran en la
API (`Cors:AllowedOrigins` / `Cors__AllowedOrigins__0`), nunca en este proyecto, y no se usan
comodines.

La autenticación seguirá siendo con token JWT en el encabezado `Authorization` (no se usan cookies),
por lo que CORS no habilita credenciales.

