# A tu Ritmo

Proyecto Unity con mini-juegos de memoria y ritmo, perfiles de jugador y conexión con Firebase.

## Configuración rápida

Para abrir y correr el proyecto, estos son los pasos importantes.

### 1. Requisitos

- Unity Hub
- Unity `6000.2.15f1`
- Conexión a internet para usar perfiles con Firebase

### 2. Abrir el proyecto

1. Clona o descarga este repositorio.
2. Abre Unity Hub.
3. Usa `Add project from disk`.
4. Selecciona la carpeta del proyecto.
5. Ábrelo con Unity `6000.2.15f1`.

### 3. Esperar la primera importación

La primera vez Unity puede tardar porque debe:

- importar paquetes
- compilar scripts
- cargar URP, TextMeshPro, Input System y Firebase

### 4. Abrir la escena principal

Abrir:

- `Assets/Scenes/MainScene.unity`

### 5. Ejecutar

Presionar `Play` en el editor.

Al iniciar:

- debe aparecer el panel de perfil
- se puede seleccionar o crear un perfil
- luego se entra al menú principal

## Firebase

El proyecto ya usa Firebase para:

- perfiles
- sesiones
- reportes de partidas

Archivos de configuración incluidos en el repo:

- `Assets/google-services.json`
- `Assets/StreamingAssets/google-services-desktop.json`

Si esos archivos siguen presentes y válidos, no debería hacer configuración extra para probar el proyecto.

Si Firebase falla, el juego puede abrir, pero el panel de perfiles mostrará mensajes como:

- `Firebase no esta listo.`

## Escenas del proyecto

Escenas activas en Build Settings:

1. `Assets/Scenes/MainScene.unity`
2. `Assets/Scenes/SimonScene.unity`
3. `Assets/Scenes/TempoTapScene.unity`
4. `Assets/Scenes/BasicrhythmScene.unity`
5. `Assets/Scenes/BandaMusical.unity`

## Mini-juegos

### MainScene

- menú principal
- selector de perfil
- acceso a juegos y configuraciones

### SimonScene

- juego de memoria visual por secuencias

### TempoTapScene

- juego de ritmo con tapping y runner

### BasicrhythmScene

- juego de ritmo con patrones musicales

### BandaMusical

- juego de ritmo con una banda musical

## Cómo hacer build

### Build para probar en PC desde Unity

1. Abre `MainScene`.
2. Ve a `File > Build Profiles` o `File > Build Settings`.
3. Verifica que estén las 5 escenas incluidas.
4. Selecciona la plataforma deseada.
5. Pulsa `Build`.

### Build WebGL

1. En Unity Hub, asegúrate de tener instalado el módulo `WebGL Build Support` para la versión `6000.2.15f1`.
2. En Unity ve a `File > Build Settings`.
3. Selecciona `WebGL`.
4. Pulsa `Switch Platform`.
5. Pulsa `Build`.

### Build Android

1. En Unity Hub, instala `Android Build Support` para `6000.2.15f1`.
2. Asegúrate de incluir SDK/NDK/OpenJDK al instalar el módulo.
3. En Unity ve a `File > Build Settings`.
4. Selecciona `Android`.
5. Pulsa `Switch Platform`.
6. Pulsa `Build`.

Configuración actual relevante de Android:

- `applicationId`: `com.unicauca.aturitmo`
- `minSdkVersion`: `25`
- `bundleVersion`: `1.0`

## Problemas comunes

### No abre el proyecto

Revisar:

- que use Unity `6000.2.15f1`
- que Unity termine de importar todo

### No aparece el perfil o no carga perfiles

Revisar:

- conexión a internet
- que Firebase inicialice bien
- que existan `Assets/google-services.json` y `Assets/StreamingAssets/google-services-desktop.json`

### No deja hacer build

Revisar:

- que el módulo de la plataforma esté instalado en Unity Hub
- que las escenas sigan en Build Settings

## Estructura básica

```text
Assets/
├── Firebase/
├── Prefabs/
├── Scenes/
├── Script/
├── Sprites/
├── StreamingAssets/
└── google-services.json
```

## Subir a GitHub

Para compartir el proyecto Unity, conserva `Assets/` (incluidos los archivos `.meta`),
`Packages/`, `ProjectSettings/`, `.gitignore`, `.gitattributes` y este README.
No subas el APK, `Ejecutable/`, `Library/`, `Temp/`, `Logs/`, `GeneratedAssets/`
ni los proyectos generados por el IDE: ya están excluidos en `.gitignore`.

Los archivos `.bundle` y `.so` de Firebase están configurados para Git LFS en
`.gitattributes`. Instala y configura Git LFS antes del primer commit si vas a
incluirlos. Conserva `Assets/GeneratedLocalRepo/`: contiene dependencias locales
que Firebase puede necesitar al compilar para Android.

Los archivos `google-services.json` identifican el proyecto Firebase de esta
aplicación. Antes de hacer público el repositorio, revisa las reglas de acceso
de Firebase y las restricciones de sus claves en Google Cloud.

## Nota final

Si alguien solo necesita correr el proyecto, lo mínimo es:

1. abrir con Unity `6000.2.15f1`
2. abrir `MainScene`
3. presionar `Play`

Si alguien necesita hacer build, lo más importante es tener instalado el módulo correcto de la plataforma en Unity Hub.
