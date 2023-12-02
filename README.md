# ZX-Animator - Packer for Windows and player for ZX Spectrum Computers

A SCR packer to play similar full screen images as animation  

Not very friendly. this can only relevant if changes between frames are minimal. 
This is just a crude delta compression. See example, 12 frames of animation reduced to 10kb (normally takes ~80kb) and updates up to 50 fps.

usage:
1. draw your screens in zx spectrum SCR format.
2. open Windows program zx-animator (there is a windows binary release in zip file)
3. select and load your screens to zxanimator, and arrange them so they are in order.
4. set delay, default is 5 (50/5=10 fps per second)
5. press pack. a .zxa file will be generated at the root of zx-animator folder.
6. put this zxa file next to zxanimator.asm (rename your zxa file to 01.zxa)
7. open a dos (terminal) window and type: pasmo -v --tapbas zxa-animator128.asm animation_out.tap
8. test the resulting tap file.


; Creative Commons Attribution-NonCommercial 4.0 International Public License 
https://creativecommons.org/licenses/by-nc/4.0/legalcode
