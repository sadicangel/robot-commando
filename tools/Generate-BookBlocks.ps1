param(
    [string]$OcrDirectory = (Join-Path $PSScriptRoot '..\.ocr-pages'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\RobotCommando\BookData\Blocks'),
    [string]$GameBookDirectory = (Join-Path $PSScriptRoot '..\RobotCommando\GameBook'),
    [string]$OldPagesDirectory = 'D:\Development\robot-commando-old\RobotCommando.Shared.Tests\Json\Pages',
    [string]$OldItemsPath = 'D:\Development\robot-commando-old\RobotCommando.Shared.Tests\Json\items.json',
    [string]$OldRobotsPath = 'D:\Development\robot-commando-old\RobotCommando.Shared.Tests\Json\robots.json',
    [string]$OldMonstersPath = 'D:\Development\robot-commando-old\RobotCommando.Shared.Tests\Json\monsters.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$gameBookFiles = Get-ChildItem -Path $GameBookDirectory -Filter '*.cs' | Sort-Object FullName | Select-Object -ExpandProperty FullName
Add-Type -Path $gameBookFiles

$conditionType = [RobotCommando.GameBook.ConditionExpression[RobotCommando.GameBook.GameState]]
$effectType = [RobotCommando.GameBook.EffectExpression[RobotCommando.GameBook.GameState]]

$skipOcrSides = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
@(
    'i.014.left.json',
    'i.014.right.json',
    'i.015.left.json',
    'i.015.right.json'
) | ForEach-Object { $null = $skipOcrSides.Add($_) }

$manualAssignments = @{
    'i.031.right.json' = @(
        @{ Mode = 'Block'; Id = 65 },
        @{ Mode = 'Block'; Id = 66 },
        @{ Mode = 'Block'; Id = 66 }
    )
    'i.032.left.json' = @(
        @{ Mode = 'Block'; Id = 66 },
        @{ Mode = 'Block'; Id = 67 },
        @{ Mode = 'Block'; Id = 68 }
    )
    'i.020.left.json' = @(
        @{ Mode = 'Ignore' },
        @{ Mode = 'Block'; Id = 15 }
    )
}

$parsedBlocks = @{}

function Ensure-ParsedBlock([int]$id) {
    if (-not $parsedBlocks.ContainsKey($id)) {
        $parsedBlocks[$id] = [ordered]@{
            Id = $id
            TextParts = [System.Collections.Generic.List[string]]::new()
            Lines = [System.Collections.Generic.List[object]]::new()
        }
    }

    return $parsedBlocks[$id]
}

function Add-GroupToParsedBlock([int]$id, [object[]]$group) {
    $parsedBlock = Ensure-ParsedBlock $id
    $text = Get-GroupText $group

    if (-not [string]::IsNullOrWhiteSpace($text)) {
        $parsedBlock.TextParts.Add($text)
    }

    foreach ($line in $group) {
        $parsedBlock.Lines.Add($line)
    }
}

function Add-TextToParsedBlock([int]$id, [string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return
    }

    $parsedBlock = Ensure-ParsedBlock $id
    $parsedBlock.TextParts.Add((Normalize-Text $text))
}

function Normalize-NumberishText([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ''
    }

    $value = $text.Trim()
    $value = $value -replace '[—–]', '-'
    $value = $value -replace '\$', '5'
    $value = $value.Replace('Ito', '110')
    $value = $value.Replace('lil', '111')
    $value = $value -replace '(?<=\d)\.(?=\d)', ''
    $value = $value -replace '(?<=\d)[oO](?=\d)|(?<=\D)[oO](?=\d)|(?<=\d)[oO](?=\D)', '0'
    $value = $value -replace '(?<=\d)[Il\|](?=\d)|(?<=\D)[Il\|](?=\d)|(?<=\d)[Il\|](?=\D)', '1'
    $value = $value -replace '\s+', ' '

    return $value.Trim()
}

function Normalize-Text([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ''
    }

    $value = $text
    $value = $value -replace "(`r`n|`n|`r)", ' '
    $value = $value -replace '([A-Za-z])[-•]\s+([A-Za-z])', '$1$2'
    $value = $value -replace '•', ''
    $value = $value -replace '\s+', ' '
    $value = $value -replace '\bTum\b', 'Turn'
    $value = $value -replace '\btum\b', 'turn'
    $value = $value -replace '\bRetum\b', 'Return'
    $value = $value -replace '\bretum\b', 'return'
    $value = $value -replace '\bJf\b', 'If'
    $value = $value -replace '\blf\b', 'If'
    $value = $value -replace '\bIow\b', 'low'
    $value = $value -replace '\bLVCK\b', 'LUCK'
    $value = $value -replace '\bARMOVR\b', 'ARMOUR'
    $value = $value -replace '\brobotes\b', "robot's"
    $value = $value -replace '\b100k\b', 'look'
    $value = $value -replace '\bJook\b', 'look'
    $value = $value -replace '\bmodem\b', 'modern'
    $value = $value -replace '\s+([,.;!?])', '$1'

    return $value.Trim()
}

function Normalize-ParagraphText([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ''
    }

    $normalizedParagraphs = foreach ($paragraph in ($text -split '\r?\n\s*\r?\n')) {
        $value = Normalize-Text $paragraph
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $value
        }
    }

    return ($normalizedParagraphs -join ([Environment]::NewLine + [Environment]::NewLine))
}

function Join-Lines([object[]]$lines) {
    $builder = [System.Text.StringBuilder]::new()

    foreach ($line in $lines) {
        $text = ($line.Text ?? $line.text ?? '').Trim()
        if ([string]::IsNullOrWhiteSpace($text)) {
            continue
        }

        if ($builder.Length -eq 0) {
            $null = $builder.Append($text)
            continue
        }

        if ($builder.ToString().EndsWith('-') -or $builder.ToString().EndsWith('•')) {
            $builder.Length--
            $null = $builder.Append($text)
        }
        else {
            $null = $builder.Append(' ')
            $null = $builder.Append($text)
        }
    }

    return (Normalize-Text $builder.ToString())
}

function Get-GroupText([object[]]$group) {
    return (Join-Lines $group)
}

function Get-RangeHeader([object[]]$lines) {
    $candidateText = (($lines | Select-Object -First 3 | ForEach-Object { $_.text ?? $_.Text }) -join ' ')
    $candidateText = Normalize-NumberishText $candidateText

    if ($candidateText -match '(?<start>\d{1,3})\s*-\s*(?<end>\d{1,3})') {
        $startText = $matches.start
        $endText = $matches.end
        $start = [int]$startText
        $end = [int]$endText

        if ($end -lt $start -and $endText.Length -lt $startText.Length) {
            $prefixLength = $startText.Length - $endText.Length
            $end = [int]($startText.Substring(0, $prefixLength) + $endText)
        }

        return @{
            Start = $start
            End = $end
        }
    }

    return $null
}

function Get-GroupHeaderId([object[]]$group) {
    foreach ($line in ($group | Select-Object -First 3)) {
        $text = Normalize-NumberishText ($line.Text ?? $line.text ?? '')
        if ($text -match '^\d{1,3}$') {
            return [int]$text
        }
    }

    return $null
}

function Test-IsChoiceTailGroup([object[]]$group) {
    if ($group.Count -eq 0) {
        return $false
    }

    foreach ($line in $group) {
        $text = Normalize-Text ($line.Text ?? $line.text ?? '')
        if ($text -notmatch '^(?i)(?:turn|tum|return|retum)?\s*(?:back\s*)?(?:to\s*)?[-\dIOl''"]*$') {
            return $false
        }
    }

    return $true
}

function Test-IsSkippableLine([string]$sideName, [object]$line) {
    $text = Normalize-Text ($line.text ?? $line.Text ?? '')
    $numberish = Normalize-NumberishText ($line.text ?? $line.Text ?? '')
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $true
    }

    $top = [int]$line.top

    if ($top -lt 110 -and $numberish -match '^\d{1,3}\s*-\s*\d{1,3}$') {
        return $true
    }

    if ($top -lt 105 -and $numberish -match '^\d{1,3}$') {
        return $true
    }

    if ($top -lt 120 -and $text -match '^(BACKGROU?ND|NOW TURN THE PAGE|NO ADMITTANCE)$') {
        return $true
    }

    if ($top -lt 120 -and $text -match '^[_().|!jilISAO-]{1,6}$') {
        return $true
    }

    return $false
}

