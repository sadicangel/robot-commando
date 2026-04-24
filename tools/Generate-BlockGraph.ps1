[CmdletBinding()]
param(
    [string]$BlocksDirectory = (Join-Path $PSScriptRoot "..\RobotCommando\BookData\Blocks"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\RobotCommando\BookData\BlockGraph.md")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-Whitespace {
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    return (($Text -replace "\r?\n", " ") -replace "\s+", " ").Trim()
}

function Escape-MermaidText {
    param(
        [AllowNull()][string]$Text,
        [int]$MaxLength = 72
    )

    $clean = Normalize-Whitespace $Text
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return ""
    }

    $clean = $clean.Replace('"', "'")
    $clean = $clean.Replace("|", "/")
    $clean = $clean.Replace("[", "")
    $clean = $clean.Replace("]", "")
    $clean = $clean.Replace("(", "")
    $clean = $clean.Replace(")", "")
    $clean = $clean.Replace("{", "")
    $clean = $clean.Replace("}", "")

    if ($clean.Length -gt $MaxLength) {
        return $clean.Substring(0, $MaxLength - 3).TrimEnd() + "..."
    }

    return $clean
}

function Get-NodeId {
    param([int]$BlockId)

    return ("b{0:D4}" -f $BlockId)
}

function Get-BlockChoices {
    param([xml]$Xml)

    $choicesNode = $Xml.block.SelectSingleNode('choices')
    if ($null -eq $choicesNode) {
        return @()
    }

    $choices = $choicesNode.SelectNodes('choice')
    if ($null -eq $choices) {
        return @()
    }

    return @($choices | Where-Object { $null -ne $_ })
}

function Get-ConditionSummary {
    param([AllowNull()][string]$Condition)

    $condition = Normalize-Whitespace $Condition
    if ([string]::IsNullOrWhiteSpace($condition)) {
        return ""
    }

    switch -Regex ($condition) {
        '^context\.Page\.IsVisited$' { return "revisit" }
        '^!context\.Page\.IsVisited$' { return "first visit" }
        '^context\.Robot is not null$' { return "requires robot" }
        '^context\.Inventory\.Contains\("(.+)"\)$' { return "have $($Matches[1])" }
        '^!context\.Inventory\.Contains\("(.+)"\)$' { return "missing $($Matches[1])" }
        '^context\.Page\.Location == WorldLocation\.CityOfKnowledge$' { return "from City of Knowledge" }
        '^context\.Page\.Location == WorldLocation\.CityOfIndustry$' { return "from City of Industry" }
        '^context\.Page\.Location == WorldLocation\.CapitalCity$' { return "from Capital City" }
        '^context\.Robot is not null && context\.Robot\.Abilities\.Any\(a => a\.Name == "Flying"\)$' { return "if robot can fly" }
        '^context\.Robot is not null && !context\.Robot\.Abilities\.Any\(a => a\.Name == "Flying"\)$' { return "if robot cannot fly" }
    }

    return Escape-MermaidText $condition 56
}

function Get-XmlChildText {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$ChildName
    )

    $child = $Node.SelectSingleNode($ChildName)
    if ($null -eq $child) {
        return ""
    }

    return [string]$child.InnerText
}

function New-EdgeLabel {
    param(
        [AllowNull()][string]$Text,
        [AllowNull()][string]$Condition,
        [AllowNull()][string]$Prefix = $null
    )

    $parts = [System.Collections.Generic.List[string]]::new()

    if (-not [string]::IsNullOrWhiteSpace($Prefix)) {
        $parts.Add($Prefix)
    }

    $choiceText = Escape-MermaidText $Text
    if (-not [string]::IsNullOrWhiteSpace($choiceText)) {
        $parts.Add($choiceText)
    }

    $conditionSummary = Get-ConditionSummary $Condition
    if (-not [string]::IsNullOrWhiteSpace($conditionSummary)) {
        $parts.Add("[$conditionSummary]")
    }

    return ($parts -join " ").Trim()
}

