<#
.SYNOPSIS
    Cree un certificat auto-signe pour tester l'installation d'un MSIX complet en local.

.DESCRIPTION
    Le paquet sparse de developpement n'a pas besoin de signature : Add-AppxPackage -Register
    suffit en mode developpeur. Ce script ne sert donc qu'a valider le chemin du MSIX complet
    avant publication.

    Un certificat auto-signe ne convient QUE pour votre propre machine (CLAUDE.md §4.5). Pour de
    vrais utilisateurs, il faut soit passer par le Microsoft Store — qui signe le paquet
    lui-meme, c'est la voie retenue — soit un certificat delivre par une autorite reconnue.

    Le sujet du certificat doit correspondre au caractere pres au Publisher declare dans
    AppxManifest.xml, sinon Add-AppxPackage refuse le paquet.

.PARAMETER OutputPath
    Chemin du fichier .pfx a produire. Par defaut artifacts\FolderHue-dev.pfx.

.PARAMETER Password
    Mot de passe protegeant la cle privee. Demande interactivement s'il est omis.
#>
[CmdletBinding()]
param(
    [string] $OutputPath,
    [System.Security.SecureString] $Password
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repository 'artifacts'

function Write-Step { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string] $Message) Write-Host "    OK   $Message" -ForegroundColor Green }

if (-not $OutputPath) {
    $OutputPath = Join-Path $artifacts 'FolderHue-dev.pfx'
}

# Le sujet vient du manifeste : c'est la seule facon de garantir la correspondance exacte.
[xml] $appx = Get-Content (Join-Path $repository 'src\FolderHue.Package\AppxManifest.xml') -Raw
$subject = $appx.Package.Identity.Publisher

Write-Step "Certificat pour le sujet « $subject »"

$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $subject `
    -KeyUsage DigitalSignature `
    -FriendlyName 'FolderHue (developpement)' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

Write-Ok "empreinte : $($certificate.Thumbprint)"

if (-not $Password) {
    $Password = Read-Host -AsSecureString -Prompt 'Mot de passe de la cle privee'
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
Export-PfxCertificate -Cert $certificate -FilePath $OutputPath -Password $Password | Out-Null
Write-Ok "exporte vers $OutputPath"

Write-Host ''
Write-Host 'Etapes suivantes :'
Write-Host "  1. Installez le certificat dans « Personnes de confiance » de la machine locale :"
Write-Host "     Import-PfxCertificate -FilePath '$OutputPath' -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
Write-Host "     (cette commande demande une elevation)"
Write-Host "  2. Signez le paquet :"
Write-Host "     .\scripts\build.ps1 -FullPackage -CertificateThumbprint $($certificate.Thumbprint)"
Write-Host ''
Write-Host 'Ne versionnez jamais le .pfx : .gitignore l''exclut deja.' -ForegroundColor Yellow
