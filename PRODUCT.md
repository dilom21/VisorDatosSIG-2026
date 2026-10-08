# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users
- Técnicos, ingenieros y administradores SIG de la cooperativa/entidad territorial de Santa Cruz de la Sierra.
- Supervisores y operadores de campo (inspecciones de acometidas, cortes, reconexiones y relevamiento predial).
- Ciudadanos, directivos, consultores y visitantes que exploran la información cartográfica y catastral pública.

## Product Purpose
Plataforma Geoespacial Inteligente para la gestión, migración, visualización y análisis del catastro territorial y servicios públicos de Santa Cruz de la Sierra. Transforma datos espaciales complejos (manzanas, parcelas catastrales, red vial y acometidas de servicio) en una experiencia visual intuitiva, accesible y orientada a la toma de decisiones.

## Positioning
Geoportal institucional y técnico de vanguardia que integra cartografía vectorial en tiempo real, monitoreo de estados de servicio (códigos fijos con semaforización operativa) y reportería catastral avanzada, ofreciendo la potencia de un SIG profesional con la fluidez y simplicidad de una web moderna de alto impacto.

## Operating Context
- Estaciones de trabajo y monitores de alta resolución en dependencias técnicas (consulta predial, análisis de capas, exportación de reportes institucionales).
- Dispositivos móviles y tablets en trabajo de campo (geolocalización de códigos fijos, verificación de linderos, comprobación de estado de servicios).
- Portal público de bienvenida: primer punto de contacto institucional para nuevos usuarios y ciudadanía.

## Capabilities and Constraints
- Cuatro capas vectoriales normalizadas: Manzanas (863), Lotes catastrales (15.280), Red Vial (578 vías) y Códigos Fijos de servicios (6.261 acometidas).
- Estados operativos con semáforo de color: Normal (verde), Para Corte (ámbar), Cortado (rojo), Bajas (naranja/gris).
- Búsqueda predictiva georreferenciada y visor Leaflet de alta precisión.
- Autenticación segura y control de roles (Admin, Supervisor, Operador, Consulta).
- Stack: ASP.NET Core MVC + Web API C#, PostGIS / Base de datos espacial, Leaflet.js, CSS con sistema de tokens institucionales.
- Restricción estricta de entorno: Prohibido ejecutar comandos git (add, commit, push).

## Brand Commitments
- Nombre oficial: VisorDatosSIG.
- Sistema cromático institucional: Verde bosque profundo (`#183d37`), Terracota cálido cruceño (`#cf8057`), superficies marfil/blancas limpias y modo oscuro integrado.
- Carácter: Institucional, técnico, prestigioso y moderno. Debe proyectar solidez gubernamental/cooperativa y excelencia tecnológica.

## Evidence on Hand
- Datos territoriales reales auditados: 15.280 lotes, 863 manzanas urbanas, 578 ejes viales, 6.261 acometidas georreferenciadas.
- Cartografía histórica y moderna de Santa Cruz de la Sierra en `wwwroot/images/`.
- Motor cartográfico interactivo integrado en el workspace.

## Product Principles
1. **Claridad territorial inmediata:** Los datos geoespaciales deben comunicarse con jerarquía visual impecable y narrativa comprensible.
2. **Autoridad técnica y sofisticación visual:** La portada debe evocar los geoportales y plataformas de datos espaciales más avanzadas del mundo (estética premium, tipografía precisa, contrastes equilibrados).
3. **Interacción viva:** Microinteracciones fluidas, tabs interactivas de capas, badges numéricos reales y visualizadores que inviten a explorar.
4. **Respeto a la identidad territorial:** Paleta cruceña de bosque y terracota aplicada con refinamiento y sobriedad.
