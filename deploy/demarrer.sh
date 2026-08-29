#!/bin/sh
# Lance l'outil de bourse aux jouets depuis son propre dossier.
cd "$(dirname "$0")" || exit 1
echo "Demarrage de l'outil de bourse aux jouets..."
echo "Attendez la ligne \"Application started\", puis ouvrez http://localhost:5050"
echo "Pour arreter : Ctrl+C"
echo
exec ./AssociationSaleTool.Web
