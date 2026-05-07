param(
    [string] $WorkbookPath = "resources\depot-internet.xlsx",
    [string] $JsonPath = "src\AssociationSaleTool.Web\wwwroot\data\isere-postal-cities.json"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$workspacePath = [System.IO.Path]::GetFullPath((Get-Location).Path)
$workbookFullPath = [System.IO.Path]::GetFullPath($WorkbookPath)
$jsonFullPath = [System.IO.Path]::GetFullPath($JsonPath)

if (-not $workbookFullPath.StartsWith($workspacePath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Workbook path is outside the workspace: $workbookFullPath"
}

if (-not $jsonFullPath.StartsWith($workspacePath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "JSON path is outside the workspace: $jsonFullPath"
}

$utf8 = New-Object System.Text.UTF8Encoding($false)

function Escape-Xml([string] $Value) {
    return [System.Security.SecurityElement]::Escape($Value)
}

function Read-ZipEntryText(
    [System.IO.Compression.ZipArchive] $Archive,
    [string] $EntryName
) {
    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "Missing workbook entry $EntryName"
    }

    $reader = New-Object System.IO.StreamReader($entry.Open(), [System.Text.Encoding]::UTF8)
    try {
        return $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}

function Set-InlineStringCell(
    [xml] $Xml,
    [string] $CellReference,
    [string] $Value
) {
    $spreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($Xml.NameTable)
    $namespaceManager.AddNamespace("s", $spreadsheetNamespace)

    $cell = $Xml.SelectSingleNode("//s:c[@r='$CellReference']", $namespaceManager)
    if ($null -eq $cell) {
        throw "Missing cell $CellReference"
    }

    $cell.SetAttribute("t", "inlineStr")

    foreach ($child in @($cell.ChildNodes)) {
        if ($child.LocalName -eq "v" -or $child.LocalName -eq "f" -or $child.LocalName -eq "is") {
            [void] $cell.RemoveChild($child)
        }
    }

    $inlineString = $Xml.CreateElement("is", $spreadsheetNamespace)
    $text = $Xml.CreateElement("t", $spreadsheetNamespace)
    $text.InnerText = $Value
    [void] $inlineString.AppendChild($text)
    [void] $cell.AppendChild($inlineString)
}

function Set-PostalCityValidationRange(
    [xml] $Xml,
    [int] $LastRow
) {
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($Xml.NameTable)
    $namespaceManager.AddNamespace("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main")
    $namespaceManager.AddNamespace("xm", "http://schemas.microsoft.com/office/excel/2006/main")

    $formula = $Xml.SelectSingleNode("//x14:dataValidation/x14:formula1/xm:f", $namespaceManager)
    if ($null -ne $formula) {
        $formula.InnerText = "'codes postaux'!" + '$D$2:$D$' + $LastRow
    }
}

function Set-PostalCityDefinedName(
    [xml] $Xml,
    [int] $LastRow
) {
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($Xml.NameTable)
    $namespaceManager.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main")

    $definedName = $Xml.SelectSingleNode("//s:definedName[@name='_xlnm._FilterDatabase' and @localSheetId='0']", $namespaceManager)
    if ($null -ne $definedName) {
        $definedName.InnerText = "'codes postaux'!" + '$A$2:$D$' + $LastRow
    }
}

function Convert-XmlDocumentToUtf8Bytes([xml] $Xml) {
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = $script:utf8
    $settings.Indent = $false
    $settings.OmitXmlDeclaration = $false

    $stream = New-Object System.IO.MemoryStream
    $writer = [System.Xml.XmlWriter]::Create($stream, $settings)
    try {
        $Xml.Save($writer)
        $writer.Flush()
        return $stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Write-ZipEntryBytes(
    [System.IO.Compression.ZipArchive] $Archive,
    [string] $EntryName,
    [byte[]] $Bytes
) {
    $entry = $Archive.CreateEntry($EntryName, [System.IO.Compression.CompressionLevel]::Optimal)
    $stream = $entry.Open()
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
    }
    finally {
        $stream.Dispose()
    }
}

$jsonObject = Get-Content -Encoding UTF8 -Raw -LiteralPath $jsonFullPath | ConvertFrom-Json
$rows = New-Object System.Collections.Generic.List[object]
foreach ($property in $jsonObject.PSObject.Properties) {
    $postalCode = $property.Name
    foreach ($city in $property.Value) {
        $rows.Add([pscustomobject]@{
            PostalCode = $postalCode
            City = $city
            Display = "$postalCode $city"
        })
    }
}

if ($rows.Count -eq 0) {
    throw "No postal city values were loaded from $jsonFullPath"
}

$lastRow = $rows.Count + 1
$sampleDisplay = ($rows | Where-Object { $_.PostalCode -eq "38250" } | Select-Object -First 1).Display
if ([string]::IsNullOrWhiteSpace($sampleDisplay)) {
    throw "Could not find a sample 38250 city in $jsonFullPath"
}

$sheetBuilder = New-Object System.Text.StringBuilder
[void] $sheetBuilder.AppendLine('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
[void] $sheetBuilder.AppendLine('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">')
[void] $sheetBuilder.AppendLine('  <sheetPr codeName="Feuil2"/>')
[void] $sheetBuilder.AppendLine("  <dimension ref=""A2:D$lastRow""/>")
[void] $sheetBuilder.AppendLine('  <sheetViews><sheetView workbookViewId="0"/></sheetViews>')
[void] $sheetBuilder.AppendLine('  <sheetFormatPr defaultRowHeight="14.5"/>')
[void] $sheetBuilder.AppendLine('  <cols><col min="1" max="1" width="12" customWidth="1"/><col min="2" max="2" width="32" customWidth="1"/><col min="4" max="4" width="42" customWidth="1"/></cols>')
[void] $sheetBuilder.AppendLine('  <sheetData>')

$rowNumber = 2
foreach ($row in $rows) {
    $postalCode = Escape-Xml $row.PostalCode
    $city = Escape-Xml $row.City
    $display = Escape-Xml $row.Display
    [void] $sheetBuilder.AppendLine("    <row r=""$rowNumber""><c r=""A$rowNumber"" t=""inlineStr""><is><t>$postalCode</t></is></c><c r=""B$rowNumber"" t=""inlineStr""><is><t>$city</t></is></c><c r=""D$rowNumber"" t=""inlineStr""><is><t>$display</t></is></c></row>")
    $rowNumber++
}

[void] $sheetBuilder.AppendLine('  </sheetData>')
[void] $sheetBuilder.AppendLine('  <sheetProtection formatCells="0" formatRows="0"/>')
[void] $sheetBuilder.AppendLine('  <pageMargins left="0.70866141732283472" right="0.70866141732283472" top="0.74803149606299213" bottom="0.74803149606299213" header="0.31496062992125984" footer="0.31496062992125984"/>')
[void] $sheetBuilder.AppendLine('</worksheet>')
$replacementSheet1Bytes = $utf8.GetBytes($sheetBuilder.ToString())

$tempPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($workbookFullPath), "depot-internet.tmp.xlsx")
if ([System.IO.File]::Exists($tempPath)) {
    [System.IO.File]::Delete($tempPath)
}

$source = [System.IO.Compression.ZipFile]::OpenRead($workbookFullPath)
try {
    $sheet2Xml = [xml] (Read-ZipEntryText $source "xl/worksheets/sheet2.xml")
    Set-InlineStringCell $sheet2Xml "F3" $sampleDisplay
    Set-PostalCityValidationRange $sheet2Xml $lastRow
    $replacementSheet2Bytes = Convert-XmlDocumentToUtf8Bytes $sheet2Xml

    $workbookXml = [xml] (Read-ZipEntryText $source "xl/workbook.xml")
    Set-PostalCityDefinedName $workbookXml $lastRow
    $replacementWorkbookBytes = Convert-XmlDocumentToUtf8Bytes $workbookXml

    $destination = [System.IO.Compression.ZipFile]::Open($tempPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $source.Entries) {
            if ($entry.FullName -eq "xl/worksheets/sheet1.xml") {
                Write-ZipEntryBytes $destination $entry.FullName $replacementSheet1Bytes
                continue
            }

            if ($entry.FullName -eq "xl/worksheets/sheet2.xml") {
                Write-ZipEntryBytes $destination $entry.FullName $replacementSheet2Bytes
                continue
            }

            if ($entry.FullName -eq "xl/workbook.xml") {
                Write-ZipEntryBytes $destination $entry.FullName $replacementWorkbookBytes
                continue
            }

            $newEntry = $destination.CreateEntry($entry.FullName, [System.IO.Compression.CompressionLevel]::Optimal)
            $input = $entry.Open()
            $output = $newEntry.Open()
            try {
                $input.CopyTo($output)
            }
            finally {
                $output.Dispose()
                $input.Dispose()
            }
        }
    }
    finally {
        $destination.Dispose()
    }
}
finally {
    $source.Dispose()
}

[System.IO.File]::Copy($tempPath, $workbookFullPath, $true)
[System.IO.File]::Delete($tempPath)

Write-Output "Updated $($rows.Count) postal/city values in $WorkbookPath from $JsonPath."
