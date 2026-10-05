# Publicación y recuperación — backend .NET 10

## Identificación de versiones

- Repositorio: https://github.com/NidroySoft/NeonSuit.RSSReader
- Base previa: `a9e9205a44d393fff7ae1e106bf809c2350a7bb9`.
- Migración y estabilización del paquete: `1ccdcea`; equivalente publicado: `d2784b37271a23dbffcd51b1a4d5f6599b4a7b93`.
- Documentación del paquete: `1029970`; equivalente publicado: `9162f6694535fb8a4b406f97fde8798673b06c44`.
- PR de integración: https://github.com/NidroySoft/NeonSuit.RSSReader/pull/1.
- Punto de recuperación remoto: `recovery/backend-before-net10-20261005`, apuntando al commit base.
- Rama de trabajo: `backend/net10-stabilization`.
- Etiqueta local de recuperación: `backend-before-net10-20261005`, apuntando al commit base.

Los hashes publicados difieren porque la API de GitHub genera nuevos metadatos de commit. Los árboles Git de ambos commits son idénticos a los originales; ver `PUBLICACION_NET10_ES.md`. La etiqueta del paquete se conserva localmente; el punto de recuperación publicado es la rama remota indicada arriba.

Los cambios se publican primero en una rama separada. La etiqueta anterior identifica el código original aunque master avance. No reescribir el historial ni usar force-push para una recuperación. La existencia local de una rama/etiqueta no implica que ya esté publicada; comprobar su presencia en GitHub.

## Antes de utilizar una base existente

1. Cerrar todas las instancias de la aplicación y detener tareas de sincronización.
2. Guardar una copia íntegra de la base original en otra carpeta. Con la aplicación completamente cerrada, conservar también cualquier archivo `.db-wal` y `.db-shm` asociado. No copiar únicamente el `.db` mientras esté abierto en modo WAL.
3. Probar el arranque de .NET 10 sobre una copia, conservando intacto el original.
4. Comprobar categorías, feeds, artículos, etiquetas y preferencias tras el arranque.

La adopción de una base sin historial genera un backup online previo con nombre `<base>.pre-net10-<identificador>.db`. Ese backup incluye datos confirmados en WAL y se produce antes de registrar el esquema inicial. Su ausencia puede significar una base nueva, una base ya versionada o un rechazo de compatibilidad antes de modificarla. Para bases ya versionadas, conservar igualmente la copia manual previa.

## Consultar el código original sin cambiar master

```sh
git fetch origin --tags
git switch -c recovery/backend-before-net10 a9e9205a44d393fff7ae1e106bf809c2350a7bb9
```

Este checkout restaura el código; no revierte automáticamente los archivos SQLite. El backend anterior utiliza .NET 8 y su propio conjunto de dependencias. Compilar únicamente los proyectos necesarios; la solución original incluye el WPF incompleto y otras referencias fuera del alcance de esta entrega.

## Revertir una publicación integrada

Preferir una nueva rama y un pull request de reversión. Si la integración fue un merge commit:

```sh
git switch -c recovery/revert-net10 origin/master
git revert -m 1 <commit-del-merge>
```

Si se integró mediante squash, usar `git revert <commit-del-squash>` sin `-m`. Si se publicaron commits individuales, revertir los commits específicos empezando por el más reciente. Sustituir los marcadores por los hashes reales del historial de GitHub; no ejecutar estos ejemplos literalmente. El rollback debe validarse contra la versión actual de master si existen cambios posteriores.

## Recuperar SQLite

1. Detener por completo la aplicación y verificar que ningún proceso tenga abierta la base.
2. Conservar la base posterior a la migración y sus sidecars en una carpeta de recuperación: puede contener artículos, preferencias o cambios que no existen en el backup anterior.
3. Restaurar el backup previo a la ruta de la base de trabajo. Evitar dejar archivos WAL/SHM de la base posterior junto al archivo restaurado.
4. Arrancar el código anterior sobre la copia restaurada y verificar los datos.

Restaurar un backup pierde los cambios posteriores a ese backup. No borrar la base posterior hasta reconciliar los datos necesarios. No se recomienda ejecutar migraciones Down automáticamente para volver a producción: algunas eliminan columnas/tablas nuevas, y una base adoptada originalmente no tenía historial de migraciones.

## Evidencia y límites

La migración fue comprobada en Linux con SDK 10.0.401 y runtime 10.0.12: 241 casos aprobados, cero fallos y cuatro benchmarks omitidos. Se verificaron dos esquemas sin historial, rechazo de columnas incompatibles, conservación de artículos y lectura de backups. No se ha probado una restauración operativa en el ordenador del usuario ni bases históricas desconocidas. Consultar `BACKEND_NET10_ES.md` para el alcance funcional y los tests históricos conservados fuera de las suites activas.
