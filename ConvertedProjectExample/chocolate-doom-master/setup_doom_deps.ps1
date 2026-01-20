[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$baseUrl = "https://www.libsdl.org/release"
$mixerUrl = "https://www.libsdl.org/projects/SDL_mixer/release"
$netUrl = "https://www.libsdl.org/projects/SDL_net/release"

$sdl2 = "SDL2-devel-2.28.5-VC.zip"
$mixer = "SDL2_mixer-devel-2.6.3-VC.zip"
$net = "SDL2_net-devel-2.2.0-VC.zip"

$destDir = Join-Path $PSScriptRoot "deps"
if (!(Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir | Out-Null }

function Download-And-Extract ($url, $file, $outPath) {
    echo "Downloading $file..."
    $zipPath = Join-Path $outPath $file
    Invoke-WebRequest -Uri "$url/$file" -OutFile $zipPath
    echo "Extracting $file..."
    Expand-Archive -Path $zipPath -DestinationPath $outPath -Force
    Remove-Item $zipPath
}

Download-And-Extract $baseUrl $sdl2 $destDir
Download-And-Extract $mixerUrl $mixer $destDir
Download-And-Extract $netUrl $net $destDir

echo "Dependencies Downloaded in $destDir"