function Add-Edge {
    param(
        [System.Collections.Generic.List[object]]$Edges,
        [hashtable]$SeenEdges,
        [int]$From,
        [string]$ToNodeId,
        [string]$Label
    )

    $key = "$From->$ToNodeId"
    if ($SeenEdges.ContainsKey($key)) {
        return
    }

    $Edges.Add([pscustomobject]@{
        From = $From
        ToNodeId = $ToNodeId
        Label = Escape-MermaidText $Label 96
    })

    $SeenEdges[$key] = $true
}

function Get-ProseTargets {
    param([AllowNull()][string]$Text)

    $normalized = Normalize-Whitespace $Text
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

function Get-BlockLabel {
    param(
        [object]$Block,
        [System.Collections.Generic.HashSet[int]]$VictoryIds
    )

    return "$($Block.Id): $($Block.Location)"
}

function Get-CompactNodeLabel {
    param([object]$Block)

    return [string]$Block.Id
}

function Get-LocationClassName {
    param([object]$Block)

    $location = Normalize-Whitespace ([string]$Block.Location)
    if ([string]::IsNullOrWhiteSpace($location)) {
        return 'loc_misc'
    }

    $safe = ($location -replace '[^A-Za-z0-9]+', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($safe)) {
        $safe = 'misc'
    }

    return "loc_$safe"
}

function Get-LocationSortKey {
    param([object]$Block)

    switch ([string]$Block.Location) {
        'Farm' { 0 }
        'Current' { 10 }
        'Capital City' { 20 }
        'City of Knowledge' { 30 }
        'City of Industry' { 40 }
        'City of the Guardians' { 50 }
        'City of the Jungle' { 60 }
        'City of Storms' { 70 }
        'City of Worship' { 80 }
        'City of Pleasure' { 90 }
        'Inherit' { 100 }
        'Unknown' { 110 }
        default { 120 }
    }
}

function Get-CompactChoiceLabel {
    param(
        [AllowNull()][string]$Text,
        [AllowNull()][string]$Condition
    )

    $conditionSummary = Get-ConditionSummary $Condition
    if (-not [string]::IsNullOrWhiteSpace($conditionSummary)) {
        return $conditionSummary
    }

    $clean = Normalize-Whitespace $Text
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return ""
    }

    switch -Regex ($clean) {
        '^(?i)if you defeat' { return 'win' }
        '^(?i)if you destroy' { return 'win' }
        '^(?i)if your .*armour.*reduced.*0' { return 'lose' }
        '^(?i)if they defeat you' { return 'lose' }
        '^(?i)if it reduces your .*armour' { return 'lose' }
        '^(?i)if you escape' { return 'escape' }
        '^(?i).*if you escape.*' { return 'escape' }
        '^(?i)if you are lucky' { return 'lucky' }
        '^(?i)if you are unlucky' { return 'unlucky' }
        '^(?i)if you roll' { return Escape-MermaidText $clean 24 }
        '^(?i)roll one die' { return 'roll die' }
        '^(?i)test your luck' { return 'test luck' }
        '^(?i)test your skill' { return 'test skill' }
        '^(?i)if you do not know' { return 'no password' }
        '^(?i)if you know' { return Escape-MermaidText $clean 24 }
        '^(?i)otherwise' { return 'otherwise' }
        '^(?i)if not' { return 'otherwise' }
        '^(?i)if you have' { return Escape-MermaidText $clean 24 }
        '^(?i)if you do not have' { return Escape-MermaidText $clean 24 }
    }

    return ""
}

