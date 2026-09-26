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
