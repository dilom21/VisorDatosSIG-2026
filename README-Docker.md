# VisorDatosSIG 2026 - Docker

Guía para construir y ejecutar el sistema Web del VisorDatosSIG mediante Docker.

## 1. Arquitectura

El despliegue utiliza tres contenedores:

Navegador
    |
    v
Nginx :80
    |
    +-- /      -> VisorDatosSIG.Web :8080
    |
    +-- /api/  -> VisorDatosSIG.Api :8080
                          |
                          v
                      Azure SQL

La base de datos VisorDatosSIG ya se encuentra desplegada en Azure SQL y no se ejecuta dentro de Docker.

VisorDatosSIG.Migrador tampoco se containeriza porque es una aplicación WinForms para Windows.

## 2. Servicios Docker

- nginx: punto único de entrada HTTP.
- web: aplicación ASP.NET Core MVC.
- api: API ASP.NET Core.
- Azure SQL: base de datos externa existente.

Web y API utilizan únicamente la red interna de Docker.

Solo Nginx publica un puerto hacia el host.

## 3. Archivos principales

- Dockerfile.Api
- Dockerfile.Web
- docker-compose.yml
- nginx/nginx.conf
- .dockerignore
- .env.example
- README-Docker.md

## 4. Requisitos

- Docker Engine o Docker Desktop.
- Docker Compose.
- Conectividad hacia Azure SQL.
- Credenciales válidas de Azure SQL.
- Clave JWT segura.

No es necesario instalar .NET en el servidor porque las imágenes Docker incluyen el runtime necesario.

## 5. Variables de entorno

Crear un archivo .env a partir de .env.example.

Windows PowerShell:

    Copy-Item .env.example .env

Linux / AWS EC2:

    cp .env.example .env

Variables utilizadas:

- PUBLIC_BASE_URL
- HTTP_PORT
- ConnectionStrings__VisorDatosSIG
- JWT_ISSUER
- JWT_AUDIENCE
- JWT_EXPIRATION_MINUTES
- Jwt__Key

Ejemplo local:

    PUBLIC_BASE_URL=http://localhost
    HTTP_PORT=80

La cadena de conexión de Azure SQL y la clave JWT deben configurarse únicamente mediante variables de entorno o el archivo .env local.

Nunca se debe subir .env al repositorio.

## 6. Validar Docker Compose

Antes de levantar el sistema:

    docker compose config --quiet

Si el comando termina sin errores, la configuración es válida.

## 7. Construir y levantar

Ejecutar:

    docker compose up -d --build

Verificar:

    docker compose ps

Los servicios esperados son:

- api
- web
- nginx

Solo Nginx debe mostrar un puerto publicado hacia el host.

## 8. Acceso local

Abrir en el navegador:

    http://localhost

Nginx enruta:

    /       -> Web
    /api/   -> API

No es necesario acceder directamente a Web o API mediante puertos separados.

## 9. Logs

Todos los servicios:

    docker compose logs -f

API:

    docker compose logs -f api

Web:

    docker compose logs -f web

Nginx:

    docker compose logs -f nginx

Para salir de los logs:

    Ctrl + C

## 10. Reiniciar

    docker compose restart

Después verificar:

    docker compose ps

## 11. Detener

    docker compose down

Esto elimina los contenedores y la red creada por Docker Compose.

Los datos no se pierden porque la base de datos está almacenada externamente en Azure SQL.

Para volver a levantar:

    docker compose up -d

## 12. Reconstruir después de cambios

    docker compose up -d --build

Reconstrucción completa sin caché:

    docker compose build --no-cache
    docker compose up -d

## 13. Base de datos

La API utiliza Azure SQL mediante la variable:

    ConnectionStrings__VisorDatosSIG

La cadena real de conexión no debe almacenarse en:

- Dockerfile
- docker-compose.yml
- appsettings.json
- Git

Debe mantenerse como secreto de entorno.

## 14. JWT

La API obtiene la clave JWT mediante:

    Jwt__Key

La clave JWT tampoco debe almacenarse en Git ni dentro de las imágenes Docker.

## 15. Migrador WinForms

VisorDatosSIG.Migrador continúa ejecutándose desde Windows y no forma parte de Docker Compose.

Para consumir una API desplegada remotamente puede configurarse:

    VISORDATOSSIG_API_URL=https://dominio-del-visor

El Migrador no necesita un contenedor propio.

## 16. Prueba funcional mínima

Después de levantar el sistema se debe comprobar:

1. http://localhost responde correctamente.
2. El login funciona.
3. Se muestra el usuario y su rol.
4. El sidebar dinámico carga.
5. El Visor carga el mapa.
6. Manzanas carga correctamente.
7. Lotes carga correctamente.
8. Códigos Fijos carga correctamente.
9. Vías carga correctamente.
10. Usuarios carga correctamente.
11. Roles y permisos cargan correctamente.
12. Bitácora carga correctamente.
13. El cierre de sesión funciona.
14. Las rutas protegidas requieren autenticación.

## 17. Flujo hacia Azure SQL

Navegador
    |
    v
Nginx
    |
    +--> Web
    |
    +--> API
           |
           v
       Azure SQL

Los datos no se almacenan dentro de los contenedores.

## 18. Despliegue en AWS EC2

En EC2:

    git clone <URL_DEL_REPOSITORIO>
    cd VisorDatosSIG-2026
    cp .env.example .env

Editar las variables reales:

    nano .env

Validar y levantar:

    docker compose config --quiet
    docker compose up -d --build
    docker compose ps

Nginx debe ser el único servicio expuesto públicamente.

Los puertos internos de Web y API no deben abrirse en el Security Group.

## 19. Actualización en AWS

    git pull origin main
    docker compose up -d --build
    docker compose ps

## 20. Seguridad

No versionar:

- .env
- contraseñas de Azure SQL
- claves JWT
- credenciales AWS
- secretos de producción

Sí se versiona:

    .env.example

pero únicamente como plantilla y sin valores secretos.

## 21. Estado validado localmente

La arquitectura fue comprobada localmente utilizando:

- Docker Compose
- Nginx
- VisorDatosSIG.Web
- VisorDatosSIG.Api
- Azure SQL

Flujo validado:

    Navegador -> Nginx -> Web/API -> Azure SQL

También se validó autenticación JWT y carga de información geográfica real.
