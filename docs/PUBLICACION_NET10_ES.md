# Publicación del backend .NET 10

## Recuperación y trazabilidad

Se recuperó el paquete Git adjunto sobre la base remota `a9e9205a44d393fff7ae1e106bf809c2350a7bb9`. El acceso de escritura se comprobó creando la rama de trabajo mediante la conexión GitHub autorizada. La publicación se realizó por la API Git de esa conexión, ya que el Git local no dispone de credenciales de push.

| Commit del paquete | Commit publicado | Árbol Git idéntico |
| --- | --- | --- |
| `1ccdcea` | `d2784b37271a23dbffcd51b1a4d5f6599b4a7b93` | `6c89aec65a318ccd9ebe69acd7e7ba046a7fd378` |
| `1029970` | `9162f6694535fb8a4b406f97fde8798673b06c44` | `f814bf02f74e0d81a6b50cb68a1fdcac45587e40` |

Se verificaron los hashes de ambos árboles y se descargó la rama publicada para comparar su contenido con `1029970`: sin diferencias. Los nuevos hashes de commit corresponden a metadatos generados por GitHub, conservando contenido y orden de los cambios. Una actualización documental posterior añade este registro y corrige las referencias de publicación inicial.

## Validación local repetida

- SDK .NET 10.0.401 / runtime 10.0.12, Linux.
- Build Release de `NeonSuit.RSSReader.Backend.slnx`: 0 errores y 0 advertencias.
- Integración: 28 aprobadas, 0 fallos.
- Unitarias: 213 aprobadas, 0 fallos, 4 benchmarks históricos omitidos.
- Total: 241 aprobadas de 245 casos descubiertos.

## Validación e integración en GitHub

PR: https://github.com/NidroySoft/NeonSuit.RSSReader/pull/1

La [ejecución 37333378615](https://github.com/NidroySoft/NeonSuit.RSSReader/actions/runs/37333378615), sobre `9162f6694535fb8a4b406f97fde8798673b06c44`, terminó correctamente en Linux y Windows: restore, build y test aprobados en ambas plataformas. Los TRX se conservan como artefactos. Esta actualización posterior solo modifica documentación; no cambia el código ni los tests validados. El resultado definitivo y el hash de merge quedan registrados en el PR y sus comprobaciones. No interpretar la creación del PR como una validación aprobada. Integrar mediante merge commit tras comprobar las dos plataformas.

## Recuperación

El código anterior se conserva en la rama remota `recovery/backend-before-net10-20261005` y en el commit base. No se modifica el historial ni se utiliza force-push.

Para revertir el merge, crear una rama de recuperación desde `origin/master` y ejecutar `git revert -m 1 <hash-del-merge-del-PR-1>`. Obtener ese hash de la página del PR o con `git log --merges origin/master`. La reversión del código no restaura SQLite: seguir [ROLLBACK_NET10_ES.md](ROLLBACK_NET10_ES.md), conservando la base posterior y restaurando el backup previo con la aplicación detenida.
