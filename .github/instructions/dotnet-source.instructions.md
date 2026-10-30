---
name: "Código .NET do PixelPointer"
description: "Use ao criar ou alterar C#, .NET 10, projetos, sobreposição SDL3/Skia, captura de tela, atalhos globais, IPC, portais XDG ou adaptadores de plataforma do PixelPointer."
applyTo: "src/**, Directory.Build.props, Directory.Packages.props, global.json, *.slnx"
---

# Código .NET

- .NET 10, C# mais recente, `Nullable` e `TreatWarningsAsErrors`; todo projeto de produção é `IsAotCompatible` e o executável publica com `PublishAot`.
- Sem reflexão dinâmica, `System.Text.Json` sem source generator ou bibliotecas que gerem avisos de trimming/AOT; P/Invoke com `LibraryImport` e structs explícitas.
- `Main` é síncrono e o SDL roda **somente na thread principal** (D8 do [plano](../../docs/plano-implementacao.md)). Trabalho assíncrono (D-Bus, IPC) roda em outras threads e chega à thread principal por fila; nunca rode um laço de janela na continuação de um `await`.
- `MessageWriter` do Tmds.DBus é `ref struct` mutável: passe sempre por `ref`.
- `SDL_HINT_NO_SIGNAL_HANDLERS=1`; SIGINT/SIGTERM são tratados com `PosixSignalRegistration` (D10).
- Dependências de plataforma passam por interfaces (`IScreenCapture`, `IHotkeyService`); implementações ficam em pastas/projetos por SO e são escolhidas em tempo de execução por `OperatingSystem.Is*()`.
- No Wayland, captura e atalhos usam apenas portais XDG com o app id `io.github.lzocateli.PixelPointer`; diálogos de permissão exigem janela própria focada (D9).
- Bibliotecas nativas (SDL3, libSkiaSharp) ficam ao lado do executável e são resolvidas por `NativeLibraries` mesmo quando o comando é um link simbólico.
- Mensagens ao usuário em pt-BR; comandos, opções e identificadores em inglês.
- Arquivos temporários criados por portais ou capturas são removidos depois de lidos.
