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

Ne jamais publier le dossier live du jeu : il peut contenir la configuration
utilisateur et plusieurs gigaoctets de bundles externes. Construire un staging
propre, hors du jeu. Avant la première compilation finale, capturer en lecture
seule l'existence et le hash de la vraie configuration utilisateur :

    cd "C:\Users\cadet\Documents\GitHub\CityTimelineMod"

    $userConfig = Join-Path $env:USERPROFILE "AppData\LocalLow\Colossal Order\Cities Skylines II\ModsSettings\CityTimelineMod\config.json"
    $userConfigExistedBefore = Test-Path -LiteralPath $userConfig -PathType Leaf
    $userConfigHashBefore = if ($userConfigExistedBefore) {
        (Get-FileHash -LiteralPath $userConfig -Algorithm SHA256).Hash
    } else {
        $null
    }

Construire ensuite le staging Release avec le mode propre obligatoire. Cette
commande ne vise jamais le `LocalModsPath` live :

    $repoRoot = (Resolve-Path .).Path
    $stageRoot = Join-Path $repoRoot "src\CityTimelineMod\obj\publish-staging"
    dotnet build .\src\CityTimelineMod\CityTimelineMod.csproj `
        -c Release `
        --no-restore `
        -p:LocalModsPath="$stageRoot" `
        -p:CityTimelineCleanPublish=true

    $content = Join-Path $stageRoot "CityTimelineMod"
    Get-ChildItem $content -Force -Recurse | Select-Object FullName, Length

Le projet vérifie lui-même que le chemin de déploiement résolu est exactement
`src\CityTimelineMod\obj\publish-staging\CityTimelineMod`. Toute tentative de
mode propre avec un autre `LocalModsPath` doit faire échouer le build avant le
déploiement.

Le build propre échoue si le staging contient `data\exports\bundles`,
`data\bundles` ou `ModsSettings`. Verifier aussi que le `config.json` publié est
strictement le défaut versionné, jamais la configuration utilisateur :

    $defaultHash = (Get-FileHash .\resources\defaults\config.json -Algorithm SHA256).Hash
    $stagedHash = (Get-FileHash (Join-Path $content "config.json") -Algorithm SHA256).Hash
    if ($defaultHash -ne $stagedHash) { throw "Published config.json is not the repository default." }

Après toutes les compilations finales, vérifier que la configuration utilisateur
n'a ni été créée, ni supprimée, ni modifiée :

    $userConfigExistedAfter = Test-Path -LiteralPath $userConfig -PathType Leaf
    if ($userConfigExistedAfter -ne $userConfigExistedBefore) {
        throw "The final builds changed the existence of the user config.json."
    }
    if ($userConfigExistedAfter) {
        $userConfigHashAfter = (Get-FileHash -LiteralPath $userConfig -Algorithm SHA256).Hash
        if ($userConfigHashAfter -ne $userConfigHashBefore) {
            throw "The final builds modified the user config.json."
        }
    }

Vérifier également les exclusions de publication :

    $forbidden = @(
        (Join-Path $content "data\exports\bundles"),
        (Join-Path $content "data\bundles"),
        (Join-Path $content "ModsSettings")
    )
    $forbidden | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object {
        throw "Forbidden publication content: $_"
    }

Le dossier doit contenir :

CityTimelineMod.dll
CityTimelineMod_win_x86_64.dll
CityTimelineMod_mac_x86_64.bundle
CityTimelineMod_linux_x86_64.so
0Harmony.dll
CityTimelineMod.mjs
CityTimelineMod.css
CityTimelineMod.mjs.LICENSE.txt
config.json
mod.json
fonts\

Les bundles complets restent des actifs utilisateur externes et ne font jamais
partie du contenu Paradox Mods.

## Gate avant publication

Ne lancer aucune commande ModPublisher tant que le contenu exact de `$content`
n'a pas été copié volontairement vers un emplacement de test, chargé dans le
jeu et validé avec la checklist runtime. Un build réussi et une inspection du
staging ne remplacent pas ce playtest du paquet réellement destiné à la
publication.

## Publier une nouvelle version

Depuis la racine du repo :

    cd "C:\Users\cadet\Documents\GitHub\CityTimelineMod"

    $content = Join-Path (Resolve-Path .).Path "src\CityTimelineMod\obj\publish-staging\CityTimelineMod"
    & $env:CSII_MODPUBLISHERPATH NewVersion .\src\CityTimelineMod\Properties\PublishConfiguration.xml -c $content -v

## Mettre a jour uniquement les metadonnees

Depuis la racine du repo :

    cd "C:\Users\cadet\Documents\GitHub\CityTimelineMod"

    $content = Join-Path (Resolve-Path .).Path "src\CityTimelineMod\obj\publish-staging\CityTimelineMod"
    & $env:CSII_MODPUBLISHERPATH Update .\src\CityTimelineMod\Properties\PublishConfiguration.xml -c $content -v

## Ne pas utiliser sauf nouvelle creation volontaire

    $content = Join-Path (Resolve-Path .).Path "src\CityTimelineMod\obj\publish-staging\CityTimelineMod"
    & $env:CSII_MODPUBLISHERPATH Publish .\src\CityTimelineMod\Properties\PublishConfiguration.xml -c $content -v

Cette commande recree un nouveau mod Paradox Mods.

