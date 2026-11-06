SPIRIT FOX (KITSUNE) - PIXEL CREATURE PACK
==========================================
1 nine-tailed spirit fox, 4 variants (spirit, ember, shadow, gold), 8 animations, 57 frames per variant (228 in total).
Side view, faces RIGHT (flip X for left). Frame 224x144 px. Paws on ground line y=136 (pivot x=0.5, y=0.944).

ANIMATIONS (frames / fps)
  idle 8/6 · walk 8/10 · run 8/14 · jump 6/8 · pounce 7/12 · foxfire 8/10 · hurt 4/8 · death 8/8
  loop: idle, walk, run. One-shot: jump, pounce, foxfire, hurt, death (death dissolves into spirit orbs).
  Foxfire orbs are baked into the frames (they fly to the right edge of the frame).

FILES (per variant folder)
  <anim>_sheet.png      one horizontal strip per animation
  <variant>_atlas.png + .json   all frames, TexturePacker-style (frames, fps, loop)
  frames/               every frame as single PNG
  gif/                  preview GIFs
Also: preview_variants.png, preview_all_animations.png, palette.gpl.

ENGINE TIPS
  Unity: Sprite Mode Multiple, Grid by Cell Size 224x144, Filter Point, Compression None, PPU 32.
  Godot: AnimatedSprite2D -> SpriteFrames -> Add frames from sprite sheet. Texture filter Nearest.
  Phaser: this.load.spritesheet('fox','run_sheet.png',{frameWidth:224,frameHeight:144}).


LICENSE: see LICENSE.txt (CC0 - free for personal and commercial use, credit appreciated, not required).