function Get-ContentGroups([string]$sideName, [object[]]$lines) {
    $contentLines = [System.Collections.Generic.List[object]]::new()

    foreach ($line in $lines) {
        if (Test-IsSkippableLine $sideName $line) {
            continue
        }

        $contentLines.Add([pscustomobject]@{
                Text = Normalize-Text ($line.text ?? '')
                Left = [int]$line.left
                Top = [int]$line.top
            })
    }

    if ($contentLines.Count -eq 0) {
        return [object[]]@()
    }

    $groups = [System.Collections.Generic.List[object[]]]::new()
    $current = [System.Collections.Generic.List[object]]::new()
    $previousTop = $null

    foreach ($line in ($contentLines | Sort-Object { [math]::Floor($_.Top / 20) }, Left, Top)) {
        if ($null -ne $previousTop -and (($line.Top - $previousTop) -gt 40)) {
            $groups.Add(@($current.ToArray()))
            $current = [System.Collections.Generic.List[object]]::new()
        }

        $current.Add($line)
        $previousTop = $line.Top
    }

    if ($current.Count -gt 0) {
        $groups.Add(@($current.ToArray()))
    }

    $mergedGroups = [System.Collections.Generic.List[object[]]]::new()

    foreach ($group in $groups) {
        if ($mergedGroups.Count -gt 0 -and $null -eq (Get-GroupHeaderId $group) -and (Test-IsChoiceTailGroup $group)) {
            $lastIndex = $mergedGroups.Count - 1
            $mergedGroups[$lastIndex] = @($mergedGroups[$lastIndex] + $group)
            continue
        }

        $mergedGroups.Add($group)
    }

    return [object[]]($mergedGroups.ToArray())
}

function Test-IsContinuationGroup([object[]]$group) {
    if ($group.Count -eq 0) {
        return $false
    }

    $firstText = ($group[0].Text ?? '').Trim()
    if ([string]::IsNullOrWhiteSpace($firstText)) {
        return $false
    }

    return ($firstText -cmatch '^[a-z]' -or $firstText -match '^(on|and|or|but|if|then|otherwise)\b')
}

function Assign-Group([int]$id, [object[]]$group) {
    Add-GroupToParsedBlock $id $group
}

function Invoke-ManualSide([string]$sideName, [object[]]$groups) {
    $instructions = $manualAssignments[$sideName]
    if ($instructions.Count -ne $groups.Count) {
        throw "Manual assignment for '$sideName' expected $($instructions.Count) groups but found $($groups.Count)."
    }

    foreach ($index in 0..($groups.Count - 1)) {
        $instruction = $instructions[$index]
        switch ($instruction.Mode) {
            'Ignore' { }
            'Block' { Assign-Group -id ([int]$instruction.Id) -group $groups[$index] }
            default { throw "Unknown manual mode '$($instruction.Mode)' for '$sideName'." }
        }
    }
}

function Convert-ToWorldLocation([string]$location) {
    if ([string]::IsNullOrWhiteSpace($location)) {
        return [RobotCommando.GameBook.WorldLocation]::Unknown
    }

    switch ($location.Trim()) {
        'Unknown' { return [RobotCommando.GameBook.WorldLocation]::Unknown }
        'Current' { return [RobotCommando.GameBook.WorldLocation]::Inherit }
        'Inherit' { return [RobotCommando.GameBook.WorldLocation]::Inherit }
        'Farm' { return [RobotCommando.GameBook.WorldLocation]::Farm }
        'Capital City' { return [RobotCommando.GameBook.WorldLocation]::CapitalCity }
        'City of Industry' { return [RobotCommando.GameBook.WorldLocation]::CityOfIndustry }
        'City of Knowledge' { return [RobotCommando.GameBook.WorldLocation]::CityOfKnowledge }
        'City of Pleasure' { return [RobotCommando.GameBook.WorldLocation]::CityOfPleasure }
        'City of Storms' { return [RobotCommando.GameBook.WorldLocation]::CityOfStorms }
        'City of the Guardians' { return [RobotCommando.GameBook.WorldLocation]::CityOfTheGuardians }
        'City of the Jungle' { return [RobotCommando.GameBook.WorldLocation]::CityOfTheJungle }
        'City of Worship' { return [RobotCommando.GameBook.WorldLocation]::CityOfWorship }
        default { return [RobotCommando.GameBook.WorldLocation]::Unknown }
    }
}

