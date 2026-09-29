# VisorDatosSIG.Migrador.UnitTests

Pruebas de la sesión del Migrador WinForms: configuración de la API (`ApiSettings`), sesión en
memoria (`UserSession`) y consumo HTTP de la autenticación (`AuthenticationApiClient`).

No requieren SQL Server ni la API en ejecución: el cliente HTTP se prueba con un manejador de
mensajes falso. Tampoco automatizan píxeles de los formularios: la apariencia se valida en la
prueba manual descrita en `src/VisorDatosSIG.Migrador/README.md`.
