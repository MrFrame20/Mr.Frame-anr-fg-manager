@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Compilar ANR FG Manager (programa + instalador)
cd /d "%~dp0"

set "FWDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "CSC=%FWDIR%\csc.exe"
if not exist "%CSC%" (
    set "FWDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
    set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
if not exist "%CSC%" (
    echo [ERRO] Compilador C# ^(.NET Framework 4^) nao encontrado em %WINDIR%\Microsoft.NET.
    pause
    exit /b 1
)

set "COMPDLL=%FWDIR%\System.IO.Compression.dll"
if not exist "%COMPDLL%" (
    echo [ERRO] System.IO.Compression.dll nao encontrada ^(necessario .NET Framework 4.5 ou superior^).
    pause
    exit /b 1
)

set "PKGZIP="
set "PKGCOUNT=0"
for %%F in (anr-fg-*-full.zip) do (
    set "PKGZIP=%%~nxF"
    set /a PKGCOUNT+=1
)
if "%PKGZIP%"=="" (
    echo [ERRO] Nenhum pacote anr-fg-*-full.zip encontrado nesta pasta.
    echo        Coloque aqui o pacote completo do ANR FG ^(ex.: anr-fg-V.B20-full.zip^).
    echo        Atencao: o "addon" e o "source" NAO servem, precisa ser o "-full.zip".
    pause
    exit /b 1
)
if %PKGCOUNT% GTR 1 (
    echo [AVISO] Ha mais de um anr-fg-*-full.zip nesta pasta. Usando: %PKGZIP%
    echo         Deixe so o pacote que voce quer embutir.
    echo.
)
>"pkgname.txt" echo %PKGZIP%

echo [1/2] Compilando ANR-FG-Manager.exe ^(o programa^)...
echo       Pacote embutido: %PKGZIP%
echo.

if exist "ANR-FG-Manager.exe" del /f /q "ANR-FG-Manager.exe"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
    "/out:ANR-FG-Manager.exe" ^
    "/win32icon:anr-fg.ico" ^
    "/reference:System.dll" ^
    "/reference:System.Core.dll" ^
    "/reference:System.Drawing.dll" ^
    "/reference:System.Windows.Forms.dll" ^
    "/reference:System.Management.dll" ^
    "/reference:%COMPDLL%" ^
    "/resource:%PKGZIP%,package.zip" ^
    "/resource:pkgname.txt,package.name" ^
    "/resource:anr-fg.ico,app.ico" ^
    "src\AssemblyInfo.cs" ^
    "src\JsonHelper.cs" ^
    "src\IniHelper.cs" ^
    "src\InstallShared.cs" ^
    "src\AppUpdater.cs" ^
    "src\PackageAndInstaller.cs" ^
    "src\Scanner.cs" ^
    "src\UiControls.cs" ^
    "src\MainForm.cs" ^
    "src\Program.cs"

if errorlevel 1 (
    echo.
    echo [ERRO] Falha na compilacao do programa.
    pause
    exit /b 1
)

echo.
echo [2/2] Compilando ANR-FG-Manager-Setup.exe ^(o instalador, com o programa dentro^)...
echo.

if exist "ANR-FG-Manager-Setup.exe" del /f /q "ANR-FG-Manager-Setup.exe"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
    "/out:ANR-FG-Manager-Setup.exe" ^
    "/win32icon:anr-fg.ico" ^
    "/reference:System.dll" ^
    "/reference:System.Core.dll" ^
    "/reference:System.Drawing.dll" ^
    "/reference:System.Windows.Forms.dll" ^
    "/resource:ANR-FG-Manager.exe,payload.exe" ^
    "installer\SetupAssemblyInfo.cs" ^
    "src\InstallShared.cs" ^
    "src\UiControls.cs" ^
    "installer\SetupMain.cs"

if errorlevel 1 (
    echo.
    echo [ERRO] Falha na compilacao do instalador.
    pause
    exit /b 1
)

if exist "pkgname.txt" del /f /q "pkgname.txt"
if not exist "dist" mkdir "dist"
copy /y "ANR-FG-Manager.exe" "dist\ANR-FG-Manager.exe" >nul
copy /y "ANR-FG-Manager-Setup.exe" "dist\ANR-FG-Manager-Setup.exe" >nul

echo.
echo [OK] Pronto! Arquivos gerados nesta pasta e em "dist":
echo      ANR-FG-Manager-Setup.exe  ^(instalador: e o unico arquivo que voce precisa distribuir^)
echo      ANR-FG-Manager.exe        ^(versao portatil, sem instalar^)
echo.
echo Para publicar uma atualizacao no GitHub: crie uma Release com a tag da versao
echo ^(ex.: v2.2.1^) e anexe esses dois arquivos. Veja o LEIA-ME.txt.
echo.
pause