function Normalize-Whitespace([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ''
    }

    return (($text -replace '\s+', ' ').Trim())
}

function Get-WorldLocationMappings() {
    return @(
        @{ Phrase = 'Farm'; Value = [RobotCommando.GameBook.WorldLocation]::Farm },
        @{ Phrase = 'Capital City'; Value = [RobotCommando.GameBook.WorldLocation]::CapitalCity },
        @{ Phrase = 'City of Industry'; Value = [RobotCommando.GameBook.WorldLocation]::CityOfIndustry },
        @{ Phrase = 'City of Knowledge'; Value = [RobotCommando.GameBook.WorldLocation]::CityOfKnowledge },
        @{ Phrase = 'City of Pleasure'; Value = [RobotCommando.GameBook.WorldLocation]::CityOfPleasure },
        @{ Phrase = 'City of Storms'; Value = [RobotCommando.GameBook.WorldLocation]::CityOfStorms },
        @{ Phrase = 'City of the Guardians'; Value = [RobotCommando.GameBook.WorldLocation]::CityOfTheGuardians },
        @{ Phrase = 'City of the Jungle'; Value = [RobotCommando.GameBook.WorldLocation]::CityOfTheJungle },
        @{ Phrase = 'City of Worship'; Value = [RobotCommando.GameBook.WorldLocation]::CityOfWorship }
    )
}

function Get-StrongLocationHint([string]$text) {
    $normalized = Normalize-Whitespace $text
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return [RobotCommando.GameBook.WorldLocation]::Unknown
    }

    $prefixes = @(
        'You are in the',
        'You are leaving the',
        'You have returned to the',
        'the buildings of the',
        'within the',
        'main building of the'
    )

    foreach ($mapping in (Get-WorldLocationMappings)) {
        foreach ($prefix in $prefixes) {
            $pattern = '(?i){0}\s+{1}' -f [regex]::Escape($prefix), [regex]::Escape($mapping.Phrase)
            if ($normalized -match $pattern) {
                return $mapping.Value
            }
        }
    }

    return [RobotCommando.GameBook.WorldLocation]::Unknown
}

function Get-PlaceLocationHint([string]$text) {
    $normalized = Normalize-Whitespace $text
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return [RobotCommando.GameBook.WorldLocation]::Unknown
    }

    $mappings = @(
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::Farm
            Phrases = @('robot parking-area', 'Dragonfly Model D', 'cowboy walking robot', 'dinosaur ranchers', 'rancher in the kingdom')
        }
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::CityOfKnowledge
            Phrases = @('College of Medicine', 'Thalian Museum', 'Dinosaur Preserve', 'College of War', 'War College')
        }
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::CityOfIndustry
            Phrases = @('Fuel Refining Plant', 'Robot Experimental Centre', 'Robot Experimental Center', 'tunnels underneath the city', 'junkyard')
        }
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::CityOfPleasure
            Phrases = @('Visit the arcades', 'Visit the airfield', 'the Arcade', 'the arcades', 'the airfield')
        }
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::CityOfStorms
            Phrases = @('Weather Bureau', 'sea-coast')
        }
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::CityOfWorship
            Phrases = @('one of the temples', 'shrines, temples, churches, cemeteries', 'CityofWorship')
        }
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::CityOfTheGuardians
            Phrases = @('Guardian Computer', 'Level Zero')
        }
        @{
            Value = [RobotCommando.GameBook.WorldLocation]::CapitalCity
            Phrases = @('seat of government of Thalos', 'white marble buildings', 'streets of the Capital City')
        }
    )

    foreach ($mapping in $mappings) {
        foreach ($phrase in $mapping.Phrases) {
            if ($normalized -match [regex]::Escape($phrase)) {
                return $mapping.Value
            }
        }
    }

    return [RobotCommando.GameBook.WorldLocation]::Unknown
}

function Get-ChoiceLocationHint([AllowNull()][string]$text, [AllowNull()][string]$condition) {
    $normalized = Normalize-Whitespace $text
    foreach ($mapping in (Get-WorldLocationMappings)) {
        if (-not [string]::IsNullOrWhiteSpace($normalized) -and $normalized -match [regex]::Escape($mapping.Phrase)) {
            return $mapping.Value
        }
    }

    switch -Regex (($condition ?? '').Trim()) {
        'WorldLocation\.CityOfKnowledge' { return [RobotCommando.GameBook.WorldLocation]::CityOfKnowledge }
        'WorldLocation\.CityOfIndustry' { return [RobotCommando.GameBook.WorldLocation]::CityOfIndustry }
        'WorldLocation\.CapitalCity' { return [RobotCommando.GameBook.WorldLocation]::CapitalCity }
    }

    return [RobotCommando.GameBook.WorldLocation]::Unknown
}

function Get-ProseTargets([AllowNull()][string]$text) {
    $normalized = Normalize-Whitespace $text
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return @()
    }

    $normalized = $normalized `
        -replace '(?i)turn\s+tot(?=\d)', 'turn to 1' `
        -replace '(?i)turn\s+tc(?=\s*\d)', 'turn to ' `
        -replace '(?i)return\s+tc(?=\s*\d)', 'return to ' `
        -replace '(?i)\bTurn to IXX\b', 'Turn to XX'

    $matches = [regex]::Matches($normalized, '(?i)\b(?:turn|return)\s+to\s*([0-9]{1,4})\b')
    $targets = [System.Collections.Generic.List[int]]::new()

    foreach ($match in $matches) {
        $target = [int]$match.Groups[1].Value
        if (-not $targets.Contains($target)) {
            $targets.Add($target)
        }
    }

    return @($targets)
}

