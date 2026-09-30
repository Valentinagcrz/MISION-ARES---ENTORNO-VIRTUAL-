@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ==============================================
echo   Subiendo MISION ARES a GitHub
echo ==============================================
where git >nul 2>nul
if errorlevel 1 (
  echo.
  echo No tienes Git instalado. Instala "Git for Windows" desde https://git-scm.com/download/win
  echo (deja todas las opciones por defecto^) y vuelve a abrir este archivo.
  echo O usa GitHub Desktop: File ^> Add local repository ^> esta carpeta ^> Push origin.
  pause
  exit /b
)
git config --global --add safe.directory "*"
git push -u origin main
echo.
if errorlevel 1 (echo ALGO FALLO: toma una foto de este mensaje.) else (echo LISTO: todo quedo en GitHub.)
pause