function Add-MermaidDiagram {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Title,
        [object[]]$DiagramEdges,
        [hashtable]$BlockById,
        [System.Collections.Generic.HashSet[int]]$VictoryIds,
        [System.Collections.Generic.HashSet[int]]$EndingIds
    )

    $nodeIds = [System.Collections.Generic.HashSet[int]]::new()
    $includeUnknown = $false
    $locationNodes = @{}

    foreach ($edge in $DiagramEdges) {
        [void]$nodeIds.Add([int]$edge.From)

        if ($edge.ToNodeId -eq 'unknown') {
            $includeUnknown = $true
            continue
        }

        if ($edge.ToNodeId -match '^b(\d{4})$') {
            [void]$nodeIds.Add([int]$Matches[1])
        }
    }

    foreach ($nodeId in ($nodeIds | Sort-Object)) {
        $block = $BlockById[$nodeId]
        $className = Get-LocationClassName $block

        if (-not $locationNodes.ContainsKey($className)) {
            $locationNodes[$className] = [System.Collections.Generic.List[int]]::new()
        }

        $locationNodes[$className].Add($nodeId)
    }

    $layoutHintCount = 0
    foreach ($entry in $locationNodes.GetEnumerator()) {
        if ($entry.Value.Count -gt 1) {
            $layoutHintCount += ($entry.Value.Count - 1)
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($Title)) {
        $Lines.Add("## $Title")
        $Lines.Add('')
    }

    $Lines.Add(('- Edge count: {0} visible edges plus {1} invisible location links.' -f $DiagramEdges.Count, $layoutHintCount))
    $Lines.Add('- Node labels are block numbers only. Node colors indicate location.')
    $Lines.Add('')
    $Lines.Add('```mermaid')
    $Lines.Add('flowchart TD')
    $Lines.Add('    classDef start fill:#d0ebff,stroke:#1c7ed6,stroke-width:2px,color:#000;')
    $Lines.Add('    classDef victory fill:#d8f5d0,stroke:#2b8a3e,stroke-width:3px,color:#000;')
    $Lines.Add('    classDef ending fill:#ffe3e3,stroke:#c92a2a,stroke-width:2px,color:#000;')
    $Lines.Add('    classDef unknown fill:#fff3bf,stroke:#f08c00,stroke-width:2px,color:#000;')
    $Lines.Add('    classDef loc_Farm fill:#e3fafc,stroke:#1098ad,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Current fill:#edf2ff,stroke:#4263eb,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Capital_City fill:#ffe3e3,stroke:#c92a2a,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Knowledge fill:#f3e8ff,stroke:#7b2cbf,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Industry fill:#d3f9d8,stroke:#2b8a3e,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_the_Guardians fill:#e6fcf5,stroke:#0ca678,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_the_Jungle fill:#fff3bf,stroke:#f08c00,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Storms fill:#d0ebff,stroke:#1c7ed6,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Worship fill:#f8f0fc,stroke:#ae3ec9,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Pleasure fill:#ffdeeb,stroke:#d6336c,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Inherit fill:#f1f3f5,stroke:#868e96,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Unknown fill:#fff9db,stroke:#fab005,color:#000,font-size:10px;')
    $Lines.Add('')

    foreach ($nodeId in ($nodeIds | Sort-Object { (Get-LocationSortKey $BlockById[$_]) }, { $_ })) {
        $block = $BlockById[$nodeId]
        $label = Get-CompactNodeLabel $block
        $Lines.Add(('    {0}["{1}"]' -f (Get-NodeId $nodeId), (Escape-MermaidText $label 96)))
    }

    if ($includeUnknown) {
        $Lines.Add('    unknown["?"]')
    }

    $Lines.Add('')

    foreach ($edge in ($DiagramEdges | Sort-Object From, ToNodeId, Label)) {
        if ([string]::IsNullOrWhiteSpace($edge.Label)) {
            $Lines.Add(("    {0} --> {1}" -f (Get-NodeId $edge.From), $edge.ToNodeId))
        }
        else {
            $Lines.Add(("    {0} -->|{1}| {2}" -f (Get-NodeId $edge.From), $edge.Label, $edge.ToNodeId))
        }
    }

    $Lines.Add('')

    foreach ($entry in ($locationNodes.GetEnumerator() | Sort-Object Name)) {
        $sortedNodeIds = @($entry.Value | Sort-Object)
        for ($i = 1; $i -lt $sortedNodeIds.Count; $i++) {
            $Lines.Add(("    {0} ~~~ {1}" -f (Get-NodeId $sortedNodeIds[$i - 1]), (Get-NodeId $sortedNodeIds[$i])))
        }
    }

    $Lines.Add('')

    foreach ($entry in ($locationNodes.GetEnumerator() | Sort-Object Name)) {
        $classNodeIds = @($entry.Value | Sort-Object | ForEach-Object { Get-NodeId $_ })
        if ($classNodeIds.Count -gt 0) {
            $Lines.Add(('    class {0} {1};' -f ($classNodeIds -join ","), $entry.Key))
        }
    }

    if ($nodeIds.Contains(0)) {
        $Lines.Add('    class b0000 start;')
    }

    if ($includeUnknown) {
        $Lines.Add('    class unknown unknown;')
    }

    $diagramVictoryNodeIds = @($nodeIds | Where-Object { $VictoryIds.Contains($_) } | Sort-Object | ForEach-Object { Get-NodeId $_ })
    if ($diagramVictoryNodeIds.Count -gt 0) {
        $Lines.Add(('    class {0} victory;' -f ($diagramVictoryNodeIds -join ",")))
    }

    $diagramEndingNodeIds = @($nodeIds | Where-Object { $EndingIds.Contains($_) } | Sort-Object | ForEach-Object { Get-NodeId $_ })
    if ($diagramEndingNodeIds.Count -gt 0) {
        $Lines.Add(('    class {0} ending;' -f ($diagramEndingNodeIds -join ",")))
    }

    $Lines.Add('```')
    $Lines.Add('')
}