function Add-LocationPropagationEdge {
    param(
        [System.Collections.Generic.List[object]]$Edges,
        [hashtable]$SeenEdges,
        [int]$From,
        [int]$To,
        [AllowNull()][string]$Text = '',
        [AllowNull()][string]$Condition = ''
    )

    $key = "$From->$To"
    if ($SeenEdges.ContainsKey($key)) {
        return
    }

    $Edges.Add([pscustomobject]@{
        From = $From
        To = $To
        Text = ($Text ?? '')
        Condition = ($Condition ?? '')
    })

    $SeenEdges[$key] = $true
}

function Get-LocationPropagationEdges([System.Collections.Generic.List[RobotCommando.GameBook.BookBlock]]$blocks) {
    $blockIds = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($block in $blocks) {
        [void]$blockIds.Add($block.Id)
    }

    $edges = [System.Collections.Generic.List[object]]::new()
    $seenEdges = @{}

    foreach ($block in $blocks) {
        foreach ($choice in $block.Choices) {
            if ($choice.To -lt 0 -or -not $blockIds.Contains($choice.To)) {
                continue
            }

            $conditionText = if ($null -ne $choice.Condition) { $choice.Condition.Text } else { '' }
            Add-LocationPropagationEdge -Edges $edges -SeenEdges $seenEdges -From $block.Id -To $choice.To -Text $choice.Text -Condition $conditionText
        }

        foreach ($target in Get-ProseTargets $block.Text) {
            if ($blockIds.Contains($target)) {
                Add-LocationPropagationEdge -Edges $edges -SeenEdges $seenEdges -From $block.Id -To $target
            }
        }
    }

    # Keep these inferred edges aligned with the graph reconstruction script.
    $manualEdges = @(
        @{ From = 40; To = 88; Text = '[hidden] know password 88' }
        @{ From = 43; To = 22; Text = '[hidden] know City of Guardians map ref 22' }
        @{ From = 80; To = 102; Text = '[inferred] leave the museum' }
        @{ From = 103; To = 111; Text = '[hidden] know duel-customs ref 111' }
        @{ From = 122; To = 22; Text = '[hidden] know City of Guardians map ref 22' }
        @{ From = 147; To = 14; Text = '[inferred] visit the Thalian Museum' }
        @{ From = 187; To = 7; Text = "[hidden] know countersign 'Seven'" }
        @{ From = 195; To = 110; Text = '[inferred] leave the junkyard' }
        @{ From = 197; To = 88; Text = '[hidden] recall password 88' }
        @{ From = 205; To = 111; Text = '[hidden] know duel-customs ref 111' }
        @{ From = 236; To = 7; Text = "[hidden] know countersign 'Seven'" }
        @{ From = 275; To = 301; Text = '[hidden] use the Tangler Field' }
        @{ From = 288; To = 112; Text = '[inferred] play Dinosaur Hunt' }
        @{ From = 305; To = 7; Text = "[hidden] know countersign 'Seven'" }
        @{ From = 316; To = 100; Text = '[hidden] Blue Potion (10 letters)' }
        @{ From = 316; To = 150; Text = '[hidden] Lavender Potion (15 letters)' }
        @{ From = 359; To = 200; Text = '[hidden] Wasp model number 200' }
    )

    foreach ($edge in $manualEdges) {
        if ($blockIds.Contains([int]$edge.From) -and $blockIds.Contains([int]$edge.To)) {
            Add-LocationPropagationEdge -Edges $edges -SeenEdges $seenEdges -From ([int]$edge.From) -To ([int]$edge.To) -Text ([string]$edge.Text)
        }
    }

    return $edges
}

function Resolve-BlockLocations([System.Collections.Generic.List[RobotCommando.GameBook.BookBlock]]$blocks) {
    $states = @{}

    foreach ($block in $blocks) {
        $choiceText = @($block.Choices | ForEach-Object { $_.Text }) -join ' '
        $strongHint = Get-StrongLocationHint ([string]::Join(' ', @($block.Text, $block.RevisitText)))
        $placeHint = Get-PlaceLocationHint ([string]::Join(' ', @($block.Text, $block.RevisitText, $choiceText)))
        $seed = if ($strongHint -ne [RobotCommando.GameBook.WorldLocation]::Unknown) { $strongHint } else { $placeHint }
        $locations = [System.Collections.Generic.HashSet[RobotCommando.GameBook.WorldLocation]]::new()
        if ($seed -ne [RobotCommando.GameBook.WorldLocation]::Unknown) {
            [void]$locations.Add($seed)
        }

        $states[$block.Id] = [pscustomobject]@{
            StrongHint = $strongHint
            PlaceHint = $placeHint
            Locations = $locations
        }
    }

    $edges = Get-LocationPropagationEdges $blocks

    $changed = $true
    while ($changed) {
        $changed = $false

        foreach ($edge in $edges) {
            if (-not $states.ContainsKey($edge.From) -or -not $states.ContainsKey($edge.To)) {
                continue
            }

            $targetState = $states[$edge.To]
            if ($targetState.StrongHint -ne [RobotCommando.GameBook.WorldLocation]::Unknown -or $targetState.PlaceHint -ne [RobotCommando.GameBook.WorldLocation]::Unknown) {
                continue
            }

            $edgeHint = Get-ChoiceLocationHint -text $edge.Text -condition $edge.Condition
            $locationsToAdd = if ($edgeHint -ne [RobotCommando.GameBook.WorldLocation]::Unknown) {
                @($edgeHint)
            }
            else {
                @($states[$edge.From].Locations)
            }

            foreach ($location in $locationsToAdd) {
                if ($location -eq [RobotCommando.GameBook.WorldLocation]::Unknown -or $location -eq [RobotCommando.GameBook.WorldLocation]::Inherit) {
                    continue
                }

                if ($targetState.Locations.Add($location)) {
                    $changed = $true
                }
            }
        }
    }

    foreach ($block in $blocks) {
        $state = $states[$block.Id]

        if ($state.StrongHint -ne [RobotCommando.GameBook.WorldLocation]::Unknown) {
            $block.Location = $state.StrongHint
            continue
        }

        $isMissingLocation = $block.Location -eq [RobotCommando.GameBook.WorldLocation]::Unknown -or $block.Location -eq [RobotCommando.GameBook.WorldLocation]::Inherit
        if (-not $isMissingLocation) {
            continue
        }

        if ($state.PlaceHint -ne [RobotCommando.GameBook.WorldLocation]::Unknown) {
            $block.Location = $state.PlaceHint
            continue
        }

        if ($state.Locations.Count -eq 1) {
            $block.Location = @($state.Locations)[0]
            continue
        }

        if ($state.Locations.Count -gt 1) {
            $block.Location = [RobotCommando.GameBook.WorldLocation]::Inherit
            continue
        }

        $block.Location = [RobotCommando.GameBook.WorldLocation]::Unknown
    }
}

