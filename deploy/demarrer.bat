@echo off
REM Lance l'outil de bourse aux jouets depuis son propre dossier.
cd /d "%~dp0"
echo Demarrage de l'outil de bourse aux jouets...
echo Attendez la ligne "Application started", puis ouvrez http://localhost:5050
echo Pour arreter : Ctrl+C
echo.
AssociationSaleTool.Web.exe
pause
