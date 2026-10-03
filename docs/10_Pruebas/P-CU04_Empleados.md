# CU04 – Gestionar Empleados

Documento de especificación, verificación y pruebas para el caso de uso **CU04 – Gestionar Empleados** del sistema **VisorDatosSIG 2026**.

---

## 1. Propósito y Alcance

Permite al **Administrador** y al **Supervisor** registrar y mantener la información del personal que participa en las actividades operativas del sistema (bajas parciales, disponibilidad de personal, asignación de servicios y gestión de rutas).

Este caso de uso funciona como la **fuente de información básica del empleado** para el módulo de **Seguimiento de Servicios**.

---

## 2. Actores y Permisos

- **Administrador:** Acceso total implícito para consultar, registrar, modificar y deshabilitar empleados.
- **Supervisor:** Acceso autorizado mediante `dbo.RolMenu` sobre la ruta `/Empleados` (Ver, Crear, Editar, Eliminar).
- **Consultor / Usuarios no autorizados:** Acceso denegado (HTTP `403 Forbidden`).

---

## 3. Contratos de la API (`/api/empleados`)

| Método | Endpoint | Acción | Permiso | Códigos de Respuesta |
|---|---|---|---|---|
| **GET** | `/api/empleados` | Listar empleados con filtros (`busqueda`, `cargo`, `disponibilidad`, `activo`) | Ver | `200 OK`, `401`, `403` |
| **GET** | `/api/empleados/metricas` | Resumen cuantitativo (total, disponibles, en servicio, bajas parciales) | Ver | `200 OK`, `401`, `403` |
| **GET** | `/api/empleados/cargos` | Catálogo de cargos registrados | Ver | `200 OK`, `401`, `403` |
| **GET** | `/api/empleados/disponibilidades` | Catálogo de estados de disponibilidad | Ver | `200 OK`, `401`, `403` |
| **GET** | `/api/empleados/{id}` | Obtener detalle de un empleado por ID | Ver | `200 OK`, `404 Not Found`, `401`, `403` |
| **POST** | `/api/empleados` | Alta de nuevo empleado (valida código y CI únicos) | Crear | `201 Created`, `400 Bad Request`, `401`, `403` |
| **PUT** | `/api/empleados/{id}` | Modificar datos del empleado (nombres, apellidos, CI, cargo, etc.) | Editar | `200 OK`, `400 Bad Request`, `404`, `401`, `403` |
| **PATCH** | `/api/empleados/{id}/estado` | Baja lógica o reactivación del empleado | Editar | `200 OK`, `404`, `401`, `403` |
| **PATCH** | `/api/empleados/{id}/disponibilidad` | Actualizar disponibilidad operativa (Disponible, Asignado, Baja Parcial) con justificación | Editar | `200 OK`, `400`, `404`, `401`, `403` |

---

## 4. Estructura de Datos (`dbo.Empleados`)

- `IdEmpleado` (INT IDENTITY, PK): Identificador único.
- `Codigo` (NVARCHAR(20), UNIQUE): Código corporativo del empleado (ej. `EMP-001`).
- `Nombres` (NVARCHAR(100)): Nombres del personal.
- `Apellidos` (NVARCHAR(100)): Apellidos del personal.
- `DocumentoIdentidad` (NVARCHAR(30), UNIQUE): Cédula de identidad.
- `Telefono` (NVARCHAR(30), NULL): Número de contacto o celular.
- `Email` (NVARCHAR(150), NULL): Correo electrónico corporativo o personal.
- `Cargo` (NVARCHAR(100)): Puesto operativo (ej. Técnico de Campo, Supervisor de Zona, Conductor de Cuadrilla).
- `Area` (NVARCHAR(100)): Departamento o área (por defecto 'Operaciones').
- `Disponibilidad` (NVARCHAR(50)): Estado para asignaciones ('Disponible', 'En Servicio', 'Baja Parcial', 'Licencia', 'Vacaciones').
- `Activo` (BIT): Estado lógico del registro (1 = Activo, 0 = Baja lógica).
- `FechaRegistro` (DATETIME2(0)): Fecha de alta en el sistema.
- `FechaModificacion` (DATETIME2(0), NULL): Última fecha de actualización.
- `Observaciones` (NVARCHAR(500), NULL): Historial de justificaciones operativas y notas.

---

## 5. Auditoría en `dbo.Bitacora`

Todas las operaciones de mutación se auditan automáticamente:
- `ALTA_EMPLEADO`: Registro inicial con código, nombre y cargo.
- `ACTUALIZACION_EMPLEADO`: Cambios en la ficha del empleado.
- `BAJA_LOGICA_EMPLEADO` / `ACTIVACION_EMPLEADO`: Desactivación o reactivación.
- `CAMBIO_DISPONIBILIDAD_EMPLEADO`: Registro del nuevo estado y motivo (bajas parciales, asignación a ruta, etc.).

---

## 6. Procedimiento de Verificación Manual

1. **Aplicar Script de Base de Datos:**
   Ejecutar `database/scripts/08_Gestion_Empleados.sql` en SQL Server / Azure SQL para crear la tabla, el rol `Supervisor`, la opción de menú `/Empleados` y los datos de prueba.
2. **Iniciar Sesión:**
   Ingresar con un usuario Administrador (`admin` / `Admin123!`) o Supervisor.
3. **Acceder a la Pantalla:**
   Navegar a `/Empleados` desde el menú lateral dinámico o la URL directa.
4. **Verificación de KPIs:**
   Comprobar que las tarjetas superiores totalicen adecuadamente el personal disponible, en servicio y en baja parcial.
5. **Alta de Empleado:**
   Pulsar `+ Nuevo empleado`, llenar el formulario y guardar. Verificar que aparezca en la tabla y que el código duplicado se rechace.
6. **Modificación:**
   Pulsar el botón de editar en una fila, modificar el cargo o teléfono y guardar.
7. **Bajas Parciales y Disponibilidad:**
   Pulsar el icono de reloj/disponibilidad, seleccionar "Baja Parcial" e ingresar la justificación. Verificar el cambio de color del badge a naranja/rojo y la actualización del contador superior.
8. **Baja Lógica:**
   Desactivar a un empleado y comprobar que su estado cambie a Inactivo conservando su registro histórico.
