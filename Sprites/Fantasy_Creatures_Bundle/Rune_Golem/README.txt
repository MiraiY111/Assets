RUNE GOLEM - PIXEL CREATURE PACK
================================
1 stone golem boss, 4 variants (crystal, magma, frost, void), 7 animations, 51 frames per variant (204 in total).
Side view, faces RIGHT (flip X for left). Frame 208x184 px. Feet on ground line y=176 (pivot x=0.5, y=0.957).

ANIMATIONS (frames / fps)
  idle 8/6 · walk 8/10 · slam 8/10 · punch 6/12 · beam 9/10 · hurt 4/8 · death 8/8
  loop: idle, walk. One-shot: slam, punch, beam, hurt, death (hold last frame; death ends as a rubble pile with a dying core).
  Slam shockwave, dust, and the core beam are baked into the frames. The beam reaches the right edge of the frame (extend it in-engine if you want).

FILES (per variant folder)
  <anim>_sheet.png      one horizontal strip per animation
  <variant>_atlas.png + .json   all frames, TexturePacker-style (frames, fps, loop)
  frames/               every frame as single PNG
  gif/                  preview GIFs
Also: preview_variants.png, preview_all_animations.png, palette.gpl.

ENGINE TIPS
  Unity: Sprite Mode Multiple, Grid by Cell Size 208x184, Filter Point, Compression None, PPU 32.
  Godot: AnimatedSprite2D -> SpriteFrames -> Add frames from sprite sheet. Texture filter Nearest.
  Phaser: this.load.spritesheet('golem','slam_sheet.png',{frameWidth:208,frameHeight:184}).


LICENSE: see LICENSE.txt (CC0 - free for personal and commercial use, credit appreciated, not required).
