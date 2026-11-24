# ANR FG Manager

Gerenciador para Windows do **ANR FG** (Apex Neural Rendering) com OptiScaler:
encontra os jogos instalados (Steam, Epic, GOG, Xbox, EA, Ubisoft e pastas personalizadas),
instala, atualiza e desinstala o ANR FG em cada jogo, edita as configurações,
recomenda ajustes para a sua GPU e abre o jogo pelo botão **Jogar**.

Executável 100% em C# (.NET Framework 4.x), sem PowerShell e sem dependências externas: compila apenas com o `csc.exe` que já vem no Windows.

## Recursos
- Lista de jogos com ícone, banner (Steam), filtros e ordenação.
- Instalação e desinstalação do ANR FG com backup dos arquivos originais e detecção automática de proxy (`dxgi.dll` / `winmm.dll`).
- Editor integrado do `nr_fg.ini` e do `OptiScaler.ini` (inclui a tecla de atalho do menu).
- Recomendação de ajustes para a sua GPU.
- Atualização do pacote ANR FG direto das releases de `apex-compute/ANR`.
- Instalador próprio (`ANR-FG-Manager-Setup.exe`), sem precisar de administrador.

## Instalação
Disponível em duas versões na aba **Releases**: `ANR-FG-Manager-Setup.exe` (instalador) e `ANR-FG-Manager.exe` (versão portátil, sem instalar).

- Instala por usuário (não pede administrador) em `%LOCALAPPDATA%\Programs\ANR FG Manager`.
- Cria atalhos no Menu Iniciar e na Área de Trabalho e registra o programa em **Aplicativos e recursos**, por onde pode ser desinstalado (com opção de apagar também configurações e cache).
- Rodar o instalador novamente atualiza a instalação existente. Executar `ANR-FG-Manager-Setup.exe /update` faz a atualização silenciosamente.

## Atualizações
Acessíveis pelo link `vX.Y.Z · atualizações` no canto do cabeçalho:
1. **Pacote ANR FG:** consulta automaticamente as releases oficiais em `apex-compute/ANR`. Quando sai uma versão nova, o link avisa e **Baixar e importar** baixa o `anr-fg-...-full.zip`, confere o SHA-256, importa o pacote e oferece reinstalar o ANR FG em todos os jogos que já o usam. **Voltar ao pacote embutido** desfaz a importação.
2. **Programa:** verifica novas versões do ANR FG Manager diretamente pelas Releases do GitHub.
3. **Importar manualmente:** **Escolher arquivo .zip** permite importar um pacote do ANR FG já baixado no PC.

## Como compilar
1. Baixe o pacote **completo** da versão que deseja embutir em
   <https://github.com/apex-compute/ANR/releases> (`anr-fg-V.B??-full.zip`; não serve o `addon` nem o `source`) e coloque na raiz do projeto.
2. Dê duplo clique em `build.cmd`.
3. Serão gerados `ANR-FG-Manager-Setup.exe` (instalador) e `ANR-FG-Manager.exe` (portátil), também copiados para `dist\`.

O pacote do ANR FG **não** faz parte deste repositório (pertence ao Apex Compute) e é ignorado pelo `.gitignore`.

## Aviso
Projeto não oficial, feito de forma independente. ANR FG e OptiScaler pertencem aos seus respectivos autores e licenças.
