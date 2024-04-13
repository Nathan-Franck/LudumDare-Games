# Ludum Dare 55 - Summoning

# Development Environment

* Install VSCode - https://code.visualstudio.com/download
    * Get the Zig extension for VSCode
* Install Bun - https://bun.sh/
* Update your Environment PATH
    * Find the path to Zig from your VSCode User Settings JSON file and add the Zig's parent folder to the PATH
    * Bun path should have been updated automatically, can double check


# Running the Game

From the command line:
```
zig build wasm
bun run dev
```
> You should be able to ctrl+click the link to open up the URL to the game!
![alt text](image.png)

# Design
* 4 Machines are working on a summoning
* Machines break down and you have to fix them
* If too many machines break down then the stage resets (4 stages of increasing difficulty)

# TODO

### Nathan
#### Saturday
* Actually implement EaselJS format
* Timed animations displaying
* Character controller - up down left right
#### Sunday
* Machines break down
* Character interaction with machines, play the interaction animation
* Summoning animation
* Stage win/lose/level-up
#### Stretch Goals
* Obstacles - goo, electricity, ??? Something to slow you down while trying to get to the broken machine
* Main Menu, menu transitions