# Publication Paradox Mods - CityTimelineMod

## Mod a conserver

CityTimelineMod

ModId = 145499

Le mod 145488 est un doublon ancien.
Ne garder que 145499.

## Regle importante

Ne plus utiliser Publish pour ce mod.

Publish = cree un nouveau mod
NewVersion = publie une nouvelle version du mod existant
Update = met a jour les metadonnees du mod existant

## Verifier le ModId

Fichier :

src\CityTimelineMod\Properties\PublishConfiguration.xml

La ligne doit etre :

<ModId Value="145499" />

Commande :

    Select-String -Path .\src\CityTimelineMod\Properties\PublishConfiguration.xml -Pattern "ModId|ModVersion|DisplayName"

## Preparer le contenu runtime

Depuis la racine du repo :

    cd "C:\Users\cadet\Documents\GitHub\CityTimelineMod"

    dotnet build .\src\CityTimelineMod\CityTimelineMod.csproj -c Release
    .\scripts\deploy-local.ps1

Verifier le dossier runtime :

    $content = "$env:LOCALAPPDATA\..\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod"

    Get-ChildItem $content -Force | Select-Object Name, Length, LastWriteTime
    tree $content /f

Le dossier doit contenir :

CityTimelineMod.dll
CityTimelineMod_win_x86_64.dll
CityTimelineMod_mac_x86_64.bundle
CityTimelineMod_linux_x86_64.so
config.json
mod.json
data\

## Publier une nouvelle version

Depuis la racine du repo :

    cd "C:\Users\cadet\Documents\GitHub\CityTimelineMod"

    $content = "$env:LOCALAPPDATA\..\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod"

    & $env:CSII_MODPUBLISHERPATH NewVersion .\src\CityTimelineMod\Properties\PublishConfiguration.xml -c $content -v

## Mettre a jour uniquement les metadonnees

Depuis la racine du repo :

    cd "C:\Users\cadet\Documents\GitHub\CityTimelineMod"

    $content = "$env:LOCALAPPDATA\..\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod"

    & $env:CSII_MODPUBLISHERPATH Update .\src\CityTimelineMod\Properties\PublishConfiguration.xml -c $content -v

## Ne pas utiliser sauf nouvelle creation volontaire

    $content = "$env:LOCALAPPDATA\..\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod"

    & $env:CSII_MODPUBLISHERPATH Publish .\src\CityTimelineMod\Properties\PublishConfiguration.xml -c $content -v

Cette commande recree un nouveau mod Paradox Mods.
