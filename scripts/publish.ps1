#!/bin/pwsh

$version = '0.1.0'

$outputDir = 'JsonLogViewer/JsonLogViewer/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish'
if (test-path $outputDir) {
    remove-item -recurse -force $outputDir
}

dotnet publish JsonLogViewer -c Release -r win-x64

$pdbPath = "$outputDir/JsonLogViewer.pdb"
if (test-path $pdbPath) {
    remove-item $pdbPath
}

$zipPath = "$PWD/JsonLogViewer-v$version.zip"
if (test-path $zipPath) {
    remove-item $zipPath
}
compress-archive "$outputDir/*" $zipPath