function Get-LocationNodeId {
    param([string]$Location)

    $safe = ($Location -replace '[^A-Za-z0-9]+', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($safe)) {
        $safe = 'misc'
    }

    return "locnode_$safe"
}

function Add-CommonClassDefinitions {
    param([System.Collections.Generic.List[string]]$Lines)

    $Lines.Add('    classDef start fill:#d0ebff,stroke:#1c7ed6,stroke-width:2px,color:#000;')
    $Lines.Add('    classDef victory fill:#d8f5d0,stroke:#2b8a3e,stroke-width:3px,color:#000;')
    $Lines.Add('    classDef ending fill:#ffe3e3,stroke:#c92a2a,stroke-width:2px,color:#000;')
    $Lines.Add('    classDef unknown fill:#fff3bf,stroke:#f08c00,stroke-width:2px,color:#000;')
    $Lines.Add('    classDef external fill:#f1f3f5,stroke:#868e96,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Farm fill:#e3fafc,stroke:#1098ad,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Current fill:#edf2ff,stroke:#4263eb,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Capital_City fill:#ffe3e3,stroke:#c92a2a,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Knowledge fill:#f3e8ff,stroke:#7b2cbf,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Industry fill:#d3f9d8,stroke:#2b8a3e,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_the_Guardians fill:#e6fcf5,stroke:#0ca678,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_the_Jungle fill:#fff3bf,stroke:#f08c00,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Storms fill:#d0ebff,stroke:#1c7ed6,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Worship fill:#f8f0fc,stroke:#ae3ec9,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_City_of_Pleasure fill:#ffdeeb,stroke:#d6336c,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Inherit fill:#f1f3f5,stroke:#868e96,color:#000,font-size:10px;')
    $Lines.Add('    classDef loc_Unknown fill:#fff9db,stroke:#fab005,color:#000,font-size:10px;')
}

function Get-EdgeTargetBlockId {
    param([object]$Edge)

    if ($null -eq $Edge) {
        return $null
    }

    if ([string]$Edge.ToNodeId -match '^b(\d{4})$') {
        return [int]$Matches[1]
    }

    return $null
}

