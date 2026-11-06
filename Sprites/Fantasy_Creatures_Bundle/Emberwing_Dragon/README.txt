EMBERWING - PIXEL DRAGON PACK
=============================
1 fantasy dragon, 4 colour variants (ember, frost, venom, void), 8 animations, 61 frames per variant (244 frames in total).
Side view, faces RIGHT (flip X for left). Frame size 264x184 px. Feet on ground line y=146 (pivot x=0.5, y=0.793).
Scale: body ~ 120 px long, 1 m ~ 24 px if you treat it as a 5 m dragon, or use PPU 32 for a large boss.

ANIMATIONS (frames / fps)
  idle 8/6 · walk 8/10 · fly 8/12 · fire_breath 10/10 · bite 7/12 · hurt 4/8 · roar 8/8 · death 8/8
  loop: idle, walk, fly. One-shot: fire_breath, bite, hurt, roar, death (hold the last frame).

FILES (per variant folder)
  <anim>_sheet.png   one horizontal strip per animation
  <variant>_atlas.png + .json   all frames, TexturePacker-style (frames, fps, loop)
  frames/            every frame as single PNG
  gif/               preview GIFs
Also: preview_variants.png, preview_all_animations.png, palette.gpl (GIMP/Aseprite).

ENGINE TIPS
  Unity: Sprite Mode Multiple, Grid by Cell Size 264x184, Filter Point, Compression None, PPU 32.
  Godot: AnimatedSprite2D -> SpriteFrames -> Add frames from sprite sheet (H frames = frame count). Texture filter Nearest.
  Phaser: this.load.spritesheet('dragon','fire_breath_sheet.png',{frameWidth:264,frameHeight:184}).
  Fire breath is part of the sprite (baked into frames). Add your own hitbox in front of the mouth.


LICENSE: see LICENSE.txt (CC0 - free for personal and commercial use, credit appreciated, not required).
