# Validación de CU15 y CU16

Pruebas automatizadas en LotesControllerTests.cs y ViasControllerTests.cs:
autenticación y rutas; autorización antes de consultar datos; filtros y espacios;
paginación inválida; identificadores inválidos; 404; detalle sin geometría;
Polygon/MultiPolygon y LineString/MultiLineString; auditoría y fallos no bloqueantes.

Ejecutar:
dotnet test tests/VisorDatosSIG.UnitTests/VisorDatosSIG.UnitTests.csproj
node --test tests/VisorDatosSIG.Web.Tests/consultas-entidades.test.cjs

Resultado automatizado (5 de octubre de 2026):
- 239 pruebas .NET aprobadas, incluidas 50 nuevas de CU15/CU16.
- 12 pruebas JavaScript aprobadas: filtros, JWT, paginación, respuestas obsoletas,
  texto seguro, geometría original, resultados vacíos y recuperación tras errores.
- Solución completa compilada (API, Web, Migrador y pruebas) sin errores ni advertencias.

Las pruebas JavaScript usan un DOM y Leaflet simulados: verifican el flujo del cliente,
pero no sustituyen la comprobación visual en navegador.

Verificación manual pendiente con SQL Server y datos migrados:
1. Ingresar y abrir Consultas → Lotes y Consultas → Vías.
2. Buscar sin filtros y combinar filtros; comparar resultados con SQL Server.
3. Cambiar página y límite. Limpiar un filtro numérico y repetir la búsqueda.
4. Seleccionar un registro: verificar atributos y geometría en el mapa.
5. Comprobar un registro sin geometría y una búsqueda sin coincidencias.
6. Probar con los cuatro actores y revocar PuedeVer: esperar 403.
7. Simular fallo de API/mapa base y comprobar mensajes y recuperación.
8. Probar a 360, 768 y 1366 px y en tema oscuro.
9. Buscar/seleccionar rápidamente: una respuesta antigua no debe reemplazar la selección actual.

Las pruebas unitarias usan repositorios falsos: no certifican acceso a SQL real,
rendimiento con datos migrados ni validación visual en navegador.