function Add-LocationOverviewDiagram {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [object[]]$Blocks,
        [object[]]$Edges,
        [hashtable]$BlockById
    )

    $locations = @(
        $Blocks |
            Group-Object Location |
            Sort-Object { Get-LocationSortKey $_.Group[0] }, Name
    )

    $crossEdges = @(
        $Edges |
            Where-Object { $null -ne (Get-EdgeTargetBlockId $_) } |
            Group-Object {
                $fromLocation = [string]$BlockById[[int]$_.From].Location
                $targetBlockId = Get-EdgeTargetBlockId $_
                $toLocation = [string]$BlockById[$targetBlockId].Location
                '{0}|{1}' -f $fromLocation, $toLocation
            } |
            ForEach-Object {
                $parts = $_.Name.Split('|', 2)
                [pscustomobject]@{
                    FromLocation = $parts[0]
                    ToLocation = $parts[1]
                    Count = $_.Count
                }
            } |
            Where-Object { $_.FromLocation -ne $_.ToLocation } |
            Sort-Object @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'FromLocation'; Descending = $false }, @{ Expression = 'ToLocation'; Descending = $false }
    )

    $Lines.Add('## Overview')
    $Lines.Add('')
    $Lines.Add('- Cross-location transitions only. Use the per-location graphs below for block-level detail.')
    $Lines.Add('')
    $Lines.Add('```mermaid')
    $Lines.Add('flowchart LR')
    Add-CommonClassDefinitions $Lines
    $Lines.Add('')

    foreach ($locationGroup in $locations) {
        $location = [string]$locationGroup.Name
        $nodeId = Get-LocationNodeId $location
        $Lines.Add(('    {0}["{1}"]' -f $nodeId, (Escape-MermaidText $location 96)))
    }

    $Lines.Add('')

    foreach ($edge in $crossEdges) {
        $fromNodeId = Get-LocationNodeId $edge.FromLocation
        $toNodeId = Get-LocationNodeId $edge.ToLocation
        $Lines.Add(('    {0} -->|{1}| {2}' -f $fromNodeId, $edge.Count, $toNodeId))
    }

    $Lines.Add('')

    foreach ($locationGroup in $locations) {
        $location = [string]$locationGroup.Name
        $nodeId = Get-LocationNodeId $location
        $className = Get-LocationClassName $locationGroup.Group[0]
        $Lines.Add(('    class {0} {1};' -f $nodeId, $className))
    }

    $Lines.Add('```')
    $Lines.Add('')
}

