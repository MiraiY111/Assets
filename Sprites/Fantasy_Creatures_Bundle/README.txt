FANTASY CREATURES BUNDLE - 4 ANIMATED PIXEL ART PACKS
=====================================================
Thank you for downloading! Everything here is free (CC0).

CONTENTS
  Emberwing_Dragon/   dragon          264x184 px   8 animations   4 variants   244 frames
  Rune_Golem/         stone golem     208x184 px   7 animations   4 variants   204 frames
  Spirit_Fox/         nine-tailed fox 224x144 px   8 animations   4 variants   228 frames
  Abyss_Jelly/        jellyfish       176x184 px   6 animations   4 variants   176 frames
  TOTAL: 4 creatures, 16 variants, 852 frames.

Every folder has its own README with animation lists, fps, pivots and engine tips.
Inside each variant folder: sprite sheets, atlas PNG + JSON (TexturePacker-style), single frames, preview GIFs.
Each creature folder also has palette.gpl (GIMP / Aseprite) and preview images.

QUICK START
  Unity:  Sprite Mode Multiple, Grid By Cell Size = frame size of the creature, Filter Point, Compression None, PPU 32.
  Godot:  AnimatedSprite2D -> SpriteFrames -> Add frames from sprite sheet. Texture filter Nearest.
  Phaser: this.load.spritesheet('key', 'idle_sheet.png', {frameWidth: W, frameHeight: H}).
  Dragon, golem and fox face RIGHT (flip X for left). The jellyfish is symmetric.

LICENSE: CC0 - see LICENSE.txt
