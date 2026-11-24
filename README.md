# ANR FG Manager

Gerenciador para Windows do **ANR FG** (Apex Neural Rendering) com OptiScaler:
encontra os jogos instalados (Steam, Epic, GOG, Xbox, EA, Ubisoft e pastas suas),
instala/atualiza/desinstala o ANR FG em cada jogo, edita as configurações,
recomenda ajustes para a sua GPU e abre o jogo pelo botão **Jogar**.

Escrito em C# (.NET Framework 4.x), sem dependências: compila só com o `csc.exe` que já vem no Windows.

## Recursos
- Lista de jogos com ícone, banner (Steam), filtros e ordenação.
- Instalação e desinstalação do ANR FG com backup, detecção de proxy (`dxgi.dll` / `winmm.dll`).
- Editor do `nr_fg.ini` e do `OptiScaler.ini` (inclui a tecla do menu).
- Atualização do pacote ANR FG direto das releases de `apex-compute/ANR`.
- Instalador próprio (`ANR-FG-Manager-Setup.exe`), sem precisar de administrador.

## Como compilar
1. Baixe o pacote **completo** da versão que quer embutir em
   <https://github.com/apex-compute/ANR/releases> (o `anr-fg-V.B??-full.zip`) e coloque na raiz desta pasta.
2. Dê duplo clique em `build.cmd`.
3. Saem `ANR-FG-Manager-Setup.exe` (instalador) e `ANR-FG-Manager.exe` (portátil), também em `dist\`.

O pacote do ANR FG **não** faz parte deste repositório (é do Apex Compute) e por isso o `.gitignore` o ignora.

## Publicar uma versão
1. Aumente a versão em `src/PackageAndInstaller.cs` (`AppVersion`), `src/AssemblyInfo.cs` e `installer/SetupAssemblyInfo.cs`.
2. Rode `build.cmd`.
3. Em **Releases > Draft a new release**, crie a tag da versão (ex.: `v2.3.0`) e anexe
   `ANR-FG-Manager-Setup.exe` e `ANR-FG-Manager.exe` (com esses nomes).
4. Quem usa o programa recebe o aviso de atualização (configure o repositório em *atualizações*,
   ou troque `DefaultRepo` em `src/AppUpdater.cs`).

## Aviso
Projeto não oficial, feito por um usuário. ANR FG e OptiScaler pertencem aos seus autores e licenças.