function New-ConditionExpression([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    $condition = $conditionType::new()
    $condition.Text = (Normalize-DslExpression $text)
    return $condition
}

function New-EffectExpression([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    $effect = $effectType::new()
    $effect.Text = (Normalize-DslExpression $text)
    return $effect
}

function Normalize-DslExpression([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ''
    }

    $value = $text.Trim()
    $value = $value -replace '\bc\.Page\.Choices\[(\d+)\]\.Link\b', 'context.Page.Choices[$1].To'
    $value = $value -replace '\bc\.', 'context.'
    $value = $value -replace '\bMechaTypes\b', 'MechaType'
    $value = $value -replace '\bDinossaur\b', 'Dinosaur'
    $value = $value -replace '\s+', ' '
    return $value
}

function Convert-ToRobotFrame([object]$value) {
    if ($null -eq $value) {
        return [RobotCommando.GameBook.RobotFrame]::Unspecified
    }

    if ($value -is [string]) {
        switch ($value) {
            'Humanoid' { return [RobotCommando.GameBook.RobotFrame]::Humanoid }
            'Dinosaur' { return [RobotCommando.GameBook.RobotFrame]::Dinosaur }
            default { return [RobotCommando.GameBook.RobotFrame]::Unspecified }
        }
    }

    switch ([int]$value) {
        1 { return [RobotCommando.GameBook.RobotFrame]::Dinosaur }
        2 { return [RobotCommando.GameBook.RobotFrame]::Humanoid }
        default { return [RobotCommando.GameBook.RobotFrame]::Unspecified }
    }
}

function Convert-ToSpeedBand([object]$value) {
    if ($null -eq $value) {
        return [RobotCommando.GameBook.SpeedBand]::Static
    }

    if ($value -is [string]) {
        switch ($value.Trim()) {
            'Static' { return [RobotCommando.GameBook.SpeedBand]::Static }
            'Slow' { return [RobotCommando.GameBook.SpeedBand]::Slow }
            'Average' { return [RobotCommando.GameBook.SpeedBand]::Average }
            'Fast' { return [RobotCommando.GameBook.SpeedBand]::Fast }
            'Very Fast' { return [RobotCommando.GameBook.SpeedBand]::VeryFast }
            'Ultra Fast' { return [RobotCommando.GameBook.SpeedBand]::UltraFast }
            default { return [RobotCommando.GameBook.SpeedBand]::Static }
        }
    }

    switch ([int]$value) {
        0 { return [RobotCommando.GameBook.SpeedBand]::Static }
        1 { return [RobotCommando.GameBook.SpeedBand]::Slow }
        2 { return [RobotCommando.GameBook.SpeedBand]::Average }
        3 { return [RobotCommando.GameBook.SpeedBand]::Fast }
        4 { return [RobotCommando.GameBook.SpeedBand]::VeryFast }
        5 { return [RobotCommando.GameBook.SpeedBand]::UltraFast }
        default { return [RobotCommando.GameBook.SpeedBand]::Static }
    }
}

function Apply-EntityBase([object]$entity, [hashtable]$data) {
    $entity.Tag = $data['tag'] ?? $data['name'] ?? ''
    $entity.Name = $data['name'] ?? $data['tag'] ?? ''
    $entity.Icon = $data['icon']
    $entity.Description = (Normalize-ParagraphText ($data['description'] ?? ''))
}

function New-BattleOutcome([hashtable]$battleResult) {
    if ($null -eq $battleResult) {
        return $null
    }

    $outcome = [RobotCommando.GameBook.BattleOutcome]::new()

    if ($battleResult.ContainsKey('win')) {
        $outcome.Win = [int]$battleResult['win']
        $outcome.WinSpecified = $true
    }

    if ($battleResult.ContainsKey('lose')) {
        $outcome.Lose = [int]$battleResult['lose']
        $outcome.LoseSpecified = $true
    }

    if ($battleResult.ContainsKey('escape')) {
        $outcome.Escape = [int]$battleResult['escape']
        $outcome.EscapeSpecified = $true
    }

    return $outcome
}

function New-BookChoiceFromOld([hashtable]$choiceData) {
    $choice = [RobotCommando.GameBook.BookChoice]::new()
    $choice.To = [int]($choiceData['link'] ?? -1)
    $choice.Text = Normalize-Text ($choiceData['text'] ?? '')
    $choice.ShowWhenDisabled = [bool]($choiceData['greyedoutIfDisabled'] ?? $false)
    $choice.Condition = New-ConditionExpression ($choiceData['condition'] ?? '')
    $choice.Effect = New-EffectExpression ($choiceData['effect'] ?? '')
    return $choice
}

function New-ItemTrigger([hashtable]$triggerData) {
    if ($null -eq $triggerData) {
        return $null
    }

    $trigger = [RobotCommando.GameBook.ItemTrigger]::new()
    $trigger.Condition = New-ConditionExpression ($triggerData['condition'] ?? '')
    $trigger.Effect = New-EffectExpression ($triggerData['effect'] ?? '')
    return $trigger
}

function New-BookItemFromOld([hashtable]$itemData) {
    $item = [RobotCommando.GameBook.BookItem]::new()
    Apply-EntityBase -entity $item -data $itemData
    $item.OnAcquire = New-ItemTrigger ($itemData['onAcquire'] ?? $null)
    $item.OnDiscard = New-ItemTrigger ($itemData['onDiscard'] ?? $null)
    $item.OnUse = New-ItemTrigger ($itemData['onUse'] ?? $null)
    return $item
}

function New-BookRobotFromOld([hashtable]$robotData) {
    $robot = [RobotCommando.GameBook.BookRobot]::new()
    Apply-EntityBase -entity $robot -data $robotData
    $robot.Frame = Convert-ToRobotFrame ($robotData['types'] ?? $robotData['frame'])
    $robot.Armor = [int]($robotData['armor'] ?? 0)
    $robot.ArmorMax = [int]($robotData['armorMax'] ?? $robot.Armor)
    $robot.Speed = Convert-ToSpeedBand ($robotData['speed'] ?? 0)
    $robot.SpeedMax = Convert-ToSpeedBand ($robotData['speedMax'] ?? $robotData['speed'] ?? 0)
    $robot.CombatBonus = [int]($robotData['bonus'] ?? $robotData['combatBonus'] ?? 0)
    $robot.CombatBonusMax = [int]($robotData['bonusMax'] ?? $robotData['combatBonusMax'] ?? $robot.CombatBonus)
    return $robot
}

function New-BookEnemyFromOld([hashtable]$enemyData) {
    $enemy = [RobotCommando.GameBook.BookEnemy]::new()
    Apply-EntityBase -entity $enemy -data $enemyData
    $enemy.Stamina = [int]($enemyData['stamina'] ?? 0)
    $enemy.StaminaMax = [int]($enemyData['staminaMax'] ?? $enemy.Stamina)
    $enemy.Skill = [int]($enemyData['skill'] ?? 0)
    $enemy.SkillMax = [int]($enemyData['skillMax'] ?? $enemy.Skill)
    $enemy.BattleOutcome = New-BattleOutcome ($enemyData['battleResult'] ?? $null)
    return $enemy
}

function New-BookMonsterFromOld([hashtable]$monsterData) {
    $monster = [RobotCommando.GameBook.BookMonster]::new()
    Apply-EntityBase -entity $monster -data $monsterData
    $monster.Frame = Convert-ToRobotFrame ($monsterData['types'] ?? $monsterData['frame'])
    $monster.Armor = [int]($monsterData['armor'] ?? 0)
    $monster.ArmorMax = [int]($monsterData['armorMax'] ?? $monster.Armor)
    $monster.Skill = [int]($monsterData['skill'] ?? 0)
    $monster.SkillMax = [int]($monsterData['skillMax'] ?? $monster.Skill)
    $monster.Speed = Convert-ToSpeedBand ($monsterData['speed'] ?? 0)
    $monster.SpeedMax = Convert-ToSpeedBand ($monsterData['speedMax'] ?? $monsterData['speed'] ?? 0)
    $monster.BattleOutcome = New-BattleOutcome ($monsterData['battleResult'] ?? $null)
    return $monster
}

function Add-Choice([RobotCommando.GameBook.BookBlock]$block, [int]$to, [string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) {
        return
    }

    $choice = [RobotCommando.GameBook.BookChoice]::new()
    $choice.To = $to
    $choice.Text = $text.Trim()
    $block.Choices.Add($choice)
}

function Test-IsStandaloneTurnLine([string]$text) {
    $value = Normalize-Text $text
    return ($value -match '^(?i)(turn|tum|return|retum)\s+(?:back\s+)?to\s+-?\d+$')
}

function Get-StructuredChoiceProjection([int]$blockId, [object[]]$lines) {
    $sortedLines = @($lines | Sort-Object { [math]::Floor($_.Top / 20) }, Left, Top)
    $turnLines = @($sortedLines | Where-Object { Test-IsStandaloneTurnLine $_.Text })

    if ($turnLines.Count -lt 2) {
        return $null
    }

    $questionIndex = -1
    for ($index = 0; $index -lt $sortedLines.Count; $index++) {
        $text = Normalize-Text $sortedLines[$index].Text
        if ($text -match '(?i)(what will you do|where will you go|where would you like to go|which button will you press|which title will you choose)\s*:?' -or $text.EndsWith(':')) {
            $questionIndex = $index
            break
        }
    }

    if ($questionIndex -lt 0 -or $questionIndex -ge ($sortedLines.Count - 1)) {
        return $null
    }

    $postQuestionLines = @($sortedLines[($questionIndex + 1)..($sortedLines.Count - 1)])
    $promptLines = @($postQuestionLines | Where-Object {
            -not (Test-IsStandaloneTurnLine $_.Text) -and
            (Normalize-NumberishText $_.Text) -notmatch '^\d{1,3}$'
        })

    if ($promptLines.Count -lt 2) {
        return $null
    }

    $promptGroups = [System.Collections.Generic.List[object[]]]::new()
    $current = [System.Collections.Generic.List[object]]::new()
    $previousTop = $null

    foreach ($line in $promptLines) {
        if ($null -ne $previousTop -and (($line.Top - $previousTop) -gt 18)) {
            $promptGroups.Add(@($current.ToArray()))
            $current = [System.Collections.Generic.List[object]]::new()
        }

        $current.Add($line)
        $previousTop = $line.Top
    }

    if ($current.Count -gt 0) {
        $promptGroups.Add(@($current.ToArray()))
    }

    if ($promptGroups.Count -ne $turnLines.Count) {
        return $null
    }

    $choices = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $turnLines.Count; $index++) {
        $turnText = Normalize-Text $turnLines[$index].Text
        if ($turnText -notmatch '(?<to>-?\d+)$') {
            return $null
        }

        $choices.Add([pscustomobject]@{
                To = [int]$matches.to
                Text = (Get-GroupText $promptGroups[$index])
            })
    }

    $cleanLines = @($sortedLines | Where-Object { -not (Test-IsStandaloneTurnLine $_.Text) })
    $cleanText = Join-Lines $cleanLines
    $cleanText = $cleanText -replace ("^\s*{0}\s+" -f [regex]::Escape([string]$blockId)), ''

    return [pscustomobject]@{
        Text = $cleanText.Trim()
        Choices = @($choices.ToArray())
    }
}

function Get-AutoChoices([string]$text) {
    $choices = [System.Collections.Generic.List[object]]::new()

    if ([string]::IsNullOrWhiteSpace($text)) {
        return @()
    }

    $normalized = Normalize-Text $text
    $regex = [regex]'(?i)(?:turn|tum|return|retum)\s+(?:back\s+)?(?:immediately\s+)?to\s+(?<to>-?\d+)'
    $matches = $regex.Matches($normalized)
    $previousEnd = 0

    foreach ($match in $matches) {
        $segmentStart = $previousEnd

        if ($previousEnd -eq 0) {
            $boundaryCandidates = @(
                @(
                    $normalized.LastIndexOf('.', $match.Index),
                    $normalized.LastIndexOf('!', $match.Index),
                    $normalized.LastIndexOf(':', $match.Index)
                ) | Where-Object { $_ -ge 0 }
            )

            if ($boundaryCandidates.Count -gt 0) {
                $segmentStart = (($boundaryCandidates | Measure-Object -Maximum).Maximum + 1)
            }
        }

        $choiceText = $normalized.Substring($segmentStart, $match.Index - $segmentStart).Trim(" ", ",", ";", ":")
        $choiceText = $choiceText -replace '^\b(?:If|Then|Otherwise)\b\s*', '$&'
        $choiceText = $choiceText.Trim()

        if (-not [string]::IsNullOrWhiteSpace($choiceText)) {
            $choices.Add([pscustomobject]@{
                    To = [int]$match.Groups['to'].Value
                    Text = $choiceText
                })
        }

        $previousEnd = $match.Index + $match.Length
    }

    $deduped = [System.Collections.Generic.List[object]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($choice in $choices) {
        $choiceText = Normalize-Text $choice.Text
        if ($choiceText -notmatch '[A-Za-z?]') {
            continue
        }

        if ($choiceText -notmatch '[.?!]$') {
            if ($choiceText -match '\?$') {
                $choiceText = $choiceText.Trim()
            }
            else {
                $choiceText = "$choiceText."
            }
        }

        $key = "$($choice.To)|$choiceText"
        if ($seen.Add($key)) {
            $deduped.Add([pscustomobject]@{
                    To = $choice.To
                    Text = $choiceText
                })
        }
    }

    return @($deduped.ToArray())
}

function Read-JsonFile([string]$path) {
    return (Get-Content -Path $path -Raw | ConvertFrom-Json -AsHashtable)
}

$oldPages = @{}
foreach ($file in (Get-ChildItem -Path $OldPagesDirectory -Filter '*.json')) {
    $page = Read-JsonFile $file.FullName
    $oldPages[[int]$page['number']] = $page
}

$oldItems = Read-JsonFile $OldItemsPath
$oldRobots = Read-JsonFile $OldRobotsPath
$oldMonsters = Read-JsonFile $OldMonstersPath

$ocrFiles = Get-ChildItem -Path $OcrDirectory -Filter '*.json' | Where-Object {
    [int](($_.BaseName -split '\.')[1]) -le 109
} | Sort-Object {
    [int](($_.BaseName -split '\.')[1])
}, {
    if (($_.BaseName -split '\.')[2] -eq 'left') { 0 } else { 1 }
}

$lastAssignedId = $null

foreach ($file in $ocrFiles) {
    if ($skipOcrSides.Contains($file.Name)) {
        continue
    }

    $side = Read-JsonFile $file.FullName
    $groups = @(Get-ContentGroups -sideName $file.Name -lines $side['lines'])

    if ($groups.Count -eq 0) {
        continue
    }

    if ($manualAssignments.ContainsKey($file.Name)) {
        Invoke-ManualSide -sideName $file.Name -groups $groups
        $lastInstruction = $manualAssignments[$file.Name] | Where-Object { $_.Mode -eq 'Block' } | Select-Object -Last 1
        if ($null -ne $lastInstruction) {
            $lastAssignedId = [int]$lastInstruction.Id
        }
        continue
    }

    $range = Get-RangeHeader $side['lines']

    if ($null -ne $range) {
        $expected = $range.End - $range.Start + 1

        if ($groups.Count -gt $expected -and $null -ne $lastAssignedId -and $lastAssignedId -eq ($range.Start - 1) -and -not (Get-GroupHeaderId $groups[0]) -and (Test-IsContinuationGroup $groups[0])) {
            Assign-Group -id $lastAssignedId -group $groups[0]
            if ($groups.Count -gt 1) {
                $groups = @($groups[1..($groups.Count - 1)])
            }
            else {
                $groups = @()
            }
        }

        while ($groups.Count -gt $expected) {
            $merged = $false

            for ($index = 1; $index -lt $groups.Count; $index++) {
                if ($null -eq (Get-GroupHeaderId $groups[$index])) {
                    $groups[$index - 1] = @($groups[$index - 1] + $groups[$index])

                    if ($index -lt ($groups.Count - 1)) {
                        $groups = @($groups[0..($index - 1)] + $groups[($index + 1)..($groups.Count - 1)])
                    }
                    else {
                        $groups = @($groups[0..($index - 1)])
                    }

                    $merged = $true
                    break
                }
            }

            if (-not $merged) {
                break
            }
        }

        if ($groups.Count -lt $expected) {
            throw "Side '$($file.Name)' expected $expected blocks from range $($range.Start)-$($range.End), but only found $($groups.Count) groups."
        }

        for ($offset = 0; $offset -lt $expected; $offset++) {
            Assign-Group -id ($range.Start + $offset) -group $groups[$offset]
            $lastAssignedId = $range.Start + $offset
        }

        continue
    }

    if ($null -ne $lastAssignedId -and -not (Get-GroupHeaderId $groups[0]) -and (Test-IsContinuationGroup $groups[0])) {
        Assign-Group -id $lastAssignedId -group $groups[0]

        if ($groups.Count -gt 1) {
            $groups = @($groups[1..($groups.Count - 1)])
        }
        else {
            $groups = @()
        }
    }

    if ($groups.Count -eq 0) {
        continue
    }

    $currentId = Get-GroupHeaderId $groups[0]
    if ($null -eq $currentId) {
        if ($null -eq $lastAssignedId) {
            continue
        }

        $currentId = $lastAssignedId + 1
    }

    foreach ($group in $groups) {
        $headerId = Get-GroupHeaderId $group
        if ($null -ne $headerId) {
            $currentId = $headerId
        }

        Assign-Group -id $currentId -group $group
        $lastAssignedId = $currentId
        $currentId++
    }
}

function Build-GenericOldPageBlock([hashtable]$pageData) {
    $block = [RobotCommando.GameBook.BookBlock]::new()
    $block.Id = [int]$pageData['number']
    $block.Location = Convert-ToWorldLocation ($pageData['location'] ?? '')
    $block.Text = Normalize-ParagraphText ($pageData['text'] ?? '')
    $block.RevisitText = Normalize-ParagraphText ($pageData['text2'] ?? '')

    foreach ($choiceData in ($pageData['choices'] ?? @())) {
        $block.Choices.Add((New-BookChoiceFromOld $choiceData))
    }

    foreach ($itemData in ($pageData['items'] ?? @())) {
        $reference = if ($oldItems.ContainsKey($itemData['name'])) { $oldItems[$itemData['name']] } else { $itemData }
        $merged = @{}
        foreach ($entry in $reference.GetEnumerator()) {
            $merged[$entry.Key] = $entry.Value
        }
        foreach ($entry in $itemData.GetEnumerator()) {
            $merged[$entry.Key] = $entry.Value
        }
        $block.Items.Add((New-BookItemFromOld $merged))
    }

    foreach ($robotData in ($pageData['robots'] ?? @())) {
        $reference = if ($oldRobots.ContainsKey($robotData['name'])) { $oldRobots[$robotData['name']] } else { $robotData }
        $merged = @{}
        foreach ($entry in $reference.GetEnumerator()) {
            $merged[$entry.Key] = $entry.Value
        }
        foreach ($entry in $robotData.GetEnumerator()) {
            $merged[$entry.Key] = $entry.Value
        }
        $block.Robots.Add((New-BookRobotFromOld $merged))
    }

    foreach ($enemyData in ($pageData['enemies'] ?? @())) {
        $block.Enemies.Add((New-BookEnemyFromOld $enemyData))
    }

    foreach ($monsterData in ($pageData['monsters'] ?? @())) {
        $reference = if ($oldMonsters.ContainsKey($monsterData['name'])) { $oldMonsters[$monsterData['name']] } else { $monsterData }
        $merged = @{}
        foreach ($entry in $reference.GetEnumerator()) {
            $merged[$entry.Key] = $entry.Value
        }
        foreach ($entry in $monsterData.GetEnumerator()) {
            $merged[$entry.Key] = $entry.Value
        }
        $block.Monsters.Add((New-BookMonsterFromOld $merged))
    }

    foreach ($effectText in ($pageData['effects'] ?? @())) {
        $block.Effects.Add((New-EffectExpression $effectText))
    }

    return $block
}

function Build-Page14Block() {
    $page14 = $oldPages[14]
    $page1014 = $oldPages[1014]

    $block = [RobotCommando.GameBook.BookBlock]::new()
    $block.Id = 14
    $block.Location = Convert-ToWorldLocation ($page14['location'] ?? '')
    $block.Text = Normalize-ParagraphText ((Normalize-Text ($page14['text'] ?? '')) + [Environment]::NewLine + [Environment]::NewLine + (Normalize-Text ($page1014['text'] ?? '')))
    $block.RevisitText = Normalize-ParagraphText ($page14['text'] ?? '')

    $returnChoice = New-BookChoiceFromOld $page14['choices'][0]
    $block.Choices.Add($returnChoice)

    foreach ($choiceData in $page1014['choices']) {
        $choice = New-BookChoiceFromOld $choiceData
        $choice.Condition = New-ConditionExpression '!context.Page.IsVisited'
        $choice.Effect = New-EffectExpression 'context.Page.IsVisited = true'
        $block.Choices.Add($choice)
    }

    return $block
}

$blocks = [System.Collections.Generic.List[RobotCommando.GameBook.BookBlock]]::new()

for ($id = 0; $id -le 400; $id++) {
    $block = $null

    if ($id -eq 14 -and $oldPages.ContainsKey(14) -and $oldPages.ContainsKey(1014)) {
        $block = Build-Page14Block
    }
    elseif ($oldPages.ContainsKey($id)) {
        $block = Build-GenericOldPageBlock $oldPages[$id]
    }
    else {
        if (-not $parsedBlocks.ContainsKey($id)) {
            throw "Missing OCR block $id."
        }

        $parsed = $parsedBlocks[$id]
        $text = Normalize-Text (($parsed.TextParts.ToArray()) -join ' ')
        if ([string]::IsNullOrWhiteSpace($text)) {
            throw "Block $id has no extracted text."
        }

        $text = $text -replace ("^\s*{0}\s+" -f [regex]::Escape([string]$id)), ''
        $structuredProjection = Get-StructuredChoiceProjection -blockId $id -lines @($parsed.Lines.ToArray())
        if ($null -ne $structuredProjection) {
            $text = $structuredProjection.Text
        }

        $block = [RobotCommando.GameBook.BookBlock]::new()
        $block.Id = $id
        $block.Location = [RobotCommando.GameBook.WorldLocation]::Unknown
        $block.Text = $text

        $choiceSource = if ($null -ne $structuredProjection) { $structuredProjection.Choices } else { Get-AutoChoices $text }
        foreach ($choiceData in $choiceSource) {
            Add-Choice -block $block -to ([int]$choiceData.To) -text (Normalize-Text $choiceData.Text)
        }
    }

    $blocks.Add($block)
}

Resolve-BlockLocations $blocks

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

foreach ($block in $blocks) {
    $path = Join-Path $OutputDirectory ('{0:D4}.xml' -f $block.Id)
    [RobotCommando.GameBook.BookBlockXmlStore]::Save($path, $block)
}

Write-Host ("Generated {0} XML blocks in '{1}'." -f $blocks.Count, (Resolve-Path $OutputDirectory))
