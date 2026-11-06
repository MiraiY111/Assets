ABYSS JELLY - PIXEL CREATURE PACK
=================================
1 floating bioluminescent jellyfish, 4 variants (aether, ember, toxic, void), 6 animations, 44 frames per variant (176 in total).
Front view (symmetric, no flip needed). Frame 176x184 px. Bell centre line y=66 (pivot x=0.5, y=0.359).

ANIMATIONS (frames / fps)
  idle 8/6 - swim 8/10 - sting 8/10 - shockwave 8/10 - hurt 4/8 - death 8/8
  loop: idle, swim. One-shot: sting, shockwave, hurt, death (death deflates, sinks and dissolves into bubbles).
  Shock ring and bubbles are baked into the frames.

FILES (per variant folder)
  <anim>_sheet.png      one horizontal strip per animation
  <variant>_atlas.png + .json   all frames, TexturePacker-style (frames, fps, loop)
  frames/               every frame as single PNG
  gif/                  preview GIFs
Also: preview_variants.png, preview_all_animations.png, palette.gpl.

ENGINE TIPS
  Unity: Sprite Mode Multiple, Grid by Cell Size 176x184, Filter Point, Compression None, PPU 32.
  Godot: AnimatedSprite2D -> SpriteFrames -> Add frames from sprite sheet. Texture filter Nearest.
  Phaser: this.load.spritesheet('jelly','swim_sheet.png',{frameWidth:176,frameHeight:184}).


LICENSE: see LICENSE.txt (CC0 - free for personal and commercial use, credit appreciated, not required).
