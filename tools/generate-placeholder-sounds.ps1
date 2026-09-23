<#
.SYNOPSIS
    Generates a synthetic "Placeholder" soundpack (short percussive click-like WAV samples).

.DESCRIPTION
    No real keyboard recordings exist yet for this project. This script produces small,
    clearly-synthetic 16-bit PCM WAV files (band-limited noise burst + pitched click, short
    exponential decay) so the app has something audible to load and play during development.
    They are intentionally distinct from real keyboard sounds and are meant to be replaced by
    dropping a new folder under assets/soundpacks/ later - nothing in the app hardcodes these
    files, see the SoundPackManager discovery logic.

    Run from anywhere; writes into <repoRoot>/assets/soundpacks/Placeholder/.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packRoot = Join-Path $repoRoot 'assets\soundpacks\Placeholder'

$sampleRate = 44100
$bitsPerSample = 16
$channels = 1

function New-ClickSample {
    param(
        [string]$Path,
        [double]$DurationSeconds,
        [double]$PitchHz,
        [double]$NoiseAmount,
        [double]$Seed
    )

    $sampleCount = [int]($sampleRate * $DurationSeconds)
    $rand = New-Object System.Random([int]($Seed * 100000))
    $samples = [int16[]]::new($sampleCount)

    for ($i = 0; $i -lt $sampleCount; $i++) {
        $t = $i / $sampleRate
        # Exponential decay envelope so it reads as a percussive "click", not a sustained tone.
        $envelope = [Math]::Exp(-$t * 38.0)
        $tone = [Math]::Sin(2 * [Math]::PI * $PitchHz * $t) * (1.0 - $NoiseAmount)
        $noise = (($rand.NextDouble() * 2.0) - 1.0) * $NoiseAmount
        $value = ($tone + $noise) * $envelope
        $clamped = [Math]::Max(-1.0, [Math]::Min(1.0, $value))
        $samples[$i] = [int]($clamped * 32000)
    }

    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
    try {
        $writer = New-Object System.IO.BinaryWriter($stream)
        $dataSize = $sampleCount * $channels * ($bitsPerSample / 8)
        $byteRate = $sampleRate * $channels * ($bitsPerSample / 8)
        $blockAlign = $channels * ($bitsPerSample / 8)

        $writer.Write([char[]]"RIFF")
        $writer.Write([int](36 + $dataSize))
        $writer.Write([char[]]"WAVE")
        $writer.Write([char[]]"fmt ")
        $writer.Write([int]16)
        $writer.Write([int16]1)          # PCM
        $writer.Write([int16]$channels)
        $writer.Write([int]$sampleRate)
        $writer.Write([int]$byteRate)
        $writer.Write([int16]$blockAlign)
        $writer.Write([int16]$bitsPerSample)
        $writer.Write([char[]]"data")
        $writer.Write([int]$dataSize)
        foreach ($s in $samples) { $writer.Write([int16]$s) }
        $writer.Flush()
    }
    finally {
        $stream.Dispose()
    }
}

Write-Host "Generating placeholder soundpack at $packRoot"

# normal: 4 varied samples for natural-feeling typing (SampleSelector avoids immediate repeats)
New-ClickSample -Path (Join-Path $packRoot 'normal\click01.wav') -DurationSeconds 0.09 -PitchHz 1400 -NoiseAmount 0.55 -Seed 1
New-ClickSample -Path (Join-Path $packRoot 'normal\click02.wav') -DurationSeconds 0.09 -PitchHz 1550 -NoiseAmount 0.50 -Seed 2
New-ClickSample -Path (Join-Path $packRoot 'normal\click03.wav') -DurationSeconds 0.10 -PitchHz 1300 -NoiseAmount 0.60 -Seed 3
New-ClickSample -Path (Join-Path $packRoot 'normal\click04.wav') -DurationSeconds 0.09 -PitchHz 1480 -NoiseAmount 0.52 -Seed 4

# space: lower-pitched, slightly longer thock
New-ClickSample -Path (Join-Path $packRoot 'space\space01.wav') -DurationSeconds 0.13 -PitchHz 700 -NoiseAmount 0.45 -Seed 5
New-ClickSample -Path (Join-Path $packRoot 'space\space02.wav') -DurationSeconds 0.13 -PitchHz 650 -NoiseAmount 0.48 -Seed 6

# enter: distinct, slightly deeper double-character click
New-ClickSample -Path (Join-Path $packRoot 'enter\enter01.wav') -DurationSeconds 0.12 -PitchHz 900 -NoiseAmount 0.40 -Seed 7
New-ClickSample -Path (Join-Path $packRoot 'enter\enter02.wav') -DurationSeconds 0.12 -PitchHz 950 -NoiseAmount 0.42 -Seed 8

# backspace: sharper/higher, short
New-ClickSample -Path (Join-Path $packRoot 'backspace\backspace01.wav') -DurationSeconds 0.08 -PitchHz 1800 -NoiseAmount 0.60 -Seed 9
New-ClickSample -Path (Join-Path $packRoot 'backspace\backspace02.wav') -DurationSeconds 0.08 -PitchHz 1900 -NoiseAmount 0.58 -Seed 10

Write-Host "Done."