function Add-LocationDiagram {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Location,
        [object[]]$Blocks,
        [object[]]$Edges,
        [hashtable]$BlockById,
        [System.Collections.Generic.HashSet[int]]$VictoryIds,
        [System.Collections.Generic.HashSet[int]]$EndingIds
    )

    $internalBlockIds = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($block in $Blocks) {
        [void]$internalBlockIds.Add([int]$block.Id)
    }

    $outgoingEdges = @($Edges | Where-Object { $internalBlockIds.Contains([int]$_.From) })
    $unknownEdges = @($outgoingEdges | Where-Object { $_.ToNodeId -eq 'unknown' })
    $knownOutgoingEdges = @($outgoingEdges | Where-Object { $null -ne (Get-EdgeTargetBlockId $_) })
    $internalEdges = @($knownOutgoingEdges | Where-Object { $internalBlockIds.Contains((Get-EdgeTargetBlockId $_)) })
    $externalEdges = @($knownOutgoingEdges | Where-Object { -not $internalBlockIds.Contains((Get-EdgeTargetBlockId $_)) })

    $externalTargetIds = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($edge in $externalEdges) {
        [void]$externalTargetIds.Add((Get-EdgeTargetBlockId $edge))
    }

    $exitLocations = @(
        $externalEdges |
            Group-Object {
                $targetBlockId = Get-EdgeTargetBlockId $_
                [string]$BlockById[$targetBlockId].Location
            } |
            Sort-Object @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'Name'; Descending = $false } |
            ForEach-Object { '{0} ({1})' -f $_.Name, $_.Count }
    )

    $Lines.Add(('## {0}' -f $Location))
    $Lines.Add('')
    $Lines.Add(('- Blocks: {0}. Internal edges: {1}. External edges: {2}. Unresolved edges: {3}.' -f $Blocks.Count, $internalEdges.Count, $externalEdges.Count, $unknownEdges.Count))
    if ($exitLocations.Count -gt 0) {
        $Lines.Add(('- Exits to: {0}.' -f ($exitLocations -join ', ')))
    }
    $Lines.Add('')
    $Lines.Add('```mermaid')
    $Lines.Add('flowchart TD')
    Add-CommonClassDefinitions $Lines
    $Lines.Add('')

    foreach ($block in ($Blocks | Sort-Object Id)) {
        $Lines.Add(('    {0}["{1}"]' -f (Get-NodeId $block.Id), (Get-CompactNodeLabel $block)))
    }

    foreach ($externalTargetId in ($externalTargetIds | Sort-Object)) {
        $Lines.Add(('    e{0:D4}["{1}"]' -f $externalTargetId, $externalTargetId))
    }

    $hasUnknown = $unknownEdges.Count -gt 0
    if ($hasUnknown) {
        $Lines.Add('    unknown["?"]')
    }

    $Lines.Add('')

    foreach ($edge in ($internalEdges | Sort-Object From, ToNodeId, Label)) {
        $targetBlockId = Get-EdgeTargetBlockId $edge
        if ([string]::IsNullOrWhiteSpace($edge.Label)) {
            $Lines.Add(('    {0} --> {1}' -f (Get-NodeId $edge.From), (Get-NodeId $targetBlockId)))
        }
        else {
            $Lines.Add(('    {0} -->|{1}| {2}' -f (Get-NodeId $edge.From), $edge.Label, (Get-NodeId $targetBlockId)))
        }
    }

    foreach ($edge in ($externalEdges | Sort-Object From, ToNodeId, Label)) {
        $targetBlockId = Get-EdgeTargetBlockId $edge
        $targetNodeId = ('e{0:D4}' -f $targetBlockId)
        if ([string]::IsNullOrWhiteSpace($edge.Label)) {
            $Lines.Add(('    {0} --> {1}' -f (Get-NodeId $edge.From), $targetNodeId))
        }
        else {
            $Lines.Add(('    {0} -->|{1}| {2}' -f (Get-NodeId $edge.From), $edge.Label, $targetNodeId))
        }
    }

    if ($hasUnknown) {
        foreach ($edge in ($unknownEdges | Sort-Object From, Label)) {
            $label = if ([string]::IsNullOrWhiteSpace($edge.Label)) { 'unknown' } else { $edge.Label }
            $Lines.Add(('    {0} -->|{1}| unknown' -f (Get-NodeId $edge.From), $label))
        }
    }

    $Lines.Add('')

    $internalNodeIds = @($Blocks | Sort-Object Id | ForEach-Object { Get-NodeId $_.Id })
    if ($internalNodeIds.Count -gt 0) {
        $className = Get-LocationClassName $Blocks[0]
        $Lines.Add(('    class {0} {1};' -f ($internalNodeIds -join ","), $className))
    }

    $externalNodeIds = @($externalTargetIds | Sort-Object | ForEach-Object { 'e{0:D4}' -f $_ })
    if ($externalNodeIds.Count -gt 0) {
        $Lines.Add(('    class {0} external;' -f ($externalNodeIds -join ",")))
    }

    if ($Blocks.Where({ $_.Id -eq 0 }).Count -gt 0) {
        $Lines.Add('    class b0000 start;')
    }

    $diagramVictoryIds = @($Blocks | Where-Object { $VictoryIds.Contains([int]$_.Id) } | Sort-Object Id | ForEach-Object { Get-NodeId $_.Id })
    if ($diagramVictoryIds.Count -gt 0) {
        $Lines.Add(('    class {0} victory;' -f ($diagramVictoryIds -join ",")))
    }

    $diagramEndingIds = @($Blocks | Where-Object { $EndingIds.Contains([int]$_.Id) } | Sort-Object Id | ForEach-Object { Get-NodeId $_.Id })
    if ($diagramEndingIds.Count -gt 0) {
        $Lines.Add(('    class {0} ending;' -f ($diagramEndingIds -join ",")))
    }

    if ($hasUnknown) {
        $Lines.Add('    class unknown unknown;')
    }

    $Lines.Add('```')
    $Lines.Add('')
}

