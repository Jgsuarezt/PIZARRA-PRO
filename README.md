# Pizarra Pro

Pizarra digital de escritorio para **Windows**, escrita en **.NET 8 / WPF**.

Es un proyecto independiente **inspirado** en la idea de apps de pizarra/notas
como [Starnote](https://starnote.ai/) (lienzo infinito, tinta digital,
anotación de PDF). No reutiliza su código, marca ni diseño — es una
implementación propia desde cero.

## Funciones de esta primera versión (MVP)

- **Lienzo infinito** con paneo (mano / botón central) y zoom (rueda del
  ratón, centrado en el cursor).
- **Dibujo a mano** con lápiz, resaltador y borrador (tinta de Windows,
  compatible con lápiz óptico / stylus).
- **Formas**: rectángulo, elipse, línea y flecha.
- **Texto**: clic para escribir, arrastrar para mover, doble clic para
  editar, `Supr` para eliminar.
- **Capas** por página: visibilidad, bloqueo, opacidad, reordenar, añadir y
  eliminar.
- **Páginas múltiples**: página en blanco o importadas desde un PDF (cada
  página del PDF se convierte en el fondo de una página anotable).
- **Deshacer / rehacer** (`Ctrl+Z` / `Ctrl+Y`) para trazos y texto.
- **Guardar / abrir** en un formato propio `.pzp` (JSON autocontenido con los
  trazos en formato ISF y las imágenes de fondo incrustadas).
- **Exportar** la página actual como PNG, o el documento completo como PDF.

## Cómo compilar y ejecutar (en Windows)

Requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
(o Visual Studio 2022 17.8+ con la carga de trabajo ".NET desktop
development").

```powershell
git clone <url-del-repo>
cd PIZARRA-PRO
dotnet restore
dotnet run --project src/PizarraPro/PizarraPro.csproj
```

O simplemente abre `PizarraPro.sln` con Visual Studio y pulsa F5.

Para generar un ejecutable independiente para distribuir:

```powershell
dotnet publish src/PizarraPro/PizarraPro.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Nota sobre esta entrega

Este proyecto fue generado y revisado en un entorno Linux (sandbox de
desarrollo), donde **WPF no puede compilarse ni ejecutarse** porque
depende del SDK de escritorio de Windows. El código fue escrito y
revisado cuidadosamente contra la documentación oficial de las APIs usadas
(WPF InkCanvas/Ink, Docnet.Core, PdfSharpCore), pero **no ha podido
compilarse ni probarse en un equipo Windows real todavía**. Si al abrirlo
en Visual Studio aparece algún error de compilación puntual, probablemente
sea una firma de método que cambió entre versiones de una librería — son
fáciles de corregir con el propio Visual Studio (IntelliSense) y, si
quieres, puedo ayudarte a solucionarlos en la siguiente iteración.

## Arquitectura

```
src/PizarraPro/
  Models/        DTOs para guardar/abrir documentos (.pzp) y el enum de herramientas
  ViewModels/    Estado de la app (MVVM ligero): documento, páginas, capas, texto
  Services/      Deshacer/rehacer, guardado/carga, importar PDF, exportar PNG/PDF
  Converters/    Conversores de binding para XAML
  MainWindow.*   Ventana principal: menús, barra de herramientas, lienzo, capas
```

## Ideas para siguientes iteraciones

- Miniaturas reales de página en el panel de páginas.
- Selección múltiple / redimensionado de formas ya dibujadas.
- Reconocimiento de formas a mano alzada ("shape snapping").
- Sincronización en la nube y sincronización entre dispositivos.
- Soporte para más formatos de documento (DOCX, PPTX, EPUB).
