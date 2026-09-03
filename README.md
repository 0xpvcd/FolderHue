# FolderHue

Colorise les dossiers de l'Explorateur de fichiers Windows depuis le menu contextuel natif.
Clic droit sur un ou plusieurs dossiers → « FolderHue ».

Fonctionne sur **Windows 10 1809 et ultérieur** (menu classique) et sur **Windows 11**
(menu direct), avec la même DLL et le même paquet.

`CLAUDE.md` est le document de référence du projet : décisions d'architecture, règles de
sécurité, pièges connus. Lisez-le avant toute modification.

## Démarrage rapide

```powershell
.\scripts\setup-prereqs.ps1   # SDK .NET 8 + toolchain C++ (NativeAOT) — une seule fois
.\scripts\make-devcert.ps1    # certificat de signature — une seule fois

# puis, depuis une console élevée :
#   Import-PfxCertificate -FilePath .\artifacts\FolderHue-dev.pfx `
#       -CertStoreLocation Cert:\LocalMachine\TrustedPeople

.\scripts\build.ps1 -FullPackage -CertificateThumbprint <empreinte>
.\scripts\install-dev.ps1
```

**La signature n'est pas optionnelle, même en développement.** Un enregistrement sparse en mode
développeur s'effectue sans erreur et le serveur COM répond — mais l'Explorateur n'affiche
jamais l'entrée de menu. Seul un MSIX signé et réellement installé fonctionne.

Pour désinstaller :

```powershell
.\scripts\uninstall-dev.ps1 -ResetFolders
```

## Ce qui se passe sous le capot

La colorisation n'installe aucun processus résident et ne consomme aucun slot d'overlay d'icône.
Pour chaque dossier choisi :

1. une icône `.ico` multi-résolution, pré-générée dans `%LOCALAPPDATA%\FolderHue\icons`,
   est référencée depuis un `desktop.ini` fusionné avec l'existant ;
2. `desktop.ini` reçoit les attributs *caché + système*, et le dossier l'attribut *lecture seule* —
   sans ce dernier, l'Explorateur ignore purement et simplement le fichier ;
3. `SHChangeNotify` rafraîchit l'affichage immédiatement.

Un journal, `%LOCALAPPDATA%\FolderHue\applied.json`, retient ce que nous avons modifié.
C'est lui qui permet une réinitialisation réellement propre : l'attribut *lecture seule* n'est
retiré que si c'est nous qui l'avions posé, et un `desktop.ini` préexistant est restauré plutôt
qu'effacé.

Les icônes sont dérivées de l'icône de dossier **de votre machine**, extraite au moment de
l'installation puis teintée en espace HSL. Le rendu suit donc l'aspect natif de votre version de
Windows, et aucun asset Microsoft n'est redistribué.

## Structure

| Projet | Rôle |
|---|---|
| `src/FolderHue.Core` | Logique métier : teinte HSL, écriture `.ico`, `desktop.ini`, chemins protégés, journal. Zéro dépendance graphique ou shell. |
| `src/FolderHue.Shell` | DLL COM in-process compilée en **NativeAOT**, chargée dans `explorer.exe`. Expose deux classes : `IExplorerCommand` pour le menu direct de Windows 11, `IContextMenu` pour le menu classique. |
| `src/FolderHue.App` | Interface de réglages WinForms et moteur de rendu GDI+. Seul projet à dépendre de `System.Drawing`. |
| `src/FolderHue.Package` | Manifeste MSIX. |
| `tests/FolderHue.Core.Tests` | xUnit sur tout `Core`. |

## Limites assumées

- L'icône est référencée par un **chemin absolu** dans votre profil : un dossier déplacé sur une
  autre machine, un partage réseau ou une clé USB retrouvera son icône d'origine.
- La colorisation ajoute un `desktop.ini` masqué dans le dossier. Git et OneDrive peuvent le
  signaler : c'est normal.
- Les dossiers système, les dossiers Windows de référence (Documents, Images, Bureau…), les
  racines de disque et les liens symboliques sont refusés, délibérément.

## Publication

`.\scripts\build.ps1 -FullPackage` produit `artifacts\FolderHue.msix`, non signé.
La voie retenue pour la distribution publique est le **Microsoft Store**, qui signe le paquet
lui-même. Hors Store, il faut un certificat délivré par une autorité reconnue ; un certificat
auto-signé (`scripts\make-devcert.ps1`) ne vaut que pour votre propre machine.
