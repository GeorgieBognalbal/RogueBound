Add-Type -AssemblyName System.Drawing

$outputDir = "Decals"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force
}

function Save-Bmp($bmp, $path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$cWhite     = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$cBlack     = [System.Drawing.Color]::FromArgb(255, 20, 20, 25)
$cSteelHi   = [System.Drawing.Color]::FromArgb(255, 235, 240, 245)
$cSteelMid  = [System.Drawing.Color]::FromArgb(255, 175, 185, 195)
$cSteelDark = [System.Drawing.Color]::FromArgb(255, 100, 110, 120)
$cGold      = [System.Drawing.Color]::FromArgb(255, 235, 185, 40)
$cWood      = [System.Drawing.Color]::FromArgb(255, 130, 80, 35)
$cWoodDark  = [System.Drawing.Color]::FromArgb(255, 80, 45, 20)
$cRuby      = [System.Drawing.Color]::FromArgb(255, 220, 35, 45)
$cCyanHi    = [System.Drawing.Color]::FromArgb(255, 140, 240, 255)
$cCyanMid   = [System.Drawing.Color]::FromArgb(255, 30, 180, 235)
$cCyanDark  = [System.Drawing.Color]::FromArgb(255, 15, 90, 160)
$cPurple    = [System.Drawing.Color]::FromArgb(255, 120, 50, 160)
$cPurpleDk  = [System.Drawing.Color]::FromArgb(255, 70, 25, 100)

# 1. Iron Sword (32x32)
$bmpSword = New-Object System.Drawing.Bitmap 32, 32
for ($i = 0; $i -lt 16; $i++) {
    $x = 8 + $i
    $y = 23 - $i
    $bmpSword.SetPixel($x, $y, $cSteelMid)
    $bmpSword.SetPixel($x - 1, $y, $cSteelHi)
    $bmpSword.SetPixel($x, $y + 1, $cSteelDark)
}
# Crossguard
$bmpSword.SetPixel(9, 21, $cGold)
$bmpSword.SetPixel(8, 22, $cGold)
$bmpSword.SetPixel(11, 23, $cGold)
$bmpSword.SetPixel(10, 24, $cGold)
# Grip & Pommel
$bmpSword.SetPixel(7, 24, $cWood)
$bmpSword.SetPixel(6, 25, $cWood)
$bmpSword.SetPixel(5, 26, $cGold)
Save-Bmp $bmpSword 'Decals/weapon_sword.png'

# 2. Hunter's Bow (32x32)
$bmpBow = New-Object System.Drawing.Bitmap 32, 32
for ($t = -10; $t -le 10; $t++) {
    $x = [int](18 - ($t * $t) * 0.08)
    $y = 16 + $t
    if ($x -ge 0 -and $x -lt 32 -and $y -ge 0 -and $y -lt 32) {
        $bmpBow.SetPixel($x, $y, $cWood)
        $bmpBow.SetPixel($x + 1, $y, $cWoodDark)
    }
}
# Bowstring
for ($y = 6; $y -le 26; $y++) {
    $bmpBow.SetPixel(18, $y, [System.Drawing.Color]::FromArgb(200, 240, 240, 240))
}
# Arrow loaded
for ($x = 10; $x -le 24; $x++) {
    $bmpBow.SetPixel($x, 16, $cSteelMid)
}
$bmpBow.SetPixel(10, 15, $cSteelHi)
$bmpBow.SetPixel(10, 17, $cSteelHi)
Save-Bmp $bmpBow 'Decals/weapon_bow.png'

# 3. Heavy Battleaxe (32x32)
$bmpAxe = New-Object System.Drawing.Bitmap 32, 32
# Handle
for ($i = 0; $i -lt 22; $i++) {
    $x = 5 + $i
    $y = 26 - $i
    $bmpAxe.SetPixel($x, $y, $cWood)
    $bmpAxe.SetPixel($x + 1, $y, $cWoodDark)
}
# Double Axe Blades
for ($bx = 18; $bx -le 27; $bx++) {
    for ($by = 5; $by -le 14; $by++) {
        $dx = $bx - 22
        $dy = $by - 9
        $dist = [Math]::Sqrt($dx*$dx + $dy*$dy)
        if ($dist -le 4.8 -and $dist -ge 1.5) {
            $bmpAxe.SetPixel($bx, $by, $cSteelHi)
        } elseif ($dist -lt 1.5) {
            $bmpAxe.SetPixel($bx, $by, $cGold)
        }
    }
}
Save-Bmp $bmpAxe 'Decals/weapon_battleaxe.png'

# 4. Arcane Magic Staff (32x32)
$bmpStaff = New-Object System.Drawing.Bitmap 32, 32
for ($i = 0; $i -lt 20; $i++) {
    $x = 6 + $i
    $y = 26 - $i
    $bmpStaff.SetPixel($x, $y, $cWoodDark)
    $bmpStaff.SetPixel($x + 1, $y, $cWood)
}
# Glowing Crystal Head
for ($x = 22; $x -le 28; $x++) {
    for ($y = 4; $y -le 10; $y++) {
        $dx = $x - 25
        $dy = $y - 7
        $dist = [Math]::Sqrt($dx*$dx + $dy*$dy)
        if ($dist -le 3.0) {
            $bmpStaff.SetPixel($x, $y, $cCyanHi)
        } elseif ($dist -le 3.8) {
            $bmpStaff.SetPixel($x, $y, $cCyanDark)
        }
    }
}
Save-Bmp $bmpStaff 'Decals/weapon_staff.png'

# 5. Shadow Dagger (32x32)
$bmpDagger = New-Object System.Drawing.Bitmap 32, 32
for ($i = 0; $i -lt 11; $i++) {
    $x = 11 + $i
    $y = 20 - $i
    $bmpDagger.SetPixel($x, $y, $cSteelHi)
    $bmpDagger.SetPixel($x + 1, $y, $cPurpleDk)
}
$bmpDagger.SetPixel(11, 19, $cRuby)
$bmpDagger.SetPixel(10, 20, $cRuby)
$bmpDagger.SetPixel(9, 21, $cGold)
$bmpDagger.SetPixel(8, 22, $cGold)
Save-Bmp $bmpDagger 'Decals/weapon_dagger.png'

# 6. Magic Bolt Projectile (16x16)
$bmpBolt = New-Object System.Drawing.Bitmap 16, 16
for ($x = 0; $x -lt 16; $x++) {
    for ($y = 0; $y -lt 16; $y++) {
        $dx = $x - 8
        $dy = $y - 8
        $dist = [Math]::Sqrt($dx*$dx + $dy*$dy)
        if ($dist -le 2.0) {
            $bmpBolt.SetPixel($x, $y, $cWhite)
        } elseif ($dist -le 4.5) {
            $bmpBolt.SetPixel($x, $y, $cCyanHi)
        } elseif ($dist -le 6.5) {
            $bmpBolt.SetPixel($x, $y, $cCyanMid)
        }
    }
}
Save-Bmp $bmpBolt 'Decals/magic_bolt.png'

# 7. Shopkeeper NPC (32x32)
$bmpShop = New-Object System.Drawing.Bitmap 32, 32
# Purple Hood & Robes
for ($y = 8; $y -le 27; $y++) {
    for ($x = 10; $x -le 21; $x++) {
        if ($y -le 15) {
            # Hood
            $dx = $x - 15.5
            $dy = $y - 12
            if (($dx*$dx + $dy*$dy) -le 18) {
                $bmpShop.SetPixel($x, $y, $cPurple)
            }
        } else {
            # Cloak Body
            $bmpShop.SetPixel($x, $y, $cPurpleDk)
        }
    }
}
# Shadowed Face & Glowing Yellow Eyes
$bmpShop.SetPixel(14, 13, $cGold)
$bmpShop.SetPixel(17, 13, $cGold)
# Merchant Candle / Lantern in Hand
$bmpShop.SetPixel(8, 20, $cWoodDark)
$bmpShop.SetPixel(8, 19, $cGold)
$bmpShop.SetPixel(8, 18, [System.Drawing.Color]::FromArgb(255, 255, 100, 30))
Save-Bmp $bmpShop 'Decals/shopkeeper.png'

Write-Output 'All Phase 3 assets generated!'
