# Backend NeonSuit RSSReader — .NET 10

Revisión del 5 de octubre de 2026 sobre `NidroySoft/NeonSuit.RSSReader`, base `a9e9205`.

## Dictamen

El backend actualizado permite empezar el nuevo cliente WPF: las bibliotecas compilan en .NET 10 y sus contratos principales están cubiertos por pruebas con SQLite real. Esta entrega es una base de desarrollo validada, no una certificación completa de producción. La aplicación WPF existente queda fuera de esta migración.

El backend es un conjunto de bibliotecas locales, no un servidor HTTP. La solución `NeonSuit.RSSReader.Backend.slnx` agrupa Core, Data, Services, Setup y las dos suites activas. El futuro WPF deberá usar `net10.0-windows` y referenciar Setup/Core.

## Cambios

- .NET 10, EF Core SQLite 10.0.12, AutoMapper 16.2.0 y AngleSharp 1.8.3; retirada de dependencias SQLite redundantes.
- Corrección de perfiles AutoMapper duplicados/incompletos y perfiles de mantenimiento. Las estadísticas cuentan categorías y la comprobación de integridad consulta tablas, índices y claves foráneas reales.
- Inicialización con cuatro migraciones: esquema original, resaltado de artículos, tablas de sincronización y clave autogenerada de ArticleTags. El par artículo/etiqueta sigue siendo único.
- Adopción de esquemas anteriores sin historial: comprobación de tablas/columnas, backup online previo y aplicación de las migraciones pendientes. Los esquemas incompatibles se rechazan; las bases SQLite nuevas se crean correctamente. Se verifican bases de esquema inicial y bases con el esquema actual pero sin historial; esto no garantiza compatibilidad con cualquier archivo histórico desconocido.
- Escrituras de estado de artículos corregidas bajo el NoTracking utilizado en producción; resolución de conflictos de seguimiento al actualizar y eliminar entidades en un mismo scope.
- Corrección de contadores de feeds que intentaban actualizar una propiedad no persistida.
- Coordinador singleton con scopes independientes y un trabajador: ejecuta feeds, reglas, retención, backups, estadísticas y caché reales. Se eliminan los resultados aleatorios. Los fallos de actualización no producen un éxito ficticio.
- Programación inicial según intervalos: el arranque no lanza toda la retención y backups inmediatamente. FullSync actualiza feeds, procesa reglas y calcula estadísticas; las tareas de retención y backup conservan sus horarios independientes.
- Reglas de etiquetas y resaltado efectivas, métricas de etiquetas basadas en relaciones realmente creadas y cancelación propagada.
- Eventos globales `IBackendEvents` para notificaciones y acciones de sonido desde scopes de segundo plano. PlaySound falla explícitamente sin un consumidor registrado; el cliente WPF implementará la reproducción.
- Prevención de ciclos al cambiar el padre de una categoría y cálculo de rutas/profundidad en las consultas.
- Backup con la API online de SQLite, incluyendo datos confirmados en WAL. El coordinador se detiene durante la liberación asíncrona del contenedor.
- Workflow .NET 10 con matriz Linux/Windows. No se ha ejecutado GitHub Actions ni se han publicado cambios en GitHub durante esta revisión.

## Compilar y comprobar

Instalar un SDK .NET 10 estable. `global.json` permite la banda de características más reciente de la familia 10.0.

```sh
dotnet restore NeonSuit.RSSReader.Backend.slnx -p:RestoreDisableParallel=true
dotnet build NeonSuit.RSSReader.Backend.slnx --no-restore -c Release -m:1
dotnet test NeonSuit.RSSReader.Backend.slnx --no-build -c Release -m:1 --logger trx
```

Se usó SDK 10.0.401/runtime 10.0.12 en Linux. Los archivos TRX y el log final de esta entrega se adjuntan en `verification/` dentro del ZIP. Las pruebas usan archivos SQLite temporales, DI de producción con validación de scopes y mapeos, HTTP local para el parser real y un parser controlado para escenarios de orquestación.

**Resultado final Release: 241 pruebas aprobadas (213 unitarias y 28 de integración), cero fallos y cuatro benchmarks omitidos; 245 casos descubiertos. Compilación sin advertencias en el log final.**

La consulta NuGet de vulnerabilidades, incluyendo dependencias transitivas de los seis proyectos, no encontró paquetes vulnerables en las fuentes consultadas al realizar esta revisión. No equivale a una auditoría de seguridad del código.

La suite verifica: categorías y jerarquía; estados, búsqueda y paginación de artículos; etiquetas y duplicados; cinco acciones persistidas de reglas; sonidos con/sin consumidor; eventos y supresión de notificaciones duplicadas; preferencias y exportación/importación; actualización y deduplicación de feeds; fallo HTTP; RSS y Atom reales; OPML; backup legible; estadísticas; arranque, parada y reinicio del coordinador; historial; cancelación; adopción e incompatibilidad de esquemas.

## Pruebas históricas

33 archivos incompatibles con los contratos actuales se conservan sin modificaciones en `tests/LegacyContracts`. Están fuera de los proyectos ejecutables; no se cuentan como aprobados ni se afirma cobertura equivalente de todos sus casos. Se han adaptado cinco suites de repositorios existentes y añadido pruebas de integración de los contratos vigentes. Cuatro benchmarks antiguos permanecen omitidos. La suite funcional no sustituye mediciones de rendimiento ni pruebas prolongadas.

## Uso desde el futuro WPF

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Setup;

var services = new ServiceCollection();
services.AddNeonSuitBackend(databasePath);
await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true
});
await provider.UseNeonSuitDatabaseAsync();

var events = provider.GetRequiredService<IBackendEvents>();
// Suscribir NotificationCreated y RuleActionRequested antes de arrancar sync.
// En WPF, enviar las actualizaciones visuales al Dispatcher.
var sync = provider.GetRequiredService<ISyncCoordinatorService>();
await sync.StartAsync();

await using (var scope = provider.CreateAsyncScope())
{
    var articleService = scope.ServiceProvider.GetRequiredService<IArticleService>();
    var unreadCount = await articleService.GetUnreadCountAsync();
}

await sync.StopAsync();
```

No resolver servicios scoped desde el proveedor raíz ni compartirlos simultáneamente entre operaciones. Mantener el proveedor durante la vida de la aplicación y liberarlo asíncronamente al cerrar. Configurar Serilog en el host si se desea salida de logs.

## Límites de la validación

- Falta ejecutar las pruebas en Windows y conectar Dispatcher, reproducción de sonido, presentación de notificaciones y WebView2 al crear WPF.
- La notificación emitida es una solicitud a la presentación; el backend no demuestra que Windows haya mostrado un toast.
- No se han medido sesiones prolongadas, carga masiva ni variedad de feeds externos. El parser real se comprueba con fixtures RSS/Atom por HTTP local.
- Persisten propuestas de mejoras futuras en comentarios del repositorio. La entrega corrige los bloqueos funcionales detectados; no implementa todas las funcionalidades sugeridas allí.
- El repositorio ya utilizaba AutoMapper 16. Los requisitos de licencia vigentes desde la versión 15 también aplican a la dependencia actualizada; consultar la [documentación oficial](https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html) para la distribución del producto.
