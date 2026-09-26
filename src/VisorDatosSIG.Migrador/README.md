# VisorDatosSIG.Migrador

Aplicación de escritorio responsable de seleccionar, validar, previsualizar y migrar los archivos SHP hacia SQL Server.

Flujo previsto:
1. Selección de SHP.
2. Validación SHP/SHX/DBF/PRJ.
3. Verificación WGS 84 / SRID 4326.
4. Previsualización y mapeo.
5. Carga transaccional por lotes.
6. Progreso, cancelación, bitácora y resumen.
