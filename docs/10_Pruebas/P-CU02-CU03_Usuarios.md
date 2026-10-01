# CU02 Gestionar Contraseña y CU03 Gestionar Usuarios

Se conserva la numeración solicitada. El PDF Visor de datos Sig (2) usa CU03 y CU04 para estas funciones (página 42).

## Uso

- Iniciar la API y la Web con su configuración habitual y la base creada mediante database/scripts/05_Seguridad_Login.sql.
- Cualquier usuario autenticado puede pulsar su nombre/avatar y elegir Cambiar contraseña. Se abre un modal sobre la página actual; /Cuenta/Password redirige a Inicio y abre ese modal. Debe indicar la contraseña actual y confirmar una nueva de entre 8 y 200 caracteres, diferente de la actual.
- Un Administrador puede abrir /Usuarios: consultar y buscar cuentas, crear usuarios, editar nombre y roles, activar/desactivar y restablecer contraseñas. El login se conserva al editar. La baja es lógica para conservar referencias y bitácoras.
- Los roles disponibles se obtienen de la base de datos; se exige al menos uno. Las contraseñas se almacenan mediante PBKDF2-SHA256 y salt aleatoria.
- Los cambios de estado y roles se consultan al validar cada petición protegida, incluso con tokens emitidos anteriormente.

## API

| Método | Ruta | Permiso |
|---|---|---|
| PUT | /api/perfil/cambiar-password | Usuario autenticado, cuenta del token |
| GET | /api/usuarios y /api/usuarios/{id} | Administrador |
| GET | /api/usuarios/roles | Administrador |
| POST | /api/usuarios | Administrador |
| PUT | /api/usuarios/{id} | Administrador |
| PATCH | /api/usuarios/{id}/estado | Administrador |
| POST | /api/usuarios/{id}/restablecer-password | Administrador |

## Verificación

Pruebas automatizadas: GestionUsuariosTests cubre validación, confirmación, identidad, roles desconocidos/vacíos, login duplicado, usuarios inexistentes, bitácora y ausencia de secretos en respuestas. Complementa las pruebas existentes de autenticación y PBKDF2.

Prueba manual con SQL Server:

1. Como Consultor abrir el menú de usuario desde Inicio o Visor, elegir Cambiar contraseña y guardar; verificar que falla la anterior al iniciar sesión y funciona la nueva.
2. Intentar administrar usuarios como Consultor: debe impedir el acceso y la API devolver 403. Sin token, 401.
3. Como Administrador crear una cuenta, comprobar duplicados, editar nombre/roles y restablecer contraseña. Verificar el inicio con la contraseña restablecida.
4. Desactivar una cuenta con sesión abierta; su siguiente llamada protegida debe devolver 401. Reactivarla y verificar un nuevo inicio.
5. Retirar Administrador de una cuenta con token emitido; su siguiente petición administrativa debe devolver 403.
6. Probar cierre por Cancelar, Escape, clic fuera del modal y devolución del foco al botón de usuario. Verificar navegación con Tab y pantalla móvil.
7. Revisar bitácora: actor, usuario afectado, acción y resultado, sin contraseñas ni hashes. Probar formularios y tabla desde pantalla móvil.

## Límites

No se añade recuperación pública por correo ni eliminación física. La gestión se realiza en Web y API. El Migrador conserva sus pantallas existentes. Cambiar/restablecer contraseña no revoca tokens JWT ya emitidos; siguen su expiración habitual. Las sesiones de cuentas desactivadas y los permisos retirados sí se bloquean consultando el estado actual en cada petición. La base debe estar disponible para validar sesiones protegidas.