if (-not (Test-Path -LiteralPath $BlocksDirectory)) {
    throw "Could not find block directory '$BlocksDirectory'."
}

$blocks = [System.Collections.Generic.List[object]]::new()
$blockById = @{}

foreach ($file in Get-ChildItem -LiteralPath $BlocksDirectory -Filter *.xml | Sort-Object Name) {
    [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw

    $block = [pscustomobject]@{
        Id = [int]$xml.block.id
        Location = [string]$xml.block.location
        Text = Normalize-Whitespace ([string]$xml.block.text)
        Choices = Get-BlockChoices $xml
    }

    $blocks.Add($block)
    $blockById[$block.Id] = $block
}

$edges = [System.Collections.Generic.List[object]]::new()
$seenEdges = @{}

foreach ($block in $blocks) {
    foreach ($choice in $block.Choices) {
        $target = [int]$choice.GetAttribute('to')
        $label = Get-CompactChoiceLabel -Text (Get-XmlChildText $choice 'text') -Condition (Get-XmlChildText $choice 'condition')

        if ($target -eq -1) {
            $effect = Normalize-Whitespace (Get-XmlChildText $choice 'effect')
            if ($block.Id -eq 12 -and $effect -match '\?\s*(\d+)\s*:\s*(\d+)') {
                Add-Edge $edges $seenEdges $block.Id (Get-NodeId ([int]$Matches[1])) "2d6 > Skill"
                Add-Edge $edges $seenEdges $block.Id (Get-NodeId ([int]$Matches[2])) "2d6 <= Skill"
                continue
            }

            Add-Edge $edges $seenEdges $block.Id "unknown" "unknown dynamic target"
            continue
        }

        if ($target -eq 1014) {
            Add-Edge $edges $seenEdges $block.Id (Get-NodeId 224) "1014 => 224"
            continue
        }

        Add-Edge $edges $seenEdges $block.Id (Get-NodeId $target) $label
    }
}

$manualEdges = @(
    @{ From = 40; To = 88; Label = "[hidden] know password 88" }
    @{ From = 43; To = 22; Label = "[hidden] know City of Guardians map ref 22" }
    @{ From = 80; To = 102; Label = "[inferred] leave the museum" }
    @{ From = 103; To = 111; Label = "[hidden] know duel-customs ref 111" }
    @{ From = 122; To = 22; Label = "[hidden] know City of Guardians map ref 22" }
    @{ From = 147; To = 14; Label = "[inferred] visit the Thalian Museum" }
    @{ From = 187; To = 7; Label = "[hidden] know countersign 'Seven'" }
    @{ From = 195; To = 110; Label = "[inferred] leave the junkyard" }
    @{ From = 197; To = 88; Label = "[hidden] recall password 88" }
    @{ From = 205; To = 111; Label = "[hidden] know duel-customs ref 111" }
    @{ From = 236; To = 7; Label = "[hidden] know countersign 'Seven'" }
    @{ From = 275; To = 301; Label = "[hidden] use the Tangler Field" }
    @{ From = 288; To = 112; Label = "[inferred] play Dinosaur Hunt" }
    @{ From = 305; To = 7; Label = "[hidden] know countersign 'Seven'" }
    @{ From = 316; To = 100; Label = "[hidden] Blue Potion (10 letters)" }
    @{ From = 316; To = 150; Label = "[hidden] Lavender Potion (15 letters)" }
    @{ From = 359; To = 200; Label = "[hidden] Wasp model number 200" }
)

foreach ($manualEdge in $manualEdges) {
    Add-Edge $edges $seenEdges $manualEdge.From (Get-NodeId $manualEdge.To) $manualEdge.Label
}

$unknownEdges = @(
    @{ From = 92; Label = "[unknown] return after defeating the Construction Robot" }
    @{ From = 270; Label = "[unknown] flee the jungle" }
    @{ From = 299; Label = "[unknown] take off if your robot can also fly" }
    @{ From = 342; Label = "[unknown] go on your way" }
    @{ From = 356; Label = "[unknown] go left at the jungle fork" }
)

foreach ($unknownEdge in $unknownEdges) {
    Add-Edge $edges $seenEdges $unknownEdge.From "unknown" $unknownEdge.Label
}

foreach ($block in $blocks) {
    $existingTargets = @(
        $edges |
            Where-Object { $_.From -eq $block.Id } |
            ForEach-Object { $_.ToNodeId }
    )

    foreach ($target in Get-ProseTargets $block.Text) {
        $nodeId = Get-NodeId $target
        if ($existingTargets -contains $nodeId) {
            continue
        }

        Add-Edge $edges $seenEdges $block.Id $nodeId "[inferred] direct prose turn"
    }
}

$victoryIds = [System.Collections.Generic.HashSet[int]]::new()
@(140, 354, 355) | ForEach-Object { [void]$victoryIds.Add($_) }

$endingIds = [System.Collections.Generic.HashSet[int]]::new()
foreach ($block in $blocks) {
    $hasOutgoing = $edges.Where({ $_.From -eq $block.Id }).Count -gt 0
    if ($hasOutgoing) {
        continue
    }

    if ($victoryIds.Contains($block.Id)) {
        continue
    }

    if ($block.Text -match '(?i)your adventure is over|you are dead|you are a hero|victorious') {
        [void]$endingIds.Add($block.Id)
    }
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Robot Commando Block Graph')
$lines.Add('')
$lines.Add('- Generated from `RobotCommando/BookData/Blocks/*.xml`.')
$lines.Add('- Edge labels prefixed with `[hidden]`, `[inferred]`, or `[unknown]` were reconstructed from prose, item references, or OCR gaps.')
$lines.Add('- Victory endings are block `140` (awakens Thalos), block `354` (defeats Minos in the Supertank), and block `355` (wins the duel with Minos).')
$lines.Add('- SCC splitting is not very helpful here: the block graph has one giant strongly connected component with 205 nodes, so this file is split by location instead.')
$lines.Add('- Colors: Farm teal, Current indigo, Capital red, Knowledge purple, Industry green, Guardians mint, Jungle gold, Storms blue, Worship violet, Pleasure pink, Inherit gray, Unknown pale yellow.')
$lines.Add("")

Add-LocationOverviewDiagram -Lines $lines -Blocks @($blocks) -Edges @($edges) -BlockById $blockById

$locationGroups = @(
    $blocks |
        Group-Object Location |
        Sort-Object { Get-LocationSortKey $_.Group[0] }, Name
)

foreach ($locationGroup in $locationGroups) {
    Add-LocationDiagram -Lines $lines -Location ([string]$locationGroup.Name) -Blocks @($locationGroup.Group) -Edges @($edges) -BlockById $blockById -VictoryIds $victoryIds -EndingIds $endingIds
}

$lines.Add('## Unresolved Edges')
$lines.Add('')
$lines.Add('- `92 -> ?` after defeating the Construction Robot.')
$lines.Add('- `270 -> ?` after fleeing the jungle with enough ARMOUR left.')
$lines.Add('- `299 -> ?` for the flying take-off option during the Brontosaurus stampede.')
$lines.Add('- `342 -> ?` if you ignore the Robot Repair shop.')
$lines.Add('- `356 -> ?` for the left branch at the jungle fork.')

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

[System.IO.File]::WriteAllLines($OutputPath, $lines)
Write-Host ('Wrote block graph to {0}' -f $OutputPath)
